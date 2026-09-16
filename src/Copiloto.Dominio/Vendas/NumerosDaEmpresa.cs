namespace Copiloto.Dominio.Vendas;

/// <summary>
/// Os numeros por onde a empresa fala, e de quem e cada um (#175).
///
/// Existe porque o produto conecta um aparelho POR VENDEDOR — um QR por celular
/// na rota WAHA, um `phone_number_id` por numero na Cloud API (ARCH-010). Ate
/// aqui o resolvedor conhecia um numero so, e decidia quem falou por uma
/// comparacao unica: com dois aparelhos, a fala do segundo vendedor caia como
/// Cliente e o dossie lia a conversa ao contrario.
///
/// O dono e OPCIONAL, e isso segue a regra da #49: numero sem dono declarado
/// deixa o lead com a equipe, e "da equipe" nao e o mesmo que "de ninguem".
/// </summary>
public sealed class NumerosDaEmpresa
{
    private readonly Dictionary<Telefone, Guid?> _donoPorNumero;

    private NumerosDaEmpresa(IEnumerable<(string Numero, Guid? VendedorId)> numeros)
    {
        ArgumentNullException.ThrowIfNull(numeros);

        _donoPorNumero = new Dictionary<Telefone, Guid?>();

        foreach (var (bruto, vendedorId) in numeros)
        {
            // Numero torto aqui nunca casaria com fala nenhuma, e o sintoma
            // seria toda mensagem daquele aparelho virando fala do cliente —
            // silenciosamente, e so no dossie. Recusar na construcao troca um
            // defeito de leitura por um erro de subida.
            var telefone = Telefone.Normalizar(bruto)
                ?? throw new ArgumentException(
                    $"'{bruto}' nao e um telefone brasileiro valido. E por estes numeros "
                    + "que o sistema decide quem falou em cada mensagem.", nameof(numeros));

            // O ultimo a declarar vence: repetir o numero com dono e o jeito de
            // corrigir uma lista que veio sem ele.
            _donoPorNumero[telefone] = vendedorId;
        }

        if (_donoPorNumero.Count == 0)
            throw new ArgumentException(
                "Sem nenhum numero da empresa nao ha como saber quem falou: toda fala "
                + "viraria do cliente, inclusive a do vendedor.", nameof(numeros));
    }

    /// <summary>Os numeros, sem dizer de quem sao.</summary>
    public static NumerosDaEmpresa De(params string[] numeros) =>
        new((numeros ?? []).Select(n => (n, (Guid?)null)));

    /// <summary>Os numeros com o vendedor que atende em cada um.</summary>
    public static NumerosDaEmpresa ComDono(params (string Numero, Guid VendedorId)[] numeros) =>
        new((numeros ?? []).Select(n => (n.Numero, (Guid?)n.VendedorId)));

    /// <summary>Quantos aparelhos distintos. O mesmo numero escrito de dois jeitos e um so.</summary>
    public int Quantidade => _donoPorNumero.Count;

    public bool Contem(Telefone telefone) =>
        telefone is not null && _donoPorNumero.ContainsKey(telefone);

    /// <summary>
    /// O vendedor que atende naquele aparelho, ou <c>null</c> quando o numero
    /// nao tem dono declarado — ou quando nem e nosso.
    /// </summary>
    public Guid? DonoDe(Telefone telefone) =>
        telefone is not null && _donoPorNumero.TryGetValue(telefone, out var dono) ? dono : null;
}
