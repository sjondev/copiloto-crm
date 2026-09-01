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
    public Mensagem(Guid id, Autor autor, string texto, DateTimeOffset enviadaEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Mensagem sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Mensagem sem texto nao e fala.", nameof(texto));

        Id = id;
        Autor = autor;
        Texto = texto;
        EnviadaEm = enviadaEm;
    }

    public Guid Id { get; }
    public Autor Autor { get; }
    public string Texto { get; }
    public DateTimeOffset EnviadaEm { get; }
}
