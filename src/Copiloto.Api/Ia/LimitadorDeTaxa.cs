using Copiloto.Api.Infra;

namespace Copiloto.Api.Ia;

/// <summary>
/// Rate limit por usuario, valendo para todas as instancias (#41, #71).
///
/// Com o contador dentro do processo, o limite vira "limite x numero de
/// replicas" — e limite que se multiplica sozinho nao e limite, e sugestao.
/// Aqui ele protege dinheiro, nao CPU: cada chamada que passa e uma invocacao
/// de modelo paga.
/// </summary>
public class LimitadorDeTaxa
{
    private readonly IDistributedState _estado;
    private readonly int _limite;
    private readonly TimeSpan _janela;

    public LimitadorDeTaxa(IDistributedState estado, int limite, TimeSpan janela)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (limite <= 0)
            throw new ArgumentOutOfRangeException(nameof(limite),
                "Limite zero bloquearia tudo, e limite negativo nao quer dizer nada. "
                + "Para desligar o limitador, nao o registre.");

        _estado = estado;
        _limite = limite;
        _janela = janela;
    }

    /// <summary>
    /// Consome uma unidade e diz se a chamada pode seguir.
    ///
    /// Contar SEMPRE, inclusive quando ja passou do limite, e deliberado: quem
    /// insiste alem do teto e exatamente quem se quer enxergar, e um contador
    /// que para de subir apaga o unico sinal de que houve insistencia.
    /// </summary>
    public async Task<bool> Permite(Guid usuarioId, CancellationToken ct)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException(
                "Rate limit sem usuario cairia todo mundo no mesmo balde, e o "
                + "primeiro vendedor movimentado bloquearia a empresa inteira.",
                nameof(usuarioId));

        var usadas = await _estado.Incrementar($"rate:usuario:{usuarioId}", _janela, ct);
        return usadas <= _limite;
    }
}
