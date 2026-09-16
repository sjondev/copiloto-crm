using Copiloto.Dominio.Conversas;

namespace Copiloto.Dominio.Dossies;

/// <summary>
/// De onde vem a resistencia. Classificar importa porque cada uma pede coisa
/// diferente do vendedor: preco pede justificar valor, autoridade pede chegar a
/// quem decide, e tratar uma como a outra queima a conversa.
/// </summary>
public enum TipoDeObjecao
{
    Preco = 0,
    Timing = 1,
    Autoridade = 2,
    Concorrente = 3,
    Necessidade = 4,
    Confianca = 5,

    /// <summary>
    /// Ha resistencia e nao da para dizer de que tipo.
    ///
    /// E um valor legitimo, nao um buraco: chutar "preco" porque e o mais comum
    /// mandaria o vendedor defender valor quando o problema era que ele nem
    /// falava com quem decide.
    /// </summary>
    NaoClassificada = 99,
}

/// <param name="PorComportamento">
/// Detectada pela FORMA da conversa, e nao pelo que foi dito. E a metade que o
/// vendedor nao ve sozinho: ninguem percebe que as respostas do cliente
/// encolheram pela metade ao longo de tres dias.
/// </param>
public record Objecao(
    TipoDeObjecao Tipo,
    string Descricao,
    string TrechoCitado,
    Guid MensagemId,
    bool PorComportamento)
{
    public string Descricao { get; init; } = string.IsNullOrWhiteSpace(Descricao)
        ? throw new ArgumentException("Objecao sem descricao.", nameof(Descricao))
        : Descricao.Trim();

    /// <summary>
    /// A frase que originou a leitura. Obrigatoria pela mesma razao do
    /// <see cref="Sinal"/>: sem ela o vendedor nao tem como conferir, e a
    /// objecao vira palpite com cara de dado.
    /// </summary>
    public string TrechoCitado { get; init; } = string.IsNullOrWhiteSpace(TrechoCitado)
        ? throw new ArgumentException("Objecao sem a fala que a originou.", nameof(TrechoCitado))
        : TrechoCitado.Trim();

    public Guid MensagemId { get; init; } = MensagemId == Guid.Empty
        ? throw new ArgumentException("Objecao sem procedencia.", nameof(MensagemId))
        : MensagemId;
}

/// <summary>
/// A objecao que aparece na FORMA da conversa, sem ninguem precisar dize-la (#9).
///
/// "Vou pensar" e a objecao mais comum do varejo e a que mais vendedor novato
/// interpreta como interesse. Mas o sinal mais forte nem sempre esta no texto:
/// esta na resposta que encurtou, no intervalo que cresceu, no cliente que
/// sumiu depois do numero.
///
/// Isso aqui NAO usa modelo. E aritmetica sobre a conversa, roda de graca, e da
/// o mesmo resultado toda vez — o que faz dela a parte do dossie que nunca
/// alucina.
/// </summary>
public static class PadraoDeConversa
{
    /// <summary>Menos que isso nao e tendencia, e so uma conversa curta.</summary>
    public const int MinimoDeFalasParaTendencia = 4;

    /// <summary>Abaixo disso, o encurtamento deixa de ser variacao normal.</summary>
    public const double LimiteDeEncurtamento = 0.5;

    /// <summary>Silencio que ja nao e "ele esta ocupado".</summary>
    public static readonly TimeSpan SilencioQuePreocupa = TimeSpan.FromHours(48);

    private static readonly string[] Adiamentos =
    [
        "vou pensar", "vou analisar", "vou avaliar", "vou ver", "pensar melhor",
        "te falo", "te retorno", "te aviso", "depois eu", "mais pra frente",
        "vou falar com", "ver com meu", "ver com a", "consultar",
        "manda por escrito", "manda no email", "me manda por",
    ];

    public static IReadOnlyList<Objecao> Detectar(Conversa conversa, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(conversa);

        var achadas = new List<Objecao>();

        // Midia fica de fora da conta de tamanho: "[audio nao transcrito, 14s]"
        // e texto NOSSO, e medir o marcador seria medir a nossa escrita como se
        // fosse a do cliente.
        var doCliente = conversa.Mensagens
            .Where(m => m.Autor == Autor.Cliente && !m.NaoInterpretada)
            .OrderBy(m => m.EnviadaEm)
            .ToList();

        if (Adiou(doCliente) is { } adiamento) achadas.Add(adiamento);
        if (Encurtou(doCliente) is { } encurtamento) achadas.Add(encurtamento);
        if (Sumiu(conversa, agora) is { } sumico) achadas.Add(sumico);

        return achadas;
    }

    /// <summary>
    /// O adiamento dito com todas as letras — e a unica que o vendedor
    /// costuma ver sozinho, mas costuma ler como interesse.
    /// </summary>
    private static Objecao? Adiou(List<Mensagem> doCliente)
    {
        var fala = doCliente.LastOrDefault(m => Adiamentos.Any(
            a => m.Texto.Contains(a, StringComparison.OrdinalIgnoreCase)));

        return fala is null
            ? null
            : new Objecao(
                TipoDeObjecao.Timing,
                "adiou sem dizer nao: 'vou pensar' e a objecao que mais parece interesse",
                fala.Texto,
                fala.Id,
                PorComportamento: false);
    }

    /// <summary>
    /// As respostas encolheram. Ninguem percebe isso sozinho ao longo de dias,
    /// e e um dos sinais mais confiaveis de que a conversa esfriou.
    /// </summary>
    private static Objecao? Encurtou(List<Mensagem> doCliente)
    {
        if (doCliente.Count < MinimoDeFalasParaTendencia) return null;

        var metade = doCliente.Count / 2;
        var comeco = doCliente.Take(metade).Average(m => m.Texto.Length);
        var fim = doCliente.Skip(doCliente.Count - 2).Average(m => m.Texto.Length);

        if (comeco <= 0 || fim / comeco > LimiteDeEncurtamento) return null;

        var ultima = doCliente[^1];

        return new Objecao(
            TipoDeObjecao.NaoClassificada,
            $"as respostas encurtaram: de {comeco:0} para {fim:0} caracteres em media",
            ultima.Texto,
            ultima.Id,
            PorComportamento: true);
    }

    /// <summary>
    /// Sumiu. A citacao e a ULTIMA fala dele — e a frase depois da qual o
    /// silencio comecou, que e o que o vendedor precisa reler.
    /// </summary>
    private static Objecao? Sumiu(Conversa conversa, DateTimeOffset agora)
    {
        var silencio = conversa.SilencioDoCliente(agora);
        if (silencio is null || silencio < SilencioQuePreocupa) return null;

        var ultima = conversa.UltimaDoCliente!;

        return new Objecao(
            TipoDeObjecao.NaoClassificada,
            $"sem responder ha {silencio.Value.TotalDays:0} dia(s)",
            ultima.Texto,
            ultima.Id,
            PorComportamento: true);
    }
}
