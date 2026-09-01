namespace Copiloto.Dominio.Planos;

/// <summary>
/// Tatica que precisa de dado do CRM para poder ser sugerida (#15, #16).
/// Fora desta lista, a sugestao e livre — nao ha o que ancorar.
/// </summary>
public enum Tatica { Escassez = 0, ProvaSocial = 1, Desconto = 2, Prazo = 3, Livre = 4 }

/// <summary>
/// Um item do plano de abordagem: ou sugestao ancorada, ou pergunta ao vendedor.
///
/// A REGRA DE ANCORAGEM esta no construtor privado e nos dois criadores, e nao
/// numa validacao que alguem chama depois. Escassez, prova social, desconto e
/// prazo so viram sugestao se existir dado no CRM que sustente; sem dado, vira
/// PERGUNTA.
///
/// Sugerir "restam 2 unidades" quando existem 200 e publicidade enganosa: cria
/// passivo para a empresa que usa o produto e queima o vendedor com o cliente.
/// Por isso a checagem nao pode depender de quem constroi lembrar dela.
/// </summary>
public class BlocoSugerido
{
    private BlocoSugerido(Tatica tatica, string texto, string? ancora, bool ehPergunta)
    {
        Tatica = tatica;
        Texto = texto;
        Ancora = ancora;
        EhPergunta = ehPergunta;
    }

    public Tatica Tatica { get; }
    public string Texto { get; }

    /// <summary>O dado do CRM que sustenta a sugestao. Null quando e pergunta.</summary>
    public string? Ancora { get; }

    /// <summary>Pergunta ao vendedor, e nao fala pronta.</summary>
    public bool EhPergunta { get; }

    public static bool PrecisaDeAncora(Tatica tatica) => tatica != Tatica.Livre;

    /// <summary>
    /// Sugestao ancorada. Recusa a tatica que exige dado quando a ancora falta —
    /// devolver um bloco sem ancora aqui seria a regra existir e nao valer.
    /// </summary>
    public static BlocoSugerido Ancorado(Tatica tatica, string texto, string ancora)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Bloco sem texto.", nameof(texto));
        if (PrecisaDeAncora(tatica) && string.IsNullOrWhiteSpace(ancora))
            throw new ArgumentException(
                $"A tatica {tatica} exige dado do CRM que a sustente. Sem ancora, "
                + "use Perguntar() — a sugestao vira pergunta ao vendedor, nunca fala pronta.",
                nameof(ancora));

        return new BlocoSugerido(tatica, texto.Trim(), ancora?.Trim(), ehPergunta: false);
    }

    /// <summary>O caminho de saida quando o dado nao existe.</summary>
    public static BlocoSugerido Perguntar(Tatica tatica, string pergunta)
    {
        if (string.IsNullOrWhiteSpace(pergunta))
            throw new ArgumentException("Pergunta sem texto.", nameof(pergunta));

        return new BlocoSugerido(tatica, pergunta.Trim(), ancora: null, ehPergunta: true);
    }
}
