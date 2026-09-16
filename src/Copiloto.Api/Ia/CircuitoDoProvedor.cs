using System.Text.Json;
using Copiloto.Api.Infra;

namespace Copiloto.Api.Ia;

/// <summary>Em que ponto o circuito de um provedor esta.</summary>
public enum EstadoDoCircuito
{
    /// <summary>Chamando normalmente.</summary>
    Fechado = 0,

    /// <summary>Provedor fora do ar: ninguem chama.</summary>
    Aberto = 1,

    /// <summary>Hora de testar — e UMA instancia testa, nao todas.</summary>
    MeioAberto = 2,
}

/// <summary>
/// O circuit breaker por provedor, com o estado fora do processo (#38, #68).
///
/// Com o estado em memoria e tres instancias existem TRES circuitos
/// independentes: cada uma precisa falhar N vezes por conta propria antes de
/// proteger. Um provedor fora do ar leva 3N requisicoes em vez de N — e cada
/// uma delas e tempo de espera na frente do vendedor.
///
/// O detalhe que separa quem ja implementou disso de quem leu sobre esta no
/// meio-aberto: se todas as instancias testarem o provedor em recuperacao ao
/// mesmo tempo, o teste vira exatamente a avalanche que o breaker existe para
/// evitar. Aqui a sonda e disputada — quem marca primeiro testa, e os demais
/// seguem barrados ate haver resposta.
/// </summary>
public class CircuitoDoProvedor
{
    /// <summary>O que o estado compartilhado guarda sobre um provedor.</summary>
    private record Registro(DateTimeOffset? AbertoAte);

    private static readonly JsonSerializerOptions Json = new();

    public const int FalhasQueAbrem = 3;

    /// <summary>Quanto tempo ninguem chama antes de valer a pena testar.</summary>
    public static readonly TimeSpan EsperaAntesDeTestar = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Falhas contam dentro de uma JANELA. Sem ela, tres falhas espalhadas por
    /// uma semana abririam o circuito de um provedor que esta de pe.
    /// </summary>
    public static readonly TimeSpan JanelaDeContagem = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Quanto tempo a sonda fica reservada. Curto de proposito: se a instancia
    /// que ganhou morrer antes de responder, o circuito nao pode ficar preso em
    /// meio-aberto para sempre — outra tenta logo depois.
    /// </summary>
    public static readonly TimeSpan ReservaDaSonda = TimeSpan.FromSeconds(10);

    private readonly IDistributedState _estado;
    private readonly Func<DateTimeOffset> _agora;

    public CircuitoDoProvedor(IDistributedState estado, Func<DateTimeOffset>? agora = null)
    {
        ArgumentNullException.ThrowIfNull(estado);

        _estado = estado;
        _agora = agora ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<EstadoDoCircuito> Estado(string provedor, CancellationToken ct)
    {
        var registro = await Registrado(provedor, ct);

        if (registro?.AbertoAte is null) return EstadoDoCircuito.Fechado;

        return registro.AbertoAte > _agora()
            ? EstadoDoCircuito.Aberto
            : EstadoDoCircuito.MeioAberto;
    }

    /// <summary>
    /// Se ESTA chamada pode seguir para o provedor.
    ///
    /// No meio-aberto a resposta e verdadeira para UMA instancia so — a que
    /// ganhar a sonda. As outras continuam barradas ate o resultado, senao o
    /// momento da recuperacao seria o momento de maior carga.
    /// </summary>
    public async Task<bool> PodeChamar(string provedor, CancellationToken ct) =>
        await Estado(provedor, ct) switch
        {
            EstadoDoCircuito.Fechado => true,
            EstadoDoCircuito.Aberto => false,
            _ => await _estado.TentarMarcar(Sonda(provedor), ReservaDaSonda, ct),
        };

    public async Task RegistrarFalha(string provedor, CancellationToken ct)
    {
        var falhas = await _estado.Incrementar(Contador(provedor), JanelaDeContagem, ct);
        if (falhas < FalhasQueAbrem) return;

        var abertoAte = _agora() + EsperaAntesDeTestar;

        // A validade da chave passa do fim da espera: enquanto ela existir com
        // AbertoAte vencido, o circuito esta em MEIO-ABERTO. Se ela sumisse
        // junto com a espera, o estado voltaria a "fechado" para todo mundo ao
        // mesmo tempo — e a avalanche aconteceria por expiracao de chave.
        await Gravar(provedor, new Registro(abertoAte), EsperaAntesDeTestar * 4, ct);
    }

    /// <summary>
    /// Provedor respondeu: fecha o circuito e zera a contagem.
    ///
    /// Zerar importa mais do que parece — sem isso, tres falhas espalhadas com
    /// sucessos no meio abririam o circuito de um provedor saudavel, e o
    /// sintoma seria "o sistema escolhe o modelo caro as vezes".
    /// </summary>
    public async Task RegistrarSucesso(string provedor, CancellationToken ct)
    {
        await _estado.Gravar(Contador(provedor), "0", JanelaDeContagem, ct);
        await Gravar(provedor, new Registro(AbertoAte: null), JanelaDeContagem, ct);
    }

    /// <summary>
    /// Os provedores que o router deve descartar agora.
    ///
    /// Existe porque o <c>RoteadorDeModelo</c> e regra de dominio e recebe uma
    /// funcao SINCRONA: quem vai chamar o modelo carrega este retrato — uma
    /// leitura por provedor da tabela — e entrega ao router. O router decide;
    /// ele nao consulta infraestrutura.
    /// </summary>
    public async Task<IReadOnlySet<string>> Indisponiveis(
        IEnumerable<string> provedores, CancellationToken ct)
    {
        var fora = new HashSet<string>();
        foreach (var provedor in provedores.Distinct())
            if (await Estado(provedor, ct) == EstadoDoCircuito.Aberto)
                fora.Add(provedor);

        return fora;
    }

    private async Task<Registro?> Registrado(string provedor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provedor))
            throw new ArgumentException(
                "Circuito sem provedor: a unidade do breaker e o provedor, e uma "
                + "chave comum derrubaria todos juntos quando um caisse.",
                nameof(provedor));

        var cru = await _estado.Ler(Chave(provedor), ct);
        return cru is null ? null : JsonSerializer.Deserialize<Registro>(cru, Json);
    }

    private Task Gravar(string provedor, Registro registro, TimeSpan validade, CancellationToken ct) =>
        _estado.Gravar(Chave(provedor), JsonSerializer.Serialize(registro, Json), validade, ct);

    private static string Chave(string provedor) => $"breaker:{provedor.Trim()}";
    private static string Contador(string provedor) => $"breaker:{provedor.Trim()}:falhas";
    private static string Sonda(string provedor) => $"breaker:{provedor.Trim()}:sonda";
}
