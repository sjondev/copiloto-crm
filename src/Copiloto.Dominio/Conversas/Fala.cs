namespace Copiloto.Dominio.Conversas;

/// <summary>
/// Varios baloes que sao UMA fala.
///
/// Ninguem escreve paragrafo no WhatsApp: manda "bom dia", "vi o cafe de
/// voces", "o bourbon", "qual o valor do kg?" — quatro baloes, uma pergunta.
/// Tratar cada um como fala separada gera quatro analises, quatro custos e um
/// dossie que muda de opiniao a cada segundo.
/// </summary>
public class Fala
{
    private readonly List<Mensagem> _baloes;

    public Fala(Autor autor, IEnumerable<Mensagem> baloes)
    {
        _baloes = baloes?.ToList() ?? throw new ArgumentNullException(nameof(baloes));
        if (_baloes.Count == 0)
            throw new ArgumentException("Fala sem balao nao e fala.", nameof(baloes));

        Autor = autor;
    }

    public Autor Autor { get; }
    public IReadOnlyList<Mensagem> Baloes => _baloes;

    /// <summary>
    /// O texto junto, na ordem. E o que vai para a analise — e o que faz "o
    /// bourbon" deixar de ser uma frase solta sem sujeito.
    /// </summary>
    public string Texto => string.Join("\n", _baloes.Select(b => b.Texto));

    /// <summary>
    /// O instante da ULTIMA mensagem, e nao da primeira.
    ///
    /// A fala so esta completa quando o silencio confirma que acabou, entao e
    /// o fim dela que marca quando ela aconteceu. Usar a primeira faria o
    /// "sumiu ha 4 dias" contar a partir do "bom dia" em vez do "vou pensar".
    /// </summary>
    public DateTimeOffset Quando => _baloes[^1].EnviadaEm;

    /// <summary>Os ids, para o sinal do dossie poder citar o balao exato.</summary>
    public IReadOnlyList<Guid> IdsDosBaloes => _baloes.Select(b => b.Id).ToList();
}
