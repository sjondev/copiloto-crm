using System.Text.RegularExpressions;
using Copiloto.Dominio.Planos;

namespace Copiloto.Api.Seguranca;

/// <summary>O que impediu um bloco de chegar a tela, e por que.</summary>
public record Bloqueio(string Motivo, string Trecho);

/// <summary>
/// Confere o que o modelo devolveu, antes de chegar na tela (#44).
///
/// E a unica camada que NAO depende de o modelo obedecer. Delimitador, nonce e
/// instrucao de sistema reduzem a chance de a injecao pegar; nenhum deles
/// garante. Verificar a saida garante — porque nao pergunta ao modelo se ele
/// se comportou, olha o que ele produziu.
///
/// A regra de ancoragem ja vive no `BlocoSugerido` (construtor privado, so
/// `Ancorado()` ou `Perguntar()`), entao o que sobra aqui e o que a injecao
/// consegue fazer mesmo respeitando o tipo: encher a ancora de invencao, ou
/// escrever fala pronta para o cliente.
/// </summary>
public static class GuardaDeSaida
{
    /// <summary>
    /// Promessas que so a EMPRESA pode fazer. Se aparecerem, e porque o texto
    /// saiu do lugar de "leitura da conversa" para o de "oferta".
    /// </summary>
    private static readonly (string Motivo, Regex Padrao)[] Proibicoes =
    [
        ("promete gratuidade",
            new Regex(@"\b(?:de\s+gra[cç]a|gratuito|gratis|sem\s+custo|isento|cortesia)\b",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("promete desconto com numero",
            new Regex(@"\b\d{1,3}\s*%\s*(?:de\s*)?(?:desconto|off)\b|\bdesconto\s+de\s+\d",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("fala como se fosse o vendedor escrevendo ao cliente",
            new Regex(@"\b(?:ignore|desconsidere|esque[cç]a)\s+(?:as\s+)?(?:instru|regras|tudo)",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    /// <summary>
    /// Filtra os blocos, devolvendo os que passam e os que foram barrados.
    ///
    /// Barrar em vez de corrigir: um bloco reescrito pelo guarda seria texto que
    /// ninguem revisou aparecendo como se o modelo tivesse produzido, e o
    /// vendedor nao teria como saber qual e qual.
    /// </summary>
    public static (IReadOnlyList<BlocoSugerido> Aprovados, IReadOnlyList<Bloqueio> Barrados)
        Filtrar(IEnumerable<BlocoSugerido> blocos, Playbook playbook)
    {
        ArgumentNullException.ThrowIfNull(blocos);
        ArgumentNullException.ThrowIfNull(playbook);

        var aprovados = new List<BlocoSugerido>();
        var barrados = new List<Bloqueio>();

        foreach (var bloco in blocos)
        {
            var motivo = PorQueBarrar(bloco, playbook);
            if (motivo is null) aprovados.Add(bloco);
            else barrados.Add(new Bloqueio(motivo, bloco.Texto));
        }

        return (aprovados, barrados);
    }

    private static string? PorQueBarrar(BlocoSugerido bloco, Playbook playbook)
    {
        // A tatica precisa estar autorizada pela empresa. O desconto maximo e
        // decisao de quem vende, nunca do modelo.
        if (!playbook.Autoriza(bloco.Tatica))
            return $"a tatica {bloco.Tatica} nao esta no playbook desta empresa";

        foreach (var (motivo, padrao) in Proibicoes)
            if (padrao.IsMatch(bloco.Texto))
                return motivo;

        // Pergunta ao vendedor pode falar de qualquer coisa: ela nao afirma
        // nada ao cliente, e barra-la calaria justamente a saida segura que a
        // regra de ancoragem oferece.
        if (bloco.EhPergunta) return null;

        // Sugestao ancorada com ancora vazia nao deveria existir (o construtor
        // recusa), mas se um caminho novo aparecer, ela nao passa por aqui.
        if (BlocoSugerido.PrecisaDeAncora(bloco.Tatica)
            && string.IsNullOrWhiteSpace(bloco.Ancora))
            return "sugestao sem ancora chegou ao guarda: alguem criou um caminho "
                 + "que desvia do construtor";

        return null;
    }
}
