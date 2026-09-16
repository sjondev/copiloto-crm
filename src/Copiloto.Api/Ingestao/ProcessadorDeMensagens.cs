using Copiloto.Api.Ia;
using Copiloto.Api.Infra;
using Copiloto.Api.Leitura;
using Copiloto.Api.Persistencia;
using Copiloto.Api.TempoReal;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// O worker que consome a fila fora do webhook (#40) e guarda a fala (#158).
///
/// Processar dentro do handler e o erro classico: provedor lento vira timeout na
/// origem, timeout vira reentrega, reentrega vira custo duplicado — e o custo
/// duplicado e' de dinheiro, nao de CPU.
///
/// `MODEL_PROVIDER=fake` e' o padrao, entao isto roda offline e de graca ate' o
/// provedor de verdade entrar.
/// </summary>
public class ProcessadorDeMensagens : BackgroundService
{
    private readonly IQueue<MensagemRecebida> _fila;
    private readonly IServiceScopeFactory _escopos;
    private readonly GuardaDeReentrega _guarda;
    private readonly ILogger<ProcessadorDeMensagens> _log;

    /// <param name="escopos">
    /// O worker vive enquanto a aplicacao vive, e o `DbContext` nao pode viver
    /// tanto: ele acumula rastreamento de entidade e nunca devolve a conexao.
    /// Um escopo POR MENSAGEM tambem isola o estrago — mensagem que falha nao
    /// deixa entidade suja para a proxima.
    /// </param>
    public ProcessadorDeMensagens(
        IQueue<MensagemRecebida> fila, IServiceScopeFactory escopos,
        GuardaDeReentrega guarda, ILogger<ProcessadorDeMensagens> log)
    {
        _fila = fila;
        _escopos = escopos;
        _guarda = guarda;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // O token NAO e' repassado ao Ler: no desligamento a fila para de
        // aceitar e o laco termina sozinho ao esvaziar. Passar o token aqui
        // abortaria no meio e descartaria o que ja estava dentro, que e' o
        // oposto de drenar.
        try
        {
            await foreach (var mensagem in _fila.Ler(CancellationToken.None))
            {
                await Processar(mensagem);
            }
        }
        catch (OperationCanceledException)
        {
            // Desligamento estourou o prazo do host. O que sobrou se perde, e a
            // durabilidade e' assunto da #69.
        }
    }

    private async Task Processar(MensagemRecebida bruta)
    {
        // A dedupe fica AQUI, e nao no webhook, de proposito: marcar antes de
        // enfileirar descartaria a reentrega de uma mensagem que se perdeu na
        // fila quando o processo caiu — trocaria custo duplicado por fala do
        // cliente sumida, que e' o desfecho pior.
        if (!await _guarda.EhAPrimeiraVez(bruta.ProviderMessageId, CancellationToken.None))
        {
            _log.LogInformation(
                "Mensagem {Id} ja processada: reentrega ignorada", bruta.ProviderMessageId);
            return;
        }

        using var escopo = _escopos.CreateScope();
        var resolvedor = escopo.ServiceProvider.GetRequiredService<ResolvedorDeLead>();
        var ctx = escopo.ServiceProvider.GetRequiredService<CopilotoDbContext>();
        var agente = escopo.ServiceProvider.GetService<AgenteDeLeitura>();
        var tela = escopo.ServiceProvider.GetService<IHubContext<DossieHub>>();
        var triador = escopo.ServiceProvider.GetService<Triador>();

        var doCliente = resolvedor.TelefoneDoCliente(bruta);
        if (doCliente is null)
        {
            // Numero irreconhecivel nao derruba o worker nem some: fica no log
            // com o id do provedor, que e por onde alguem consegue ir atras.
            _log.LogWarning(
                "Mensagem {Id} descartada: nem De ({De}) nem Para ({Para}) e telefone valido",
                bruta.ProviderMessageId, bruta.De, bruta.Para);
            return;
        }

        try
        {
            await Guardar(ctx, resolvedor, agente, triador, tela, bruta, doCliente);
        }
        catch (DbUpdateException e)
        {
            // Duas instancias gravando a mesma reentrega ao mesmo tempo: as duas
            // leem "nao existe" e o banco recusa a segunda. Perder essa corrida
            // e o desfecho CERTO — a fala ja esta la.
            _log.LogWarning(e,
                "Mensagem {Id} nao gravada: o banco recusou, provavelmente reentrega concorrente",
                bruta.ProviderMessageId);
        }
    }

