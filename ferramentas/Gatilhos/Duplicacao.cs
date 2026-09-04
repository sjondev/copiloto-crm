using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Gatilhos;

/// <summary>
/// O unico gatilho que so aparece olhando os arquivos JUNTOS: o mesmo bloco
/// logico na terceira aparicao.
///
/// A comparacao e por TOKEN, e nao por linha: renomear variavel, quebrar linha
/// ou trocar aspas simples por interpolacao muda o texto e nao muda o bloco —
/// e duplicacao que a ferramenta perde por causa disso e duplicacao que
/// continua se espalhando.
///
/// O que ela NAO decide e se aquilo deve virar uma funcao. Duplicacao
/// acidental — dois trechos iguais que mudam por razoes diferentes — e
/// coincidencia, e unificar acopla duas regras que precisam evoluir separadas.
/// A ferramenta aponta; quem decide le o CONTRIBUTING e responde a pergunta de
/// la: "se um desses mudar, o outro tem que mudar junto, sempre?"
/// </summary>
public static class Duplicacao
{
    public static IReadOnlyList<Achado> Entre(IReadOnlyList<(string Caminho, string Texto)> arquivos)
    {
        var janelas = new Dictionary<string, List<Ocorrencia>>(StringComparer.Ordinal);

        foreach (var (caminho, texto) in arquivos)
            Indexar(caminho, texto, janelas);

        return Colher(janelas);
    }

    private static void Indexar(
        string caminho, string texto, Dictionary<string, List<Ocorrencia>> janelas)
    {
        var tokens = CSharpSyntaxTree.ParseText(texto, path: caminho)
            .GetRoot()
            .DescendantTokens()
            .Where(t => !t.IsKind(SyntaxKind.EndOfFileToken))
            .ToList();

        for (var i = 0; i + Limites.TokensDoBloco <= tokens.Count; i++)
        {
            var chave = string.Join(
                ' ', tokens.Skip(i).Take(Limites.TokensDoBloco).Select(t => t.Text));

            var linha = tokens[i].GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            if (!janelas.TryGetValue(chave, out var lista))
                janelas[chave] = lista = [];

            lista.Add(new Ocorrencia(caminho, i, linha));
        }
    }

    /// <summary>
    /// Junta as janelas repetidas em um achado por bloco.
    ///
    /// Um trecho repetido de 200 tokens produz 140 janelas repetidas, todas
    /// verdadeiras e todas a mesma coisa. Relatorio com 140 linhas para um
    /// problema so nao e mais informacao: e menos, porque ninguem le ate o fim.
    /// Por isso, emitido um bloco, o que se sobrepoe a ele fica de fora.
    /// </summary>
    private static List<Achado> Colher(Dictionary<string, List<Ocorrencia>> janelas)
    {
        var achados = new List<Achado>();
        var emitidos = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        var candidatos = janelas.Values
            .Where(o => o.Count >= Limites.AparicoesDoMesmoBloco)
            .OrderBy(o => o[0].Caminho, StringComparer.Ordinal)
            .ThenBy(o => o[0].Indice);

        foreach (var grupo in candidatos)
        {
            if (grupo.Any(o => JaEmitido(emitidos, o))) continue;

            foreach (var o in grupo) Marcar(emitidos, o);

            var onde = string.Join(", ", grupo.Skip(1).Select(o => $"{o.Caminho}:{o.Linha}"));

            achados.Add(new Achado(
                "bloco repetido", grupo[0].Caminho, grupo[0].Linha,
                $"{Limites.TokensDoBloco} tokens", grupo.Count,
                Limites.AparicoesDoMesmoBloco, $"tambem em {onde}"));
        }

        return achados;
    }

    private static bool JaEmitido(Dictionary<string, HashSet<int>> emitidos, Ocorrencia o) =>
        emitidos.TryGetValue(o.Caminho, out var indices) && indices.Contains(o.Indice);

    private static void Marcar(Dictionary<string, HashSet<int>> emitidos, Ocorrencia o)
    {
        if (!emitidos.TryGetValue(o.Caminho, out var indices))
            emitidos[o.Caminho] = indices = [];

        for (var i = o.Indice; i < o.Indice + Limites.TokensDoBloco; i++)
            indices.Add(i);
    }

    private sealed record Ocorrencia(string Caminho, int Indice, int Linha);
}
