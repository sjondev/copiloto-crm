using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Gatilhos;

/// <summary>
/// Os gatilhos que se medem dentro de UM arquivo: tamanho, parametros,
/// aninhamento e complexidade.
///
/// Tudo sai da arvore do Roslyn, e nao de expressao regular. Chave em string
/// interpolada, chave em comentario e `{{` de formatacao existem neste
/// repositorio, e cada um deles quebraria uma contagem por texto — com o
/// agravante de quebrar em silencio, produzindo um numero plausivel.
/// </summary>
public static class Medidas
{
    public static IEnumerable<Achado> DoArquivo(string caminho, string texto)
    {
        var linhas = texto.AsSpan().Count('\n') + (texto.EndsWith('\n') ? 0 : 1);
        if (linhas > Limites.LinhasPorArquivo)
        {
            yield return new Achado(
                "arquivo grande", caminho, 1, Path.GetFileName(caminho),
                linhas, Limites.LinhasPorArquivo, "linhas");
        }

        var raiz = CSharpSyntaxTree.ParseText(texto, path: caminho).GetCompilationUnitRoot();

        foreach (var achado in raiz.DescendantNodes().Where(EhCorpo).SelectMany(no => DoCorpo(caminho, no)))
            yield return achado;
    }

    private static bool EhCorpo(SyntaxNode no) =>
        no is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax;

    private static IEnumerable<Achado> DoCorpo(string caminho, SyntaxNode no)
    {
        var span = no.GetLocation().GetLineSpan();
        var linha = span.StartLinePosition.Line + 1;
        var onde = Nome(no);
        var tamanho = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;

        if (tamanho > Limites.LinhasPorMetodo)
            yield return new Achado("metodo grande", caminho, linha, onde, tamanho, Limites.LinhasPorMetodo, "linhas");

        var parametros = Parametros(no);
        if (parametros > Limites.ParametrosPorMetodo)
            yield return new Achado("parametros demais", caminho, linha, onde, parametros, Limites.ParametrosPorMetodo, "parametros");

        var fundura = Aninhamento(no);
        if (fundura > Limites.NiveisDeAninhamento)
            yield return new Achado("aninhamento fundo", caminho, linha, onde, fundura, Limites.NiveisDeAninhamento, "niveis");

        var complexidade = Complexidade(no);
        if (complexidade > Limites.ComplexidadeCiclomatica)
            yield return new Achado("complexidade", caminho, linha, onde, complexidade, Limites.ComplexidadeCiclomatica, "caminhos (observacao: a #74 nao fixou este limite)");
    }

    private static string Nome(SyntaxNode no)
    {
        var proprio = no switch
        {
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => "ctor",
            LocalFunctionStatementSyntax f => f.Identifier.Text,
            OperatorDeclarationSyntax o => $"operator {o.OperatorToken.Text}",
            ConversionOperatorDeclarationSyntax => "conversao",
            _ => "?",
        };

        var tipo = no.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return tipo is null ? proprio : $"{tipo.Identifier.Text}.{proprio}";
    }

    private static int Parametros(SyntaxNode no) => no switch
    {
        BaseMethodDeclarationSyntax m => m.ParameterList.Parameters.Count,
        LocalFunctionStatementSyntax f => f.ParameterList.Parameters.Count,
        _ => 0,
    };

    /// <summary>
    /// A fundura maxima de blocos aninhados dentro do corpo.
    ///
    /// `else if` nao conta como nivel novo: no Roslyn ele e um `if` dentro de um
    /// `else`, e contar a arvore literalmente daria fundura 5 para uma cadeia
    /// que qualquer pessoa le como uma lista de casos.
    /// </summary>
    private static int Aninhamento(SyntaxNode no)
    {
        var maximo = 0;
        foreach (var dentro in no.DescendantNodes().Where(Aninha))
        {
            var fundura = dentro.Ancestors().TakeWhile(a => a != no).Count(Aninha) + 1;
            if (fundura > maximo) maximo = fundura;
        }
        return maximo;
    }

    private static bool Aninha(SyntaxNode no) => no switch
    {
        IfStatementSyntax => no.Parent is not ElseClauseSyntax,
        ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
            or DoStatementSyntax or SwitchStatementSyntax or TryStatementSyntax
            or LockStatementSyntax or UsingStatementSyntax => true,
        _ => false,
    };

    /// <summary>
    /// Complexidade ciclomatica pelo caminho barato: um, mais cada ponto onde a
    /// execucao se divide. Nao e a formula do grafo, e da o mesmo numero nos
    /// casos que aparecem em codigo de verdade.
    /// </summary>
    private static int Complexidade(SyntaxNode no) =>
        1 + no.DescendantNodes().Count(Divide) + no.DescendantTokens().Count(DivideToken);

    private static bool Divide(SyntaxNode no) => no switch
    {
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
            or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax
            or ConditionalExpressionSyntax => true,
        SwitchExpressionArmSyntax arm => arm.Pattern is not DiscardPatternSyntax,
        CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax => true,
        _ => false,
    };

    private static bool DivideToken(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AmpersandAmpersandToken)
        || token.IsKind(SyntaxKind.BarBarToken)
        || token.IsKind(SyntaxKind.QuestionQuestionToken);
}
