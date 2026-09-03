using Copiloto.Dominio.Fichas;

namespace Copiloto.Dominio.Planos;

/// <summary>
/// Tatica que precisa de dado do CRM para poder ser sugerida (#15, #16).
/// Fora desta lista, a sugestao e livre — nao ha o que ancorar.
///
/// `Preco` entra em #57: preco dito errado ao cliente e o erro que a empresa
/// tem de honrar depois, entao ele exige a mesma ancora que a escassez.
/// </summary>
public enum Tatica { Escassez = 0, ProvaSocial = 1, Desconto = 2, Prazo = 3, Livre = 4, Preco = 5 }

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

    /// <summary>
    /// Sugestao ancorada numa anotacao da ficha, com a procedencia junto (#88).
    ///
    /// Impressao nao ancora tatica que exige dado, e a recusa e aqui e nao no
    /// prompt: "parece que ele tem pressa" sustentando um prazo e o palpite do
    /// vendedor voltando para ele com a autoridade do sistema — e' camara de
    /// eco cara, porque confirma o vies em vez de corrigi-lo.
    ///
    /// A #88 cita escassez, prazo e preco. A regra ficou geral, valendo para
    /// desconto e prova social tambem, porque a distincao fato/impressao nao e'
    /// sobre a tatica: e' sobre AFIRMAR. Impressao sustenta hipotese, e
    /// hipotese nao e' o que uma dessas taticas devolve ao cliente.
    /// </summary>
    public static BlocoSugerido AncoradoEm(Tatica tatica, string texto, Anotacao anotacao)
    {
        ArgumentNullException.ThrowIfNull(anotacao);

        if (PrecisaDeAncora(tatica) && !anotacao.EhFato)
            throw new ArgumentException(
                $"A tatica {tatica} exige FATO, e '{anotacao.Valor}' e impressao. "
                + "Impressao sustenta no maximo hipotese: use Perguntar() para o "
                + "vendedor confirmar antes de a fala existir.",
                nameof(anotacao));

        // A ancora carrega a procedencia, e nao so o valor: ver "isso saiu de
        // uma impressao sua de tres semanas atras" e o que da ao vendedor a
        // chance de discordar. Conclusao sem procedencia ele so pode aceitar.
        return Ancorado(tatica, texto, anotacao.Rotulado());
    }

    /// <summary>O caminho de saida quando o dado nao existe.</summary>
    public static BlocoSugerido Perguntar(Tatica tatica, string pergunta)
    {
        if (string.IsNullOrWhiteSpace(pergunta))
            throw new ArgumentException("Pergunta sem texto.", nameof(pergunta));

        return new BlocoSugerido(tatica, pergunta.Trim(), ancora: null, ehPergunta: true);
    }
}
