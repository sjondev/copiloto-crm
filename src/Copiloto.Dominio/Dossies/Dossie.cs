using Copiloto.Dominio.Conversas;

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

    /// <summary>
    /// A temperatura lida, com direcao (#13). Nula enquanto ninguem leu.
    /// </summary>
    public Termometro? Termometro { get; private set; }

    public void Ler(Termometro termometro) =>
        Termometro = termometro ?? throw new ArgumentNullException(nameof(termometro));

    public IReadOnlyList<Sinal> SinaisDe(TipoDeSinal tipo) =>
        _sinais.Where(s => s.Tipo == tipo).ToList();

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
    /// Declara, como lacuna, a midia que esta leitura nao interpretou (#23).
    ///
    /// Uma lacuna por TIPO e nao por mensagem: sete audios viram uma linha
    /// dizendo sete, e nao sete linhas iguais empurrando as outras lacunas —
    /// as de verdade, sobre o cliente — para fora da tela.
    ///
    /// Sem isto o dossie leria a conversa pela metade sem dizer que era pela
    /// metade, e "o cliente nao explicou o orcamento" sairia igual quer ele
    /// tenha ficado calado, quer tenha mandado dois minutos de audio.
    /// </summary>
    public void DeclararMidiaNaoInterpretada(IEnumerable<Mensagem> analisadas)
    {
        ArgumentNullException.ThrowIfNull(analisadas);

        var porTipo = analisadas
            .Where(m => m.NaoInterpretada)
            .GroupBy(m => m.Midia!.Tipo)
            .OrderBy(g => g.Key);

        foreach (var tipo in porTipo)
        {
            RegistrarLacuna(Midia.Lacuna(tipo.Key, tipo.Count()));
        }
    }

    /// <summary>
    /// Quantas vezes a mesma fala foi citada — o "preco citado 3x" da tela.
    /// </summary>
    public int VezesQueCitou(Guid mensagemId) =>
        _sinais.Count(s => s.MensagemId == mensagemId);
}
