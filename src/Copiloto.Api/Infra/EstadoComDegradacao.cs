namespace Copiloto.Api.Infra;

/// <summary>
/// Usa o estado distribuido enquanto ele responde, e cai para a memoria quando
/// ele para — registrando o risco (#72).
///
/// Redis fora do ar NAO pode derrubar a aplicacao: o vendedor perde o
/// atendimento inteiro por causa de um cache. Mas cair para memoria em silencio
/// tambem nao serve, porque enquanto isso durar a idempotencia vale so por
/// instancia (#67), o rate limit se multiplica pelas replicas (#71) e o
/// circuito de cada uma volta a ser o seu (#68).
///
/// As duas coisas juntas sao a decisao: degrada, e GRITA enquanto estiver
/// degradado. Falha silenciosa aqui aparece semanas depois como fatura maior,
/// e ninguem liga uma coisa a outra.
/// </summary>
public class EstadoComDegradacao : IDistributedState
{
    /// <summary>
    /// Quanto tempo esperar antes de tentar o primario de novo.
    ///
    /// Tentar a cada chamada transformaria cada operacao em uma espera de
    /// timeout — o remedio ficaria mais caro que a doenca. Tentar de vez em
    /// quando e o que faz a reconexao ser automatica sem ser cara.
    /// </summary>
    public static readonly TimeSpan EsperaParaTentarDeNovo = TimeSpan.FromSeconds(30);

    private readonly IDistributedState _primario;
    private readonly IDistributedState _reserva;
    private readonly ILogger<EstadoComDegradacao> _log;
    private readonly Func<DateTimeOffset> _agora;

    private DateTimeOffset? _degradadoDesde;
    private DateTimeOffset _proximaTentativa = DateTimeOffset.MinValue;

    public EstadoComDegradacao(
        IDistributedState primario, ILogger<EstadoComDegradacao> log,
        IDistributedState? reserva = null, Func<DateTimeOffset>? agora = null)
    {
        ArgumentNullException.ThrowIfNull(primario);

        _primario = primario;
        _reserva = reserva ?? new InMemoryState();
        _log = log;
        _agora = agora ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Esta rodando na reserva agora?</summary>
    public bool Degradado => _degradadoDesde is not null;

    public DateTimeOffset? DegradadoDesde => _degradadoDesde;

    /// <summary>O que o `/saude` mostra, e o que o log repete enquanto durar.</summary>
    public string RiscoAtual => Degradado
        ? "estado compartilhado indisponivel: idempotencia, rate limit e circuito "
          + "valem apenas nesta instancia. Com mais de uma replica, mensagem "
          + "reprocessada, limite multiplicado e provedor caido golpeado por cada uma."
        : "";

    public Task<bool> TentarMarcar(string chave, TimeSpan validade, CancellationToken ct) =>
        Tentar(e => e.TentarMarcar(chave, validade, ct));

    public Task<string?> Ler(string chave, CancellationToken ct) =>
        Tentar(e => e.Ler(chave, ct));

    public Task Gravar(string chave, string valor, TimeSpan validade, CancellationToken ct) =>
        Tentar<object?>(async e =>
        {
            await e.Gravar(chave, valor, validade, ct);
            return null;
        });

    public Task<long> Incrementar(string chave, TimeSpan janela, CancellationToken ct) =>
        Tentar(e => e.Incrementar(chave, janela, ct));

    private async Task<T> Tentar<T>(Func<IDistributedState, Task<T>> operacao)
    {
        if (Degradado && _agora() < _proximaTentativa)
            return await operacao(_reserva);

        try
        {
            var resultado = await operacao(_primario);
            if (Degradado) Recuperou();

            return resultado;
        }
        catch (Exception erro)
        {
            Degradou(erro);
            return await operacao(_reserva);
        }
    }

    private void Degradou(Exception erro)
    {
        _proximaTentativa = _agora() + EsperaParaTentarDeNovo;

        if (Degradado) return;   // ja avisado; nao repetir a cada chamada

        _degradadoDesde = _agora();
        _log.LogError(erro, "Estado compartilhado caiu; usando memoria. RISCO: {Risco}", RiscoAtual);
    }

    private void Recuperou()
    {
        // O que foi escrito na reserva durante a queda NAO e copiado de volta,
        // e isso e deliberado: sao chaves com validade curta, e reidratar
        // idempotencia velha reintroduziria decisoes que ja venceram. O sistema
        // volta ao normal com a memoria compartilhada limpa, que e o estado
        // conservador.
        _log.LogWarning(
            "Estado compartilhado voltou depois de {Tempo}. O que foi gravado durante a "
            + "queda ficou na memoria da instancia e nao foi promovido.",
            _agora() - _degradadoDesde);

        _degradadoDesde = null;
    }
}
