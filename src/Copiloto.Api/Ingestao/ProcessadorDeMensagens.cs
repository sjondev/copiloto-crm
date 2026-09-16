using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;
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
    private readonly FilaDeMensagens _fila;
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ProcessadorDeMensagens> _log;

    /// <param name="escopos">
    /// O worker vive enquanto a aplicacao vive, e o `DbContext` nao pode viver
    /// tanto: ele acumula rastreamento de entidade e nunca devolve a conexao.
    /// Um escopo POR MENSAGEM tambem isola o estrago — mensagem que falha nao
    /// deixa entidade suja para a proxima.
    /// </param>
    public ProcessadorDeMensagens(
        FilaDeMensagens fila, IServiceScopeFactory escopos, ILogger<ProcessadorDeMensagens> log)
    {
        _fila = fila;
        _escopos = escopos;
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
        using var escopo = _escopos.CreateScope();
        var resolvedor = escopo.ServiceProvider.GetRequiredService<ResolvedorDeLead>();
        var ctx = escopo.ServiceProvider.GetRequiredService<CopilotoDbContext>();

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
            await Guardar(ctx, resolvedor, bruta, doCliente);
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
        CopilotoDbContext ctx, ResolvedorDeLead resolvedor, MensagemRecebida bruta, Telefone doCliente)
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

        var lead = resolvedor.Resolver(doCliente, bruta.EnviadaEm);
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

        conversa.Registrar(new Mensagem(id, autor, bruta.Texto, bruta.EnviadaEm, bruta.Midia));
        await ctx.SaveChangesAsync();

        _log.LogInformation(
            "Fala {Id} de {Autor} guardada na conversa {Conversa} do lead {Lead}",
            bruta.ProviderMessageId, autor, conversa.Id, lead.Id);
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
