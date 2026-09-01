using System.Text.RegularExpressions;

namespace Copiloto.Api.Seguranca;

/// <summary>
/// Troca dado pessoal por marcador antes de a carga sair da rede, e remonta na
/// volta (#43).
///
/// Conversa com cliente e dado pessoal, e mandar para modelo de terceiro e
/// TRANSFERENCIA de dado pessoal sob a LGPD. O marcador nao elimina a
/// transferencia; reduz o que atravessa.
///
/// E PSEUDONIMIZACAO, NAO ANONIMIZACAO (#83). O mapa que remonta fica do nosso
/// lado, entao o texto continua sendo dado pessoal sob a LGPD — anonimo seria o
/// que nao da para reverter nem com informacao adicional, e aqui da, por
/// construcao. Chamar de anonimizacao seria a base legal errada.
/// </summary>
public class EscudoDePii
{
    // A ORDEM importa e nao e alfabetica: o que e mais especifico vai primeiro.
    // E-mail antes de tudo porque contem ponto e digito e seria retalhado pelos
    // outros; CPF antes de telefone porque os dois tem onze digitos.
    private static readonly (string Tipo, Regex Padrao)[] Padroes =
    [
        ("EMAIL", new Regex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", RegexOptions.Compiled)),
        ("CPF",   new Regex(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled)),
        ("CEP",   new Regex(@"\b\d{5}-\d{3}\b", RegexOptions.Compiled)),
        ("TEL",   new Regex(@"(?:\+55\s?)?\(?\b\d{2}\)?[\s.-]?9?\d{4}[\s.-]?\d{4}\b",
                            RegexOptions.Compiled)),
        // Endereco: logradouro + numero. Nao pega tudo, e a issue nao pede que
        // pegue — pega a forma que aparece em conversa de venda ("manda pra Rua
        // X, 123").
        ("END",   new Regex(@"\b(?:rua|av\.?|avenida|travessa|alameda|praca|rodovia)\s+[^,\n]{2,60},\s*\d+\w*",
                            RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    /// <summary>
    /// Mascara o texto. Devolve o texto com marcadores e o mapa para remontar.
    ///
    /// O mesmo valor recebe SEMPRE o mesmo marcador dentro de uma carga: o
    /// cliente que repete o telefone em tres falas precisa aparecer como uma
    /// pessoa so, senao o modelo le tres pessoas e o dossie inventa relacao
    /// entre elas.
    /// </summary>
    public static (string Texto, IReadOnlyDictionary<string, string> Mapa) Mascarar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return (texto ?? "", new Dictionary<string, string>());

        var mapa = new Dictionary<string, string>();
        var jaVisto = new Dictionary<string, string>();
        var contador = new Dictionary<string, int>();
        var saida = texto;

        foreach (var (tipo, padrao) in Padroes)
        {
            saida = padrao.Replace(saida, m =>
            {
                var achado = m.Value;

                // CPF e celular tem onze digitos. So o digito verificador
                // desempata, e errar aqui vaza o CPF ou apaga o telefone.
                if (tipo == "CPF" && !EhCpfValido(achado)) return achado;

                if (jaVisto.TryGetValue(achado, out var marcadorAntigo))
                    return marcadorAntigo;

                contador[tipo] = contador.GetValueOrDefault(tipo) + 1;
                var marcador = $"[{tipo}_{contador[tipo]}]";

                jaVisto[achado] = marcador;
                mapa[marcador] = achado;
                return marcador;
            });
        }

        return (saida, mapa);
    }

    /// <summary>
    /// Remonta o texto que voltou do modelo.
    ///
    /// Marcador que o modelo inventou e que nao esta no mapa fica como esta: o
    /// certo e o texto sair com `[TEL_9]` visivel, que denuncia o problema, e
    /// nao com um telefone de outra pessoa no lugar.
    /// </summary>
    public static string Remontar(string? texto, IReadOnlyDictionary<string, string> mapa)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? "";

        foreach (var (marcador, valor) in mapa)
            texto = texto.Replace(marcador, valor);

        return texto;
    }

    /// <summary>Digito verificador. E o que separa CPF de celular.</summary>
    private static bool EhCpfValido(string bruto)
    {
        var d = Regex.Replace(bruto, @"\D", "");
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;

        for (var casa = 9; casa < 11; casa++)
        {
            var soma = 0;
            for (var i = 0; i < casa; i++)
                soma += (d[i] - '0') * (casa + 1 - i);

            var resto = soma * 10 % 11 % 10;
            if (resto != d[casa] - '0') return false;
        }
        return true;
    }
}
