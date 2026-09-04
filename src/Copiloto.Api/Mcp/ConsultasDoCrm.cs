using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Mcp;

/// <summary>O que o CRM responde sobre um lead, sem a analise de IA.</summary>
public record FichaDoLead(
    Guid LeadId, string? Nome, string Telefone,
    IReadOnlyDictionary<string, string> Anotado,
    IReadOnlyList<string> Lacunas,
    string? Estagio, int? DiasNoEstagio, int? DiasSemFalarComOCliente);

/// <summary>Um negocio parado, com o dado que sustenta o "parado".</summary>
public record NegocioParado(Guid DealId, Guid LeadId, string? Nome, string Estagio, int DiasParado);

public record LeadEncontrado(Guid LeadId, string? Nome, string Telefone);

public record FalaDaConversa(string Autor, string Texto, DateTimeOffset EnviadaEm);

/// <summary>
/// As consultas de leitura do CRM (#56).
///
/// Ficam FORA da classe de ferramentas MCP de proposito: o que responde a
/// pergunta e codigo comum, testavel sem subir servidor nem cliente MCP. A
/// camada MCP e so a porta — e porta que carrega regra vira regra que so o
/// protocolo enxerga.
/// </summary>
public static class ConsultasDoCrm
{
    public const int TetoDeResultados = 50;

    public static async Task<IReadOnlyList<LeadEncontrado>> BuscarLead(
        CopilotoDbContext ctx, string termo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(termo)) return [];

        var porTelefone = Telefone.Normalizar(termo);
        var busca = termo.Trim();

        var leads = await ctx.Leads.AsNoTracking()
            .Where(l => (porTelefone != null && l.Telefone == porTelefone.ToString())
                        || (l.Nome != null && EF.Functions.Like(l.Nome, $"%{busca}%")))
            .Take(TetoDeResultados)
            .ToListAsync(ct);

        return leads.Select(l => new LeadEncontrado(l.Id, l.Nome, l.Telefone)).ToList();
    }

    public static async Task<FichaDoLead?> ObterFicha(
        CopilotoDbContext ctx, Guid leadId, DateTimeOffset agora, CancellationToken ct)
    {
        var lead = await ctx.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return null;

        var ficha = await ctx.Fichas.AsNoTracking().FirstOrDefaultAsync(f => f.LeadId == leadId, ct);
        var deal = await ctx.Deals.AsNoTracking()
            .Where(d => d.LeadId == leadId && d.FechadoEm == null)
            .OrderByDescending(d => d.AbertoEm)
            .FirstOrDefaultAsync(ct);

        var conversa = await ctx.Conversas.AsNoTracking()
            .Include(c => c.Mensagens)
            .FirstOrDefaultAsync(c => c.LeadId == leadId, ct);

        var silencio = conversa?.UltimaDoCliente is { } ultima
            ? (int)(agora - ultima.EnviadaEm).TotalDays
            : (int?)null;

        // `Anotado` e um dicionario chato de proposito: nesta base a ficha ainda
        // guarda texto simples. Quando a #88 entrar, cada linha passa a dizer se
        // e FATO ou IMPRESSAO, com a fonte — e a ferramenta ganha a distincao
        // sem mudar de forma, porque quem chama ja recebe rotulo e valor.
        return new FichaDoLead(
            lead.Id, lead.Nome, lead.Telefone,
            ficha?.Preenchidos.ToDictionary(a => a.Key, a => a.Value)
                ?? new Dictionary<string, string>(),
            ficha?.Lacunas() ?? [],
            deal?.Estagio.ToString(),
            deal is null ? null : (int)(agora - deal.EstagioDesde).TotalDays,
            silencio);
    }

    public static async Task<IReadOnlyList<NegocioParado>> ListarNegociosParados(
        CopilotoDbContext ctx, int dias, DateTimeOffset agora, CancellationToken ct)
    {
        if (dias < 0) dias = 0;
        var limite = agora.AddDays(-dias);

        var parados = await ctx.Deals.AsNoTracking()
            .Where(d => d.FechadoEm == null && d.EstagioDesde <= limite)
            .OrderBy(d => d.EstagioDesde)
            .Take(TetoDeResultados)
            .Join(ctx.Leads.AsNoTracking(), d => d.LeadId, l => l.Id, (d, l) => new { d, l.Nome })
            .ToListAsync(ct);

        return parados
            .Select(x => new NegocioParado(
                x.d.Id, x.d.LeadId, x.Nome, x.d.Estagio.ToString(),
                (int)(agora - x.d.EstagioDesde).TotalDays))
            .ToList();
    }

    public static async Task<IReadOnlyList<FalaDaConversa>> HistoricoDaConversa(
        CopilotoDbContext ctx, Guid leadId, int limite, CancellationToken ct)
    {
        // Teto sempre, mesmo quando quem chama pede mais: do outro lado ha um
        // agente que nao sente a conta crescer, e conversa inteira num contexto
        // e token gasto antes de alguem decidir se precisava.
        limite = Math.Clamp(limite <= 0 ? 20 : limite, 1, TetoDeResultados);

        var conversa = await ctx.Conversas.AsNoTracking()
            .Include(c => c.Mensagens)
            .FirstOrDefaultAsync(c => c.LeadId == leadId, ct);

        if (conversa is null) return [];

        return conversa.Mensagens
            .OrderByDescending(m => m.EnviadaEm)
            .Take(limite)
            .OrderBy(m => m.EnviadaEm)
            .Select(m => new FalaDaConversa(m.Autor.ToString(), m.Texto, m.EnviadaEm))
            .ToList();
    }
}
