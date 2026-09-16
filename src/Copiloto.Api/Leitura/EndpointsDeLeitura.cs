using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Leitura;

/// <summary>Um sinal como a tela precisa dele: com a frase, sempre.</summary>
public record SinalNaTela(string Tipo, string Descricao, string TrechoCitado, Guid MensagemId);

/// <param name="PorComportamento">
/// Detectada pela forma da conversa, nao pelo que foi dito. A tela marca essas
/// de outro jeito: e a leitura que o vendedor NAO faria sozinho, e e o que ele
/// precisa aprender a confiar.
/// </param>
public record ObjecaoNaTela(
    string Tipo, string Descricao, string TrechoCitado, Guid MensagemId, bool PorComportamento);

/// <summary>O dossie que a tela dividida mostra (#50).</summary>
public record DossieNaTela(
    Guid Id,
    Guid LeadId,
    DateTimeOffset GeradoEm,
    string? Temperatura,
    string? Direcao,
    string? Resumo,
    IReadOnlyList<SinalNaTela> SinaisDeCompra,
    IReadOnlyList<SinalNaTela> SinaisDeFuga,
    IReadOnlyList<ObjecaoNaTela> Objecoes,
    IReadOnlyList<string> Lacunas);

/// <summary>Uma fala da conversa.</summary>
public record FalaNaTela(
    Guid Id, string Autor, string Texto, DateTimeOffset EnviadaEm, string? Midia);

/// <summary>
/// As rotas que a tela chama (#161).
///
/// Devolvem DTO e nao entidade: o dominio serializado vazaria campo que a tela
/// nao usa e prenderia o formato do JSON a forma da tabela — mudar coluna
/// quebraria o front sem ninguem prever.
///
/// Sinais ja separados por TIPO no payload. A tela mostra compra e fuga em
/// colunas diferentes, e deixar a divisao para o front faria cada tela futura
/// reimplementar a mesma regra — e uma delas errar.
/// </summary>
public static class EndpointsDeLeitura
{
    public static void MapearLeitura(this WebApplication app)
    {
        app.MapGet("/leads/{id:guid}/dossie", DossieDoLead);
        app.MapGet("/leads/{id:guid}/conversa", ConversaDoLead);
    }

    /// <summary>
    /// A leitura mais recente daquele lead.
    ///
    /// Metodo nomeado e nao lambda dentro do MapGet: assim a suite chama a regra
    /// direto, sem subir a aplicacao e sem pacote de teste de integracao. Rota
    /// que so pode ser exercitada por HTTP acaba sendo rota sem teste.
    /// </summary>
    public static async Task<IResult> DossieDoLead(Guid id, CopilotoDbContext ctx)
    {
        if (!await ctx.Leads.AnyAsync(l => l.Id == id))
            return Results.NotFound(new { erro = "lead nao encontrado" });

        // O dossie de um lead e o do negocio dele, e o MAIS RECENTE: leitura
        // antiga na tela e pior que tela vazia, porque parece atual.
        //
        // A escolha do mais recente e no CLIENTE porque o SQLite da suite nao
        // aceita DateTimeOffset em ORDER BY (TECH-005, #56), e um teste que so
        // roda contra Postgres nao roda. O filtro por lead ja acontece no banco,
        // entao o que vem para a memoria e o historico de UM negocio.
        var doLead = await ctx.Dossies
            .Include(d => d.Sinais)
            .Include(d => d.Objecoes)
            .Where(d => ctx.Deals.Any(deal => deal.Id == d.DealId && deal.LeadId == id))
            .ToListAsync();

        var dossie = doLead.MaxBy(d => d.GeradoEm);

        // 404 e nao um dossie vazio: vazio pareceria leitura feita que nao achou
        // nada, e a tela mostraria "nenhum sinal" com cara de certeza.
        return dossie is null
            ? Results.NotFound(new { erro = "ainda nao ha leitura para este lead" })
            : Results.Ok(Montar(dossie, id));
    }

    public static async Task<IResult> ConversaDoLead(Guid id, CopilotoDbContext ctx)
    {
        if (!await ctx.Leads.AnyAsync(l => l.Id == id))
            return Results.NotFound(new { erro = "lead nao encontrado" });

        var conversa = await ctx.Conversas
            .Include(c => c.Mensagens)
            .FirstOrDefaultAsync(c => c.LeadId == id);

        // Conversa vazia E uma resposta valida aqui, diferente do dossie: o lead
        // existe e ainda nao falou, e a tela mostra isso sem mentir.
        return Results.Ok(conversa is null
            ? []
            : conversa.Mensagens.OrderBy(m => m.EnviadaEm).Select(Montar).ToArray());
    }

    /// <summary>
    /// O dossie no formato da tela.
    ///
    /// Publico porque o tempo real (#50) empurra exatamente o mesmo payload que
    /// o GET devolve. Dois formatos para o mesmo dado dariam duas telas
    /// possiveis para o mesmo dossie, e a diferenca so apareceria quando um dos
    /// caminhos mudasse — provavelmente em producao.
    /// </summary>
    public static DossieNaTela ParaTela(Dossie dossie, Guid leadId) => Montar(dossie, leadId);

    private static DossieNaTela Montar(Dossie dossie, Guid leadId) =>
        new(dossie.Id,
            leadId,
            dossie.GeradoEm,
            dossie.Termometro?.Valor.ToString(),
            dossie.Termometro?.Para.ToString(),
            dossie.Termometro?.Resumo,
            Montar(dossie.SinaisDe(TipoDeSinal.Compra)),
            Montar(dossie.SinaisDe(TipoDeSinal.Fuga)),
            dossie.Objecoes.Select(o => new ObjecaoNaTela(
                o.Tipo.ToString(), o.Descricao, o.TrechoCitado, o.MensagemId, o.PorComportamento)).ToList(),
            dossie.Lacunas);

    private static IReadOnlyList<SinalNaTela> Montar(IEnumerable<Sinal> sinais) =>
        sinais.Select(s => new SinalNaTela(
            s.Tipo.ToString(), s.Descricao, s.TrechoCitado, s.MensagemId)).ToList();

    private static FalaNaTela Montar(Mensagem m) =>
        new(m.Id, m.Autor.ToString(), m.Texto, m.EnviadaEm, m.Midia?.Tipo.ToString());
}
