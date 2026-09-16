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
    private readonly List<Objecao> _objecoes = new();

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
    /// A leitura entra decomposta em duas colunas porque o EF Core nao mapeia
    /// objeto de valor em propriedade sem setter publico — e abrir o setter
    /// deixaria qualquer um trocar a temperatura sem passar por <see cref="Ler"/>.
    /// </summary>
    public Temperatura? TemperaturaLida { get; private set; }

    public Direcao? DirecaoLida { get; private set; }

    /// <summary>
    /// A temperatura lida, com direcao (#13). Nula enquanto ninguem leu.
    /// </summary>
    public Termometro? Termometro =>
        TemperaturaLida is { } valor ? new Termometro(valor, DirecaoLida ?? Direcao.Estavel) : null;

    public void Ler(Termometro termometro)
    {
        ArgumentNullException.ThrowIfNull(termometro);

        TemperaturaLida = termometro.Valor;
        DirecaoLida = termometro.Para;
    }

    public IReadOnlyList<Sinal> SinaisDe(TipoDeSinal tipo) =>
        _sinais.Where(s => s.Tipo == tipo).ToList();

    /// <summary>
    /// A resistencia que o cliente nao declarou (#9).
    ///
    /// Separada dos sinais porque tem TIPO — preco, timing, autoridade — e cada
    /// um pede coisa diferente do vendedor. Um sinal de fuga diz "ele esta
    /// saindo"; a objecao diz por onde.
    /// </summary>
    public IReadOnlyList<Objecao> Objecoes => _objecoes;

    public void Registrar(Objecao objecao)
    {
        ArgumentNullException.ThrowIfNull(objecao);

        // A mesma fala pode originar o adiamento E o encurtamento. Repetir a
        // dupla tipo+trecho na tela seria ruido, e ruido no dossie e o que faz
        // o vendedor parar de ler as linhas de baixo.
        if (_objecoes.Any(o => o.Tipo == objecao.Tipo && o.TrechoCitado == objecao.TrechoCitado)) return;

        _objecoes.Add(objecao);
    }

    /// <summary>
    /// Le a FORMA da conversa e registra o que ela mostra (#9).
    ///
    /// Roda sem modelo nenhum, e por isso e a parte do dossie que nao alucina:
    /// aritmetica sobre as falas da o mesmo resultado toda vez.
    /// </summary>
    public void DetectarObjecoes(Conversa conversa, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(conversa);

        foreach (var objecao in PadraoDeConversa.Detectar(conversa, agora))
            Registrar(objecao);
    }

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

        var limpa = pergunta.Trim();

        // A mesma pergunta duas vezes e ruido, e ruido no dossie e o que faz o
        // vendedor parar de ler as linhas de baixo — a mesma razao ja escrita em
        // `Registrar(Objecao)`. Agora que as lacunas vem de DUAS fontes (o
        // agente, lendo a conversa, e a ficha, pelos slots vazios da #8), a
        // repeticao deixou de ser hipotetica.
        if (_lacunas.Contains(limpa, StringComparer.OrdinalIgnoreCase)) return;

        _lacunas.Add(limpa);
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
