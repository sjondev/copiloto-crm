namespace Gatilhos;

/// <summary>
/// Uma coisa que passou do limite, no formato que o relatorio precisa: onde,
/// o que, quanto e contra qual limite.
///
/// O valor e o limite andam JUNTOS de proposito. "Metodo grande" e opiniao;
/// "57 linhas, limite 40" e constatacao — e a diferenca entre as duas e o que
/// decide se a conversa na revisao acaba em um minuto ou em dez.
/// </summary>
public record Achado(
    string Gatilho,
    string Arquivo,
    int Linha,
    string Onde,
    int Valor,
    int Limite,
    string? Detalhe = null)
{
    public string Resumo =>
        Detalhe is null
            ? $"{Gatilho}: {Valor} (limite {Limite})"
            : $"{Gatilho}: {Valor} (limite {Limite}) — {Detalhe}";
}

/// <summary>
/// Os numeros da politica (#74), num lugar so.
///
/// Eles estao no CONTRIBUTING.md e no CLAUDE.md em forma de tabela; aqui estao
/// em forma de codigo. Mudar um so num lugar produz uma ferramenta que mede uma
/// coisa e uma politica que promete outra — por isso mudar aqui obriga a mudar
/// la, e o PR que fizer isso vai ter os dois arquivos no diff.
/// </summary>
public static class Limites
{
    public const int LinhasPorArquivo = 300;
    public const int LinhasPorMetodo = 40;
    public const int ParametrosPorMetodo = 4;
    public const int NiveisDeAninhamento = 3;

    /// <summary>Repeticao a partir da TERCEIRA aparicao — a regra dos tres.</summary>
    public const int AparicoesDoMesmoBloco = 3;

    /// <summary>
    /// Tamanho minimo, em tokens, para um trecho repetido contar como bloco.
    ///
    /// Calibrado contra este repositorio, em 04/09/2026, medindo de verdade:
    ///
    ///   30 tokens -> 5 achados, um deles casando `EscudoDePiiTeste` com
    ///                `RoteadorDeModeloTeste` — dois assuntos que nao tem nada
    ///                a ver, ou seja, coincidencia vendida como duplicacao;
    ///   40 tokens -> 2 achados, os dois verdadeiros;
    ///   80 tokens -> 0 achados, perdendo os dois.
    ///
    /// Gate que acusa o que ninguem vai arrumar ensina a ignorar o gate, e gate
    /// que nao acha nada e enfeite. 40 e onde os dois erros ficam pequenos.
    /// </summary>
    public const int TokensDoBloco = 40;

    /// <summary>
    /// Complexidade ciclomatica: o unico gatilho que a #74 NAO fixou.
    ///
    /// Entra com 10, que e o numero classico do McCabe, e entra como
    /// observacao: enquanto a politica nao adotar, ele aparece no relatorio
    /// para dar material a decisao, e nao para cobrar nada de ninguem.
    /// </summary>
    public const int ComplexidadeCiclomatica = 10;
}
