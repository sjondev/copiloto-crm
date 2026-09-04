using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
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
        // A ordenacao acontece no cliente, e nao no banco: o SQLite da suite
        // NAO ordena DateTimeOffset ("SQLite does not support expressions of
        // type 'DateTimeOffset' in ORDER BY clauses"), e o Postgres de producao
        // ordena. Ordenar no servidor daria uma consulta que passa em producao e
        // quebra so no teste — e o desfecho provavel disso e alguem apagar o
        // teste. Aqui o custo e nenhum: negocio ABERTO por lead sao poucos.
        var abertos = await ctx.Deals.AsNoTracking()
            .Where(d => d.LeadId == leadId && d.FechadoEm == null)
            .ToListAsync(ct);

        var deal = abertos.OrderByDescending(d => d.AbertoEm).FirstOrDefault();

        var conversa = await ctx.Conversas.AsNoTracking()
            .Include(c => c.Mensagens)
            .FirstOrDefaultAsync(c => c.LeadId == leadId, ct);

        // O silencio sai do MAXIMO, e nao de `UltimaDoCliente`: aquela
        // propriedade usa `LastOrDefault`, que so vale enquanto a lista foi
        // montada por `Registrar`. Lida do banco, a colecao vem na ordem do
        // provedor — a PK e Guid — e "a ultima fala" pode ser a primeira. Ja
        // aconteceu aqui: 9 dias de silencio onde eram 8. O conserto e no
        // dominio e esta na #136; esta linha nao espera por ele.
        var ultima = conversa?.Mensagens
            .Where(m => m.Autor == Autor.Cliente)
            .MaxBy(m => m.EnviadaEm);

        var silencio = ultima is null ? (int?)null : (int)(agora - ultima.EnviadaEm).TotalDays;

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

        // O filtro por DATA tambem sai para o cliente, e nao so a ordem: no
        // SQLite da suite, `DateTimeOffset` nao e comparavel nem ordenavel em
        // SQL ("could not be translated"). O que fica no banco e o que ele sabe
        // fazer — negocio ABERTO —, e e esse recorte que segura o tamanho.
        //
        // Em Postgres o filtro rodaria no servidor. Escrever assim e o menor
        // denominador, e o preco esta pago enquanto o funil couber em memoria;
        // quando nao couber, a saida e mapear a coluna como DateTime UTC, e ai
        // os dois bancos filtram. Isso e mudanca de modelo, e nao cabia aqui.
        var abertos = await ctx.Deals.AsNoTracking()
            .Where(d => d.FechadoEm == null)
            .Join(ctx.Leads.AsNoTracking(), d => d.LeadId, l => l.Id, (d, l) => new { d, l.Nome })
            .ToListAsync(ct);

        // O teto entra DEPOIS de ordenar, senao "os 50 mais parados" viraria
        // "50 quaisquer entre os parados" — a resposta errada para a pergunta.
        return abertos
            .Where(x => x.d.EstagioDesde <= limite)
            .OrderBy(x => x.d.EstagioDesde)
            .Take(TetoDeResultados)
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
