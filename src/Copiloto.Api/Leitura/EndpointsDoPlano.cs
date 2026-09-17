using System.Security.Claims;
using Copiloto.Api.Auth;
using Copiloto.Api.Ia;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Planos;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Leitura;

/// <summary>Um bloco como a tela o recebe.</summary>
/// <param name="TemProcedencia">
/// Se da para perguntar "por que essa sugestao?" (#51). Falso nas sugestoes
/// geradas antes da issue, e a tela nao mostra um botao que responderia 404.
/// </param>
public record BlocoNaTela(string Bloco, string Texto, string? Sugestao, bool TemProcedencia);

/// <summary>O plano inteiro, com a versao que responde "o que ele tinha escrito".</summary>
public record PlanoNaTela(Guid PlanoId, Guid DealId, int Versao, IReadOnlyList<BlocoNaTela> Blocos);

/// <summary>O que o vendedor escreveu num bloco.</summary>
public record TextoDoBloco(string? Texto);

/// <summary>
/// O que sustentou a sugestao (#51).
///
/// A resposta pronta para "como voce sabe que a IA nao esta inventando?" — e,
/// para o vendedor, o que constroi confianca: ele ve o texto EXATO que foi ao
/// modelo, ja mascarado, e o que aquilo custou.
/// </summary>
public record PorQueNaTela(
    string Modelo,
    string? VersaoDoPrompt,
    decimal CustoEmReais,
    int LatenciaMs,
    int TokensEntrada,
    int TokensSaida,
    int Tentativas,
    bool Sucesso,
    DateTimeOffset Quando,
    string? ContextoEnviado);

/// <summary>
/// O plano de abordagem (#12).
///
/// A tela onde o vendedor e o protagonista. Nada aqui chama modelo: o criterio
/// que prova a tese e "funciona sem nunca clicar em sugerir", e sugerir ainda
/// nem existe.
/// </summary>
public static class EndpointsDoPlano
{
    public static void MapearPlano(this WebApplication app)
    {
        app.MapGet("/leads/{id:guid}/plano", PlanoDoLead).RequireAuthorization();
        app.MapPut("/leads/{id:guid}/plano/{bloco}", EscreverBloco).RequireAuthorization();
        app.MapPost("/leads/{id:guid}/plano/{bloco}/sugerir", Sugerir).RequireAuthorization();
        app.MapPost("/leads/{id:guid}/plano/{bloco}/aceitar", Aceitar).RequireAuthorization();
        app.MapDelete("/leads/{id:guid}/plano/{bloco}/sugestao", Descartar).RequireAuthorization();
        app.MapGet("/leads/{id:guid}/plano/{bloco}/porque", PorQue).RequireAuthorization();
    }

    /// <summary>
    /// O plano daquele lead, CRIADO na hora se ainda nao existe.
    ///
    /// 404 aqui seria errado: plano que nao existe e plano em branco, e a tela
    /// precisa abrir com quatro campos vazios para o vendedor escrever. Exigir
    /// um "criar plano" antes de escrever seria um clique que nao decide nada.
    /// </summary>
    public static async Task<IResult> PlanoDoLead(
        Guid id, CopilotoDbContext ctx, ClaimsPrincipal? quem = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var barrado = await Porteiro.Barrar(id, await UsuarioAtual.De(quem, ctx, ct), ctx, ct);
        if (barrado is not null) return barrado;

        var deal = await DealDoLead(ctx, id, ct);
        if (deal is null)
            return Results.NotFound(new { erro = "este lead ainda nao tem negocio aberto" });

        var plano = await ctx.Planos.FirstOrDefaultAsync(p => p.DealId == deal.Value, ct);

        if (plano is null)
        {
            plano = new PlanoDeAbordagem(Guid.NewGuid(), deal.Value, DateTimeOffset.UtcNow);
            ctx.Planos.Add(plano);
            await ctx.SaveChangesAsync(ct);
        }

        return Results.Ok(ParaTela(plano));
    }

    public static async Task<IResult> EscreverBloco(
        Guid id, string bloco, TextoDoBloco corpo, CopilotoDbContext ctx,
        ClaimsPrincipal? quem = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!Enum.TryParse<BlocoDoPlano>(bloco, ignoreCase: true, out var qual))
        {
            return Results.BadRequest(new
            {
                erro = $"'{bloco}' nao e um bloco do plano",
                blocos = Enum.GetNames<BlocoDoPlano>(),
            });
        }

        var barrado = await Porteiro.Barrar(id, await UsuarioAtual.De(quem, ctx, ct), ctx, ct);
        if (barrado is not null) return barrado;

        var deal = await DealDoLead(ctx, id, ct);
        if (deal is null)
            return Results.NotFound(new { erro = "este lead ainda nao tem negocio aberto" });

        var plano = await ctx.Planos.FirstOrDefaultAsync(p => p.DealId == deal.Value, ct);
        if (plano is null)
        {
            plano = new PlanoDeAbordagem(Guid.NewGuid(), deal.Value, DateTimeOffset.UtcNow);
            ctx.Planos.Add(plano);
        }