    private async Task Guardar(
        CopilotoDbContext ctx,
        ResolvedorDeLead resolvedor,
        AgenteDeLeitura? agente,
        Triador? triador,
        IHubContext<DossieHub>? tela,
        MensagemRecebida bruta,
        Telefone doCliente)
    {
        var id = IdDaMensagem.De(bruta.ProviderMessageId);

        // A reentrega e' reconhecida ANTES de qualquer escrita. A Conversa
        // tambem ignora id repetido, mas ela so protege o que ja esta carregado
        // — e carregar a conversa inteira para descobrir isso sai caro.
        if (await ctx.Mensagens.AnyAsync(m => m.Id == id))
        {
            _log.LogInformation(
                "Mensagem {Id} ja estava no banco: reentrega do provedor, ignorada",
                bruta.ProviderMessageId);
            return;
        }

        // O dono do aparelho que recebeu a fala vira dono do lead novo (#175).
        // Lead que ja tem dono nao muda de mao por causa do aparelho.
        var lead = resolvedor.Resolver(doCliente, bruta.EnviadaEm, resolvedor.VendedorDaTroca(bruta));
        var autor = resolvedor.QuemFalou(Telefone.Normalizar(bruta.De)!);

        // A colecao vem junto porque `Registrar` mantem a ordem cronologica
        // dentro dela. Conversa muito longa vai pesar aqui, e o dia em que
        // pesar o conserto e inserir pela FK em vez de pela agregacao.
        var conversa = await ctx.Conversas
            .Include(c => c.Mensagens)
            .FirstOrDefaultAsync(c => c.LeadId == lead.Id);

        if (conversa is null)
        {
            conversa = new Conversa(Guid.NewGuid(), lead.Id);
            ctx.Conversas.Add(conversa);
        }

        // A fala anterior precisa ser lida ANTES de registrar a nova, senao a
        // "anterior" seria ela mesma — e a triagem perderia justamente o
        // contexto que a torna segura.
        var anterior = conversa.Mensagens.Count > 0
            ? conversa.Mensagens.MaxBy(m => m.EnviadaEm)
            : null;

        var nova = new Mensagem(id, autor, bruta.Texto, bruta.EnviadaEm, bruta.Midia);
        conversa.Registrar(nova);

        var deal = await DealAberto(ctx, lead.Id, bruta.EnviadaEm);
        await ctx.SaveChangesAsync();

        _log.LogInformation(
            "Fala {Id} de {Autor} guardada na conversa {Conversa} do lead {Lead}",
            bruta.ProviderMessageId, autor, conversa.Id, lead.Id);

        // A fala e SEMPRE guardada; a triagem decide apenas se vale reler (#37).
        // Guardar depende de ter chegado, nao de ser interessante — e o que faz
        // "obrigado" continuar na conversa da tela mesmo sem gerar dossie novo.
        if (triador is not null)
        {
            var triagem = await triador.Triar(nova, anterior, CancellationToken.None);
            if (!triagem.Analisa) return;
        }

        await Reler(ctx, agente, tela, conversa, deal, lead.Id);
    }

    /// <summary>
    /// O negocio daquele lead, aberto na primeira fala.
    ///
    /// No WhatsApp nao existe cadastro previo nem botao de "iniciar negociacao":
    /// a primeira mensagem JA e a negociacao. Esperar alguem abrir o Deal na mao
    /// deixaria o custo de IA sem dono desde a primeira chamada — e vincular
    /// custo depois exige backfill e adivinhacao (#2).
    ///
    /// Um Deal por lead, por enquanto. Cliente que volta meses depois para
    /// comprar outra coisa merece Deal novo, e isso e decisao de produto que
    /// ainda nao foi tomada — quando for, o lugar e aqui.
    /// </summary>
    private static async Task<Deal> DealAberto(
        CopilotoDbContext ctx, Guid leadId, DateTimeOffset quando)
    {
        // O mais recente e escolhido no CLIENTE: o SQLite da suite nao aceita
        // DateTimeOffset em ORDER BY (TECH-005, #56). Filtrar por lead acontece
        // no banco, entao o que vem para a memoria sao os deals de UM lead.
        var doLead = await ctx.Deals.Where(d => d.LeadId == leadId).ToListAsync();

        var existente = doLead.MaxBy(d => d.AbertoEm);
        if (existente is not null) return existente;

        var novo = new Deal(Guid.NewGuid(), leadId, quando);
        ctx.Deals.Add(novo);
        return novo;
    }

