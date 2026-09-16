namespace Copiloto.Api.Seguranca;

/// <summary>
/// Conta com que frequencia dado sensivel aparece, e avisa o gestor (#82).
///
/// O alerta NAO e sobre o cliente que falou: e sobre o PROCESSO. Dado sensivel
/// chegando toda semana quase nunca e coincidencia — costuma ser um formulario
/// que pergunta restricao alimentar, um vendedor que puxa assunto de saude, uma
/// campanha que convida o cliente a contar da vida. Nenhuma dessas coisas
/// aparece num log de erro, e todas mudam a base legal do que a empresa esta
/// coletando.
/// </summary>
public class IncidenciaDeDadoSensivel
{
    /// <summary>
    /// CONVERSAS distintas na janela, e nao mencoes.
    ///
    /// Um cliente falante que cita o refluxo cinco vezes na mesma conversa e um
    /// cliente falante. Cinco conversas diferentes com dado sensivel e um
    /// padrao de coleta — e so o segundo justifica incomodar o gestor.
    /// </summary>
    public const int ConversasQueJaSaoPadrao = 5;

    public static readonly TimeSpan Janela = TimeSpan.FromDays(7);

    private readonly Dictionary<Guid, DateTimeOffset> _ultimaPorConversa = new();

    public void Registrar(Guid conversaId, DateTimeOffset quando)
    {
        if (conversaId == Guid.Empty)
            throw new ArgumentException("Incidencia sem conversa.", nameof(conversaId));

        _ultimaPorConversa[conversaId] = quando;
    }

    public int ConversasNaJanela(DateTimeOffset agora) =>
        _ultimaPorConversa.Count(o => agora - o.Value < Janela);

    public bool DeveAlertarGestor(DateTimeOffset agora) =>
        ConversasNaJanela(agora) >= ConversasQueJaSaoPadrao;

    /// <summary>
    /// O texto do alerta. Diz o que fazer, e nao so que aconteceu: aviso que
    /// nao aponta para uma acao vira aviso que se aprende a fechar.
    /// </summary>
    public string Alerta(DateTimeOffset agora) =>
        $"Dado sensível apareceu em {ConversasNaJanela(agora)} conversas nos últimos "
        + $"{Janela.Days} dias. Isso costuma indicar coleta pelo processo — formulário, "
        + "roteiro de abordagem ou campanha pedindo informação que a empresa não precisa. "
        + "Vale revisar por onde essa pergunta está sendo feita.";
}
