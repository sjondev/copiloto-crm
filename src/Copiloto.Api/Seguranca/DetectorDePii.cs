using System.Text.RegularExpressions;

namespace Copiloto.Api.Seguranca;

/// <summary>
/// Procura dado pessoal no que JA passou pelo escudo. Independente dele.
///
/// A primeira versao reusava os padroes do `EscudoDePii`, com o argumento de
/// que dois detectores poderiam divergir. O argumento estava errado, e o teste
/// que sabota o escudo provou: desligando o padrao de telefone do escudo, o
/// gate continuou VERDE — porque o detector deixou de procurar exatamente o que
/// o escudo deixou de mascarar.
///
/// Compartilhar os padroes torna o gate cego a falha mais provavel, que e a de
/// COBERTURA: alguem escreve um regex que nao pega um formato, e nada acusa.
/// Um gate de seguranca que so encontra o que o proprio codigo ja sabe procurar
/// nao e gate, e eco.
///
/// Por isso aqui os padroes sao proprios e deliberadamente MAIS AMPLOS. Falso
/// positivo custa uma investigacao; falso negativo custa dado pessoal do
/// cliente na rede de terceiro.
/// </summary>
public static class DetectorDePii
{
    private static readonly (string Tipo, Regex Padrao)[] Suspeitas =
    [
        // Qualquer coisa com arroba entre caracteres nao-brancos.
        ("possivel e-mail", new Regex(@"\S+@\S+", RegexOptions.Compiled)),

        // Sequencias longas de digitos, aceitando pontuacao no meio. Nao tenta
        // dizer se e CPF, telefone ou CEP: na saida, digito agrupado e suspeito
        // ate prova em contrario.
        ("sequencia numerica longa",
            new Regex(@"(?<![\w\[])(?:\d[\s.\-()]?){8,}\d(?![\w\]])", RegexOptions.Compiled)),

        // Logradouro seguido de qualquer coisa: mais amplo que o do escudo, que
        // exige numero.
        ("possivel endereco",
            new Regex(@"\b(?:rua|av\.?|avenida|travessa|alameda|pra[cç]a|rodovia|estrada)\s+\w",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    /// <summary>
    /// O que ainda parece dado pessoal. Vazio e o esperado depois do escudo.
    ///
    /// Marcadores (`[TEL_1]`) sao ignorados de proposito: eles sao o RESULTADO
    /// do escudo, e acusa-los faria o gate reprovar o proprio conserto.
    /// </summary>
    public static IReadOnlyList<string> Suspeito(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return [];

        var semMarcadores = Regex.Replace(texto, @"\[[A-Z]+_\d+\]", " ");

        var achados = new List<string>();
        foreach (var (tipo, padrao) in Suspeitas)
            foreach (Match m in padrao.Matches(semMarcadores))
                achados.Add($"{tipo}: {m.Value.Trim()}");

        return achados;
    }
}