        plano.Escrever(qual, corpo?.Texto, DateTimeOffset.UtcNow);
        await ctx.SaveChangesAsync(ct);

        return Results.Ok(ParaTela(plano));
    }

    /// <summary>
    /// Pede uma sugestao para UM bloco (#189).
    ///
    /// A sugestao que nao vem devolve 200 com o plano intacto, e nao erro: o
    /// vendedor clicou num botao opcional, e transformar isso em tela vermelha
    /// ensinaria que a ferramenta esta quebrada quando so o modelo nao respondeu.
    /// Quem diz que nao veio e o campo `sugestao` continuar vazio.
    /// </summary>
    public static async Task<IResult> Sugerir(
        Guid id, string bloco, CopilotoDbContext ctx, AgenteDePlano agente,
        ClaimsPrincipal? quem = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(agente);

        var (erro, plano, qual) = await Abrir(id, bloco, ctx, quem, ct);
        if (erro is not null) return erro;

        var sugestao = await agente.Sugerir(qual, await Contexto(ctx, id, ct), ct);

        if (sugestao.Texto is { } texto) plano!.Sugerir(qual, texto, DateTimeOffset.UtcNow);

        // O custo entra no ledger AMARRADO ao negocio, TENHA OU NAO vindo
        // sugestao (#1, #2): o provedor cobra pelo token gasto antes de falhar.
        // Sem o Deal, a invocacao nao responde a unica pergunta que o ledger
        // existe para responder — quanto custou ESTA venda.
        if (sugestao.Medicao is not null)
        {
            var deal = await ctx.Deals.FirstOrDefaultAsync(d => d.Id == plano!.DealId, ct);
            var invocacao = new AiInvocation(
                Guid.NewGuid(), Tarefa.Plano, sugestao.Medicao,
                DateTimeOffset.UtcNow, plano!.DealId,
                procedencia: sugestao.Procedencia);

            deal?.RegistrarInvocacao(invocacao);

            // A sugestao aponta para a invocacao que a gerou (#51): sem esse
            // fio, o "por que" teria de adivinhar qual chamada produziu qual
            // frase, e erraria assim que houvesse duas no mesmo negocio.
            if (sugestao.Texto is not null) plano.Vincular(qual, invocacao.Id);
        }

        await ctx.SaveChangesAsync(ct);

        return Results.Ok(ParaTela(plano!));
    }

    public static async Task<IResult> Aceitar(
        Guid id, string bloco, CopilotoDbContext ctx,
        ClaimsPrincipal? quem = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var (erro, plano, qual) = await Abrir(id, bloco, ctx, quem, ct);
        if (erro is not null) return erro;

        // O aceite volta para a invocacao ANTES de o vinculo sumir (#3): e ele
        // que responde "qual modelo acerta mais".
        await RegistrarDecisao(ctx, plano![qual].InvocacaoId, aceita: true, ct);

        plano.Aceitar(qual, DateTimeOffset.UtcNow);
        await ctx.SaveChangesAsync(ct);

        return Results.Ok(ParaTela(plano));
    }

    public static async Task<IResult> Descartar(
        Guid id, string bloco, CopilotoDbContext ctx,
        ClaimsPrincipal? quem = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var (erro, plano, qual) = await Abrir(id, bloco, ctx, quem, ct);
        if (erro is not null) return erro;

        await RegistrarDecisao(ctx, plano![qual].InvocacaoId, aceita: false, ct);

        plano.Descartar(qual, DateTimeOffset.UtcNow);
        await ctx.SaveChangesAsync(ct);

        return Results.Ok(ParaTela(plano));
    }

    /// <summary>
    /// Marca na invocacao o que o vendedor decidiu sobre a sugestao dela.
    ///
    /// Silencioso quando nao ha vinculo: sugestao antiga, de antes da #51, nao
    /// tem invocacao para marcar — e recusar a acao por isso impediria o
    /// vendedor de descartar uma sugestao que esta na tela dele.
    /// </summary>
    private static async Task RegistrarDecisao(
        CopilotoDbContext ctx, Guid? invocacaoId, bool aceita, CancellationToken ct)
    {
        if (invocacaoId is null) return;

        var invocacao = await ctx.Invocacoes.FirstOrDefaultAsync(i => i.Id == invocacaoId.Value, ct);
        invocacao?.RegistrarAceite(aceita);
    }

    /// <summary>
    /// De onde saiu a sugestao pendente daquele bloco (#51).
    ///
    /// 404 quando nao ha sugestao ou quando ela nao tem vinculo — o que acontece
    /// com as que foram geradas antes desta issue. Inventar uma procedencia
    /// plausivel seria pior que dizer que nao ha: o botao existe justamente para
    /// nao pedir confianca cega.
    /// </summary>
    public static async Task<IResult> PorQue(
        Guid id, string bloco, CopilotoDbContext ctx,
        ClaimsPrincipal? quem = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var (erro, plano, qual) = await Abrir(id, bloco, ctx, quem, ct);
        if (erro is not null) return erro;

        var invocacaoId = plano![qual].InvocacaoId;
        if (invocacaoId is null)
            return Results.NotFound(new { erro = "esta sugestao nao tem procedencia registrada" });

        var invocacao = await ctx.Invocacoes.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invocacaoId.Value, ct);

        if (invocacao is null)
            return Results.NotFound(new { erro = "a chamada que gerou esta sugestao nao esta mais no ledger" });

        return Results.Ok(new PorQueNaTela(
            invocacao.Modelo,
            invocacao.VersaoDoPrompt,
            invocacao.CustoEmReais,
            invocacao.LatenciaMs,
            invocacao.TokensEntrada,
            invocacao.TokensSaida,
            invocacao.Tentativas,
            invocacao.Sucesso,
            invocacao.Quando,
            invocacao.ContextoEnviado));
    }

    /// <summary>
    /// O caminho comum das tres rotas de bloco: valida o nome, passa pelo
    /// porteiro e traz o plano — criando se for a primeira vez.
    /// </summary>
    private static async Task<(IResult? Erro, PlanoDeAbordagem? Plano, BlocoDoPlano Qual)> Abrir(
        Guid id, string bloco, CopilotoDbContext ctx, ClaimsPrincipal? quem, CancellationToken ct)
    {
        if (!Enum.TryParse<BlocoDoPlano>(bloco, ignoreCase: true, out var qual))
        {
            return (Results.BadRequest(new
            {
                erro = $"'{bloco}' nao e um bloco do plano",
                blocos = Enum.GetNames<BlocoDoPlano>(),
            }), null, default);
        }

        var barrado = await Porteiro.Barrar(id, await UsuarioAtual.De(quem, ctx, ct), ctx, ct);
        if (barrado is not null) return (barrado, null, qual);

        var deal = await DealDoLead(ctx, id, ct);
        if (deal is null)
            return (Results.NotFound(new { erro = "este lead ainda nao tem negocio aberto" }), null, qual);

        var plano = await ctx.Planos.FirstOrDefaultAsync(p => p.DealId == deal.Value, ct);
        if (plano is null)
        {
            plano = new PlanoDeAbordagem(Guid.NewGuid(), deal.Value, DateTimeOffset.UtcNow);
            ctx.Planos.Add(plano);
        }

        return (null, plano, qual);
    }

    /// <summary>
    /// O que o agente recebe sobre o cliente.
    ///
    /// Sai do DOSSIE, e nao da conversa crua: o dossie ja passou pelo escudo de
    /// PII e ja e' o resumo do que se leu. Mandar a conversa inteira aqui
    /// dobraria o custo e reabriria a porta que a #43 fechou.
    /// </summary>
    private static async Task<string> Contexto(
        CopilotoDbContext ctx, Guid leadId, CancellationToken ct)
    {
        var deal = await DealDoLead(ctx, leadId, ct);
        if (deal is null) return "Ainda nao ha leitura deste cliente.";

        var dossies = await ctx.Dossies.AsNoTracking()
            .Include(d => d.Objecoes)
            .Where(d => d.DealId == deal.Value).ToListAsync(ct);

        var dossie = dossies.MaxBy(d => d.GeradoEm);
        if (dossie is null) return "Ainda nao ha leitura deste cliente.";

        var partes = new List<string>();

        if (dossie.Termometro is { } t) partes.Add($"Temperatura: {t.Valor}, {t.Para}.");

        if (dossie.Objecoes.Count > 0)
        {
            partes.Add("Resistencias lidas: " + string.Join("; ",
                dossie.Objecoes.Select(o => $"{o.Tipo} — \"{o.TrechoCitado}\"")));
        }

        if (dossie.Lacunas.Count > 0)
            partes.Add("Ainda nao sabemos: " + string.Join("; ", dossie.Lacunas));

        return partes.Count == 0 ? "Ainda nao ha leitura deste cliente." : string.Join("\n", partes);
    }

    public static PlanoNaTela ParaTela(PlanoDeAbordagem plano)
    {
        ArgumentNullException.ThrowIfNull(plano);

        // A ordem dos blocos e a do enum, e ela e a ordem da CONVERSA: objetivo,
        // o que descobrir, o que vai travar, como termina. A tela nao reordena.
        return new PlanoNaTela(
            plano.Id,
            plano.DealId,
            plano.Versao,
            Enum.GetValues<BlocoDoPlano>()
                .Select(b => new BlocoNaTela(
                    b.ToString(), plano[b].Texto, plano[b].Sugestao,
                    plano[b].InvocacaoId is not null))
                .ToList());
    }

    /// <summary>
    /// O negocio mais recente daquele lead.
    ///
    /// Escolhido no CLIENTE porque o SQLite da suite nao aceita DateTimeOffset
    /// em ORDER BY (TECH-005). O filtro por lead acontece no banco, entao o que
    /// vem para a memoria sao os deals de UM lead.
    /// </summary>
    private static async Task<Guid?> DealDoLead(
        CopilotoDbContext ctx, Guid leadId, CancellationToken ct)
    {
        var doLead = await ctx.Deals.AsNoTracking()
            .Where(d => d.LeadId == leadId).ToListAsync(ct);

        return doLead.MaxBy(d => d.AbertoEm)?.Id;
    }
}
