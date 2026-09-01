namespace Copiloto.Dominio.Dossies;

/// <summary>
/// O que a IA leu da conversa, para o vendedor decidir. Nunca uma fala pronta.
///
/// As LACUNAS sao a parte mais util e a mais facil de esquecer: "o que ainda nao
/// sabemos sobre este cliente" e o que faz o vendedor perguntar em vez de supor.
/// </summary>
public class Dossie
{
    private readonly List<Sinal> _sinais = new();
    private readonly List<string> _lacunas = new();

    public Dossie(Guid id, Guid dealId, DateTimeOffset geradoEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Dossie sem id.", nameof(id));
        if (dealId == Guid.Empty) throw new ArgumentException("Dossie sem deal.", nameof(dealId));

        Id = id;
        DealId = dealId;
        GeradoEm = geradoEm;
    }

    public Guid Id { get; }
    public Guid DealId { get; }
    public DateTimeOffset GeradoEm { get; }
    public IReadOnlyList<Sinal> Sinais => _sinais;

    /// <summary>O que ainda nao sabemos — perguntas, nao afirmacoes.</summary>
    public IReadOnlyList<string> Lacunas => _lacunas;

    public void Registrar(Sinal sinal)
    {
        ArgumentNullException.ThrowIfNull(sinal);
        _sinais.Add(sinal);
    }

    public void RegistrarLacuna(string pergunta)
    {
        if (string.IsNullOrWhiteSpace(pergunta)) return;
        _lacunas.Add(pergunta.Trim());
    }

    /// <summary>
    /// Quantas vezes a mesma fala foi citada — o "preco citado 3x" da tela.
    /// </summary>
    public int VezesQueCitou(Guid mensagemId) =>
        _sinais.Count(s => s.MensagemId == mensagemId);
}
