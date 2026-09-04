namespace Gatilhos;

/// <summary>
/// A saida, em tres formas para tres leitores: o terminal de quem roda na
/// maquina, a anotacao que o GitHub gruda na linha do arquivo dentro do PR, e
/// o resumo do job.
///
/// A anotacao e a que muda o comportamento. Relatorio que mora dentro do log do
/// job exige que alguem abra o log; anotacao aparece na linha do diff que a
/// pessoa ja esta lendo — e a diferenca entre um gate que ensina e um gate que
/// so registra.
/// </summary>
public static class Relatorio
{
    public static void NoTerminal(IReadOnlyList<Achado> achados)
    {
        if (achados.Count == 0)
        {
            Console.WriteLine("Nenhum gatilho de KISS/DRY foi batido.");
            return;
        }

        Console.WriteLine($"{achados.Count} gatilho(s) batido(s):");
        Console.WriteLine();

        foreach (var grupo in achados.GroupBy(a => a.Gatilho).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"{grupo.Key.ToUpperInvariant()}");
            foreach (var a in grupo.OrderBy(a => a.Arquivo, StringComparer.Ordinal).ThenBy(a => a.Linha))
                Console.WriteLine($"  {a.Arquivo}:{a.Linha}  {a.Onde}  —  {a.Resumo}");
            Console.WriteLine();
        }
    }

    public static void ComoAnotacao(IReadOnlyList<Achado> achados)
    {
        foreach (var a in achados)
        {
            var titulo = Escapar($"Gatilho de KISS/DRY: {a.Gatilho}");
            var mensagem = Escapar($"{a.Onde} — {a.Resumo}. Abre issue com os labels kiss-dry e refatoracao, e SEGUE o trabalho: refatoracao misturada com feature produz diff que ninguem revisa.");
            Console.WriteLine($"::warning file={a.Arquivo},line={a.Linha},title={titulo}::{mensagem}");
        }
    }

    public static string Resumo(IReadOnlyList<Achado> achados)
    {
        if (achados.Count == 0)
        {
            return "## Gatilhos de KISS e DRY\n\nNenhum limite estourado. "
                + "Este gate nunca reprova o PR — ele avisa.\n";
        }

        var linhas = achados
            .OrderBy(a => a.Gatilho, StringComparer.Ordinal)
            .ThenBy(a => a.Arquivo, StringComparer.Ordinal)
            .Select(a => $"| {a.Gatilho} | `{a.Arquivo}:{a.Linha}` | {a.Onde} | {a.Valor} | {a.Limite} | {a.Detalhe} |");

        return $"""
            ## Gatilhos de KISS e DRY

            **{achados.Count} limite(s) estourado(s).** Este gate **nao reprova** o PR, e isso e
            deliberado: gate que reprova build por um metodo de 42 linhas vira gate que todo
            mundo aprende a contornar — e a partir dai ele nao mede mais nada.

            O que a politica pede ao bater um gatilho: **abrir issue com os labels `kiss-dry` e
            `refatoracao` e SEGUIR o trabalho.** Refatorar aqui dentro mistura mudanca de
            comportamento com mudanca cosmetica no mesmo diff.

            | Gatilho | Onde | O que | Medido | Limite | |
            |---|---|---|---|---|---|
            {string.Join("\n", linhas)}

            Antes de unificar bloco repetido, a pergunta do CONTRIBUTING: *"se um desses mudar,
            o outro tem que mudar junto, sempre?"* Se nao for um sim claro, deixa duplicado.

            """;
    }

    /// <summary>
    /// Anotacao do GitHub e uma linha so: `\n` de verdade cortaria a mensagem
    /// no meio, e virgula seria lida como o proximo parametro.
    /// </summary>
    private static string Escapar(string texto) =>
        texto.Replace("%", "%25", StringComparison.Ordinal)
             .Replace("\r", "%0D", StringComparison.Ordinal)
             .Replace("\n", "%0A", StringComparison.Ordinal)
             .Replace(",", "%2C", StringComparison.Ordinal)
             .Replace("::", "%3A%3A", StringComparison.Ordinal);
}
