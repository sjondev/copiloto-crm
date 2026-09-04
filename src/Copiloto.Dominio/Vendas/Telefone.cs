using System.Globalization;
using System.Text.RegularExpressions;

namespace Copiloto.Dominio.Vendas;

/// <summary>
/// Telefone brasileiro na forma canonica `+55DDNNNNNNNNN`.
///
/// Normalizacao de numero brasileiro e fonte classica de bug, e o estrago e
/// especifico: o mesmo cliente vira DOIS leads e o historico se parte no meio —
/// o vendedor abre a conversa e nao ve o que foi combinado semana passada.
///
/// O caso que causa isso e o NONO DIGITO. Celular ganhou o 9 na frente em 2016,
/// e ate hoje chega numero dos dois jeitos: agenda velha, cadastro antigo,
/// etiqueta de loja. `11 8765-4321` e `11 98765-4321` sao a mesma pessoa, e
/// comparar string crua diz que nao sao.
///
/// A regra do nono digito vale para CELULAR, e celular no Brasil comeca com
/// 6, 7, 8 ou 9. Fixo comeca com 2, 3, 4 ou 5 e continua com oito digitos —
/// enfiar um 9 nele criaria um numero que nao existe.
/// </summary>
public sealed class Telefone : IEquatable<Telefone>
{
    private const string Ddi = "55";

    private Telefone(string e164) => E164 = e164;

    /// <summary>A forma canonica: `+55` + DDD + assinante.</summary>
    public string E164 { get; }

    public string Ddd => E164.Substring(3, 2);
    public string Assinante => E164[5..];
    public bool EhCelular => Assinante.Length == 9;

    /// <summary>
    /// Normaliza. Devolve null quando o que chegou nao e telefone brasileiro
    /// reconhecivel — e nao um Telefone com dado torto, que so adiaria o erro.
    /// </summary>
    public static Telefone? Normalizar(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        var digitos = Regex.Replace(bruto, @"\D", "");

        // DDI opcional. `550` nao e DDI seguido de DDD: nao existe DDD com zero
        // na frente, entao ali o 55 e o proprio DDD (Rio Grande do Sul).
        if (digitos.Length is 12 or 13 && digitos.StartsWith(Ddi, StringComparison.Ordinal) && digitos[2] != '0')
            digitos = digitos[2..];

        if (digitos.Length is not (10 or 11)) return null;

        var ddd = digitos[..2];
        var assinante = digitos[2..];

        // DDD brasileiro vai de 11 a 99, e nenhum comeca com 0 ou 1 no segundo
        // digito abaixo de 11.
        if (int.Parse(ddd, CultureInfo.InvariantCulture) < 11) return null;

        if (assinante.Length == 8)
        {
            // Celular antigo (comeca com 6-9) ganha o nono digito. Fixo nao.
            if (assinante[0] >= '6') assinante = "9" + assinante;
            else if (assinante[0] < '2') return null;
        }
        else if (assinante[0] != '9')
        {
            // Nove digitos que nao comeca com 9 nao e celular valido.
            return null;
        }

        return new Telefone($"+{Ddi}{ddd}{assinante}");
    }

    public bool Equals(Telefone? outro) => outro is not null && E164 == outro.E164;
    public override bool Equals(object? obj) => Equals(obj as Telefone);
    public override int GetHashCode() => E164.GetHashCode();
    public override string ToString() => E164;
}
