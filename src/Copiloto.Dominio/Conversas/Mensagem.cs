namespace Copiloto.Dominio.Conversas;

public enum Autor { Cliente = 0, Vendedor = 1 }

/// <summary>
/// Uma fala. E a unidade que o dossie CITA.
///
/// Imutavel por decisao: mensagem de WhatsApp nao muda depois de enviada, e o
/// sinal do dossie aponta para ela. Sinal que cita uma fala editavel e sinal
/// que pode passar a citar outra coisa sem ninguem perceber — a citacao existe
/// justamente para o vendedor conferir com os proprios olhos.
/// </summary>
public class Mensagem
{
    /// <param name="midia">
    /// O anexo, quando a fala nao e texto (#23). Com midia e sem texto, o
    /// <see cref="Midia.Marcador"/> entra no lugar — mensagem sem texto nao
    /// existe neste dominio, e um audio nao pode virar buraco silencioso na
    /// conversa so porque ninguem o transcreveu.
    /// </param>
    public Mensagem(
        Guid id, Autor autor, string texto, DateTimeOffset enviadaEm, Midia? midia = null)
        : this(id, autor, texto, enviadaEm, midia?.Tipo, midia?.Duracao)
    {
    }

    /// <summary>
    /// O construtor que o EF Core consegue usar.
    ///
    /// Ele recusa parametro que nao corresponda a propriedade mapeada, e um
    /// objeto de valor nao corresponde: <c>Midia</c> entra decomposta em duas
    /// colunas e volta montada pela propriedade. O dominio continua POCO — quem
    /// cede aqui e a forma, nao a regra.
    /// </summary>
    private Mensagem(
        Guid id,
        Autor autor,
        string texto,
        DateTimeOffset enviadaEm,
        TipoDeMidia? tipoDeMidia,
        TimeSpan? duracaoDaMidia)
    {
        if (id == Guid.Empty) throw new ArgumentException("Mensagem sem id.", nameof(id));

        TipoDeMidia = tipoDeMidia;
        DuracaoDaMidia = duracaoDaMidia;

        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = Midia?.Marcador
                ?? throw new ArgumentException("Mensagem sem texto nao e fala.", nameof(texto));
        }

        Id = id;
        Autor = autor;
        Texto = texto;
        EnviadaEm = enviadaEm;
    }

    public Guid Id { get; }
    public Autor Autor { get; }
    public string Texto { get; }
    public DateTimeOffset EnviadaEm { get; }
    public TipoDeMidia? TipoDeMidia { get; }
    public TimeSpan? DuracaoDaMidia { get; }

    /// <summary>O anexo, ou <c>null</c> quando a fala e texto puro.</summary>
    public Midia? Midia => TipoDeMidia is { } tipo ? new Midia(tipo, DuracaoDaMidia) : null;

    /// <summary>
    /// A fala carrega conteudo que esta leitura nao interpretou.
    ///
    /// E o que o dossie precisa saber para declarar a lacuna, e o que separa
    /// "o cliente nao respondeu" de "o cliente respondeu e nos nao ouvimos".
    /// </summary>
    public bool NaoInterpretada => Midia is not null;
}
