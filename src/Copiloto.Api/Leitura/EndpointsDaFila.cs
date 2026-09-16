using Copiloto.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Leitura;

/// <summary>Uma linha da fila: o lead e o motivo de ele estar onde esta.</summary>
public record LeadNaFila(
    Guid LeadId,
    string? Nome,
    string Telefone,
    string? Estagio,
    string? Temperatura,
    string? Objecao,
    string? Alerta,
    string? Motivo,
    DateTimeOffset? UltimaFala,
    int? DiasEmSilencio,
    bool AnaliseSuspensa);

/// <summary>
/// A fila de atendimento (#174).
///
/// A porta de entrada do CRM era colar um Guid no cabecalho, e vendedor nao
/// decora Guid. Mas isto NAO e uma lista de contatos: a pergunta que ele faz ao
/// abrir o sistema e "quem eu atendo agora?", e a ordem existe para responder
/// isso. Lista alfabetica de trezentos leads responde "todos", que e o mesmo que
/// nao responder.
///
/// Nada aqui e calculo novo: silencio e negocio parado saem do Vigia (#53),
/// temperatura e objecao saem do dossie (#13, #9). A fila so junta e ordena.
/// </summary>
public static class EndpointsDaFila
{
    public static void MapearFila(this WebApplication app)
    {
        app.MapGet("/leads", FilaDeAtendimento).RequireAuthorization();
    }

    public static async Task<IResult> FilaDeAtendimento(
        CopilotoDbContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return Results.Ok(await Montar(ctx, DateTimeOffset.UtcNow, ct));
    }

    /// <summary>
    /// Quatro consultas, e nao uma por lead.
    ///
    /// CARREGA TUDO, e isso e limite declarado e nao descuido: numa torrefacao
    /// com algumas centenas de leads, quatro consultas sem filtro sao mais
    /// baratas que a paginacao que ninguem pediu. O gatilho para mudar e
    /// mensuravel — quando a #54 mostrar esta rota no p99, entra paginacao por
    /// vendedor, que e o recorte natural (#176).
    ///
    /// A ordenacao acontece em MEMORIA porque o SQLite da suite nao aceita
    /// DateTimeOffset em ORDER BY (TECH-005), a mesma razao ja registrada no
    /// DealAberto. Ordenar no banco aqui quebraria a suite offline.
    /// </summary>
    public static async Task<IReadOnlyList<LeadNaFila>> Montar(
        CopilotoDbContext ctx, DateTimeOffset agora, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var leads = await ctx.Leads.AsNoTracking().ToListAsync(ct);
        var deals = await ctx.Deals.AsNoTracking().ToListAsync(ct);
        var dossies = await ctx.Dossies.AsNoTracking().ToListAsync(ct);
        var conversas = await ctx.Conversas.AsNoTracking()
            .Include(c => c.Mensagens).ToListAsync(ct);

        var dealPorLead = deals.GroupBy(d => d.LeadId)
            .ToDictionary(g => g.Key, g => g.MaxBy(d => d.AbertoEm)!);

        var conversaPorLead = conversas.GroupBy(c => c.LeadId)
            .ToDictionary(g => g.Key, g => g.First());

        var dossiePorDeal = dossies.GroupBy(d => d.DealId)
            .ToDictionary(g => g.Key, g => g.MaxBy(d => d.GeradoEm)!);

        var linhas = leads.Select(lead =>
        {
            dealPorLead.TryGetValue(lead.Id, out var deal);
            conversaPorLead.TryGetValue(lead.Id, out var conversa);

            Dominio.Dossies.Dossie? dossie = null;
            if (deal is not null) dossiePorDeal.TryGetValue(deal.Id, out dossie);

            var alerta = deal is null
                ? null
                : Dominio.Vigia.Vigia.Varrer(deal, conversa, agora).OrderBy(a => Gravidade(a.Motivo)).FirstOrDefault();

            var ultimaDoCliente = conversa?.UltimaDoCliente;

            return new LeadNaFila(
                lead.Id,
                lead.Nome,
                lead.Telefone,
                deal?.Estagio.ToString(),
                dossie?.Termometro is { } t ? $"{t.Valor} e {t.Para}".ToLowerInvariant() : null,
                PrimeiraObjecaoUtil(dossie),
                alerta?.Motivo.ToString(),
                alerta?.Texto,
                ultimaDoCliente?.EnviadaEm,
                ultimaDoCliente is null ? null : (int)(agora - ultimaDoCliente.EnviadaEm).TotalDays,

                // A analise suspensa aparece na lista, e nao so na tela do lead:
                // o vendedor precisa saber ANTES de abrir que aquele dossie nao
                // vai atualizar, senao ele espera uma leitura que nao vem (#81).
                lead.AnaliseDeIaSuspensa);
        });

        // Quem tem alerta primeiro, o mais grave na frente; dentro do mesmo
        // motivo, quem esta esperando ha mais tempo. Quem nao tem alerta vem
        // depois, pela fala mais recente — e conversa quente no topo do resto.
        return linhas
            .OrderBy(l => l.Alerta is null ? 1 : 0)
            .ThenBy(l => l.Alerta is null ? int.MaxValue : GravidadePorNome(l.Alerta))
            .ThenByDescending(l => l.DiasEmSilencio ?? 0)
            .ThenByDescending(l => l.UltimaFala ?? DateTimeOffset.MinValue)
            .ToList();
    }

    /// <summary>
    /// A objecao que vale UMA PALAVRA na lista.
    ///
    /// `NaoClassificada` fica de fora: na tela do lead ela e informacao — ha
    /// resistencia e nao se sabe de que tipo, com a fala citada ao lado. Numa
    /// lista, reduzida a um rotulo solto, ela vira "naoclassificada" pendurado
    /// no nome do cliente: ocupa espaco, nao diz nada, e ensina o vendedor a
    /// parar de ler os rotulos que dizem.
    ///
    /// Preferir a primeira classificada a mostrar a primeira qualquer — a
    /// resistencia com nome e a que muda o que ele vai perguntar.
    /// </summary>
    private static string? PrimeiraObjecaoUtil(Dominio.Dossies.Dossie? dossie) =>
        dossie?.Objecoes
            .FirstOrDefault(o => o.Tipo != Dominio.Dossies.TipoDeObjecao.NaoClassificada)
            ?.Tipo.ToString();

    /// <summary>
    /// A ordem de urgencia entre os motivos.
    ///
    /// Silencio na frente porque e o unico que so PIORA sozinho: proposta
    /// envelhecendo e negocio parado continuam onde estao, e o cliente calado
    /// esta sendo atendido por outro fornecedor enquanto ninguem liga.
    /// </summary>
    private static int Gravidade(Dominio.Vigia.MotivoDeAlerta motivo) => motivo switch
    {
        Dominio.Vigia.MotivoDeAlerta.ClienteEmSilencio => 0,
        Dominio.Vigia.MotivoDeAlerta.PropostaEnvelhecendo => 1,
        Dominio.Vigia.MotivoDeAlerta.NegocioParado => 2,
        _ => 3,
    };

    private static int GravidadePorNome(string motivo) =>
        Enum.TryParse<Dominio.Vigia.MotivoDeAlerta>(motivo, out var m) ? Gravidade(m) : 3;
}