    /// <summary>
    /// Refaz a leitura com a fala nova e guarda o dossie (#161).
    ///
    /// Falha de leitura NAO derruba a ingestao: a fala ja esta no banco, que e o
    /// que nao pode se perder. Cascata esgotada devolve null e a tela segue
    /// mostrando o dossie anterior — e essa e a decisao da #30, nao um descuido.
    /// </summary>
    private async Task Reler(
        CopilotoDbContext ctx,
        AgenteDeLeitura? agente,
        IHubContext<DossieHub>? tela,
        Conversa conversa,
        Deal deal,
        Guid leadId)
    {
        // Agente opcional de proposito: guardar a fala e o que nao pode falhar.
        // Uma instalacao sem camada de IA configurada continua sendo um CRM que
        // registra conversa — deixar a ingestao morrer por isso trocaria uma
        // funcionalidade ausente por perda de dado.
        if (agente is null)
        {
            _log.LogWarning(
                "Sem AgenteDeLeitura registrado: a fala do deal {Deal} foi guardada sem leitura",
                deal.Id);
            return;
        }

        var grupo = tela?.Clients.Group(DossieHub.Grupo(leadId));

        try
        {
            // O aviso sai ANTES da leitura. O intervalo entre a fala chegar e o
            // dossie ficar pronto e visivel a olho nu, e tela parada nesse
            // intervalo parece tela quebrada.
            if (grupo is not null)
                await grupo.SendAsync(DossieHub.Analisando, leadId, CancellationToken.None);

            var leitura = await agente.Ler(conversa, deal.Id, "", "", CancellationToken.None);

            // A linha do ledger entra ANTES de qualquer decisao sobre o dossie
            // (#1): a chamada aconteceu e custou, tenha ela degradado ou nao.
            // Registrar so no caminho feliz esconderia justamente o gasto que
            // ninguem esperava ter.
            if (leitura.Medicao is { } medicao)
            {
                deal.RegistrarInvocacao(new AiInvocation(
                    Guid.NewGuid(), Tarefa.Leitura, medicao, DateTimeOffset.UtcNow, deal.Id));
                await ctx.SaveChangesAsync();
            }

            var dossie = leitura.Dossie;
            if (dossie is null)
            {
                // Degradou (#30). O aviso de "analisando" precisa ser desfeito,
                // senao a tela fica girando para sempre por causa de uma
                // leitura que nunca vai chegar.
                if (grupo is not null)
                    await grupo.SendAsync(DossieHub.DossieAtualizado, null, CancellationToken.None);
                return;
            }

            // As lacunas da FICHA entram depois das que o agente escreveu (#8).
            // Sao duas fontes diferentes e complementares: o agente aponta o que
            // so aparece lendo a conversa, e a ficha cobra os slots que ela sabe
            // cobrar — e essa metade nao depende de modelo, entao nao alucina.
            //
            // Entram ATE o teto, e nao todas. Somar as duas fontes sem limite
            // produz oito perguntas de uma vez — que e o estado `CheioDemais` do
            // Storybook (#170), onde as lacunas caem abaixo da dobra e a parte
            // mais util do dossie deixa de ser vista. As da conversa ja estao
            // dentro; a ficha completa o que sobrar de espaco.
            var ficha = await ctx.Fichas.FirstOrDefaultAsync(f => f.LeadId == leadId);

            foreach (var lacuna in Lacunas.De(ficha, dossie.Lacunas))
            {
                if (dossie.Lacunas.Count >= Lacunas.Maximo) break;
                dossie.RegistrarLacuna(lacuna.Pergunta);
            }

            ctx.Dossies.Add(dossie);
            await ctx.SaveChangesAsync();

            _log.LogInformation(
                "Dossie {Dossie} gerado para o deal {Deal}: {Sinais} sinal(is), {Lacunas} lacuna(s)",
                dossie.Id, deal.Id, dossie.Sinais.Count, dossie.Lacunas.Count);

            if (grupo is not null)
            {
                await grupo.SendAsync(
                    DossieHub.DossieAtualizado,
                    EndpointsDeLeitura.ParaTela(dossie, leadId),
                    CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            _log.LogError(e,
                "Leitura falhou no deal {Deal}, mas a fala ja esta guardada", deal.Id);

            // Mesma razao do caso degradado: a tela nao pode ficar presa no
            // "analisando" por causa de um erro que ela nao tem como saber.
            if (grupo is not null)
                await grupo.SendAsync(DossieHub.DossieAtualizado, null, CancellationToken.None);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Ordem importa: fechar para escrita ANTES de esperar. Ao contrario, o
        // laco ficaria esperando trabalho que nunca chega e o desligamento
        // dependeria do timeout do host.
        _fila.PararDeAceitar();
        await base.StopAsync(cancellationToken);
    }
}
