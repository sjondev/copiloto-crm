using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Leitura;

/// <summary>Quanto se gastou com um agente, ou com um modelo.</summary>
public record GastoPor(string Nome, decimal CustoEmReais, int Invocacoes);

/// <summary>Quantas sugestoes daquele agente ou modelo o vendedor usou.</summary>
public record AceitePor(string Nome, int Aceitas, int Ignoradas)
{
    /// <summary>
    /// Nulo quando ninguem decidiu ainda. Zero diria "ninguem usa", que e uma
    /// afirmacao sobre o modelo — e nao ha dado para faze-la.
    /// </summary>
    public decimal? Taxa => Aceitas + Ignoradas == 0
        ? null
        : Math.Round((decimal)Aceitas / (Aceitas + Ignoradas), 4);
}

/// <summary>
/// O painel que responde, com numero, se a IA se paga (#3).
/// </summary>
/// <param name="Ressalva">
/// A ressalva de vies vai NA RESPOSTA, e nao num rodape da tela (#6): ela
/// precisa chegar junto do numero em qualquer lugar que consuma esta rota.
/// </param>
public record MetricasNaTela(
    DateTimeOffset De,
    DateTimeOffset Ate,
    decimal CustoIaEmReais,
    int Invocacoes,
    int InvocacoesQueFalharam,
    IReadOnlyList<GastoPor> PorAgente,
    IReadOnlyList<GastoPor> PorModelo,
    int NegociosGanhos,
    decimal ReceitaGanha,
    int GanhosComIa,
    decimal ReceitaInfluenciada,
    decimal? ReceitaPorRealGasto,
    decimal? CustoPorNegocioGanho,
    IReadOnlyList<AceitePor> AceitePorModelo,
    string Ressalva);

/// <summary>
/// As contas do painel de ROI (#3).
///
/// Quase todo projeto de IA sabe dizer o que GASTOU. Quase nenhum sabe dizer o
/// que rendeu — e sem os dois lados o numero nao responde nada.
/// </summary>
public static class EndpointsDasMetricas
{
    /// <summary>
    /// A ressalva que acompanha o numero (#6).
    ///
    /// Escrita aqui, e nao na tela, para viajar junto da resposta: quem
    /// consumir esta rota por API, por planilha ou por outra tela recebe o
    /// numero e a ressalva no mesmo lugar. Numero de ROI solto vira slide, e
    /// slide com vies de selecao nao declarado e o jeito mais rapido de perder
    /// a confianca de quem entende do assunto.
    /// </summary>
    public const string RessalvaDeVies =
        "Isto NAO e um experimento controlado. O vendedor escolhe quando usar o "
        + "copiloto, e provavelmente usa nos negocios mais dificeis — ou nos mais "
        + "promissores. A razao abaixo mostra correlacao, nao causa: ela diz que os "
        + "negocios em que a IA foi usada renderam X por real gasto, e nao que a IA "
        + "fez esses negocios acontecerem.";

    public static void MapearMetricas(this WebApplication app)
    {
        // So o gestor (#176). A conta do produto inteiro nao e informacao de
        // carteira, e a politica ja existia sem ser usada em rota nenhuma.
        app.MapGet("/metricas", Painel).RequireAuthorization("gestor");
    }

    public static async Task<IResult> Painel(
        CopilotoDbContext ctx, DateTimeOffset? de, DateTimeOffset? ate, Guid? vendedor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return Results.Ok(await Montar(
            ctx,
            de ?? DateTimeOffset.UtcNow.AddDays(-30),
            ate ?? DateTimeOffset.UtcNow,
            vendedor,
            ct));
    }

    /// <summary>
    /// As contas, AGREGADAS NO BANCO.
    ///
    /// O criterio da issue e carregar com 10 mil invocacoes em menos de um
    /// segundo, e trazer dez mil linhas para somar em memoria faz o tempo
    /// crescer com o uso — justamente na tela que so fica interessante depois
    /// de meses de uso.
    /// </summary>
    public static async Task<MetricasNaTela> Montar(
        CopilotoDbContext ctx, DateTimeOffset de, DateTimeOffset ate,
        Guid? vendedor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var dealsDoRecorte = ctx.Deals.AsNoTracking().Where(d =>
            vendedor == null ||
            ctx.Leads.Any(l => l.Id == d.LeadId && l.VendedorId == vendedor));

        var invocacoes = ctx.Invocacoes.AsNoTracking()
            .Where(i => i.Quando >= de && i.Quando <= ate)
            .Where(i => i.DealId == null || dealsDoRecorte.Any(d => d.Id == i.DealId));

        var custo = await invocacoes.SumAsync(i => i.CustoEmReais, ct);
        var quantas = await invocacoes.CountAsync(ct);
        var falharam = await invocacoes.CountAsync(i => !i.Sucesso, ct);

        var porAgente = await invocacoes
            .GroupBy(i => i.Agente)
            .Select(g => new GastoPor(g.Key.ToString(), g.Sum(i => i.CustoEmReais), g.Count()))
            .ToListAsync(ct);

        var porModelo = await invocacoes
            .GroupBy(i => i.Modelo)
            .Select(g => new GastoPor(g.Key, g.Sum(i => i.CustoEmReais), g.Count()))
            .ToListAsync(ct);

        var aceitePorModelo = await invocacoes
            .Where(i => i.SugestaoAceita != null)
            .GroupBy(i => i.Modelo)
            .Select(g => new AceitePor(
                g.Key,
                g.Count(i => i.SugestaoAceita == true),
                g.Count(i => i.SugestaoAceita == false)))
            .ToListAsync(ct);

        // Ganhos NO PERIODO, pela data de fechamento: negocio ganho em janeiro
        // nao entra na conta de fevereiro so porque a IA foi usada nele em
        // fevereiro.
        var ganhos = dealsDoRecorte.Where(d =>
            d.Estagio == Estagio.Ganho && d.FechadoEm >= de && d.FechadoEm <= ate);

        var quantosGanhos = await ganhos.CountAsync(ct);
        var receita = await ganhos.SumAsync(d => d.ValorEmReais ?? 0m, ct);

        // INFLUENCIADA: so os ganhos em que houve chamada de IA. Somar todos
        // creditaria ao copiloto a venda que aconteceu sem ele.
        var comIa = ganhos.Where(d => ctx.Invocacoes.Any(i => i.DealId == d.Id));
        var quantosComIa = await comIa.CountAsync(ct);
        var receitaInfluenciada = await comIa.SumAsync(d => d.ValorEmReais ?? 0m, ct);

        return new MetricasNaTela(
            de, ate, custo, quantas, falharam, porAgente, porModelo,
            quantosGanhos, receita, quantosComIa, receitaInfluenciada,
            custo == 0 ? null : Math.Round(receitaInfluenciada / custo, 2),
            quantosGanhos == 0 ? null : Math.Round(custo / quantosGanhos, 4),
            aceitePorModelo,
            RessalvaDeVies);
    }
}
