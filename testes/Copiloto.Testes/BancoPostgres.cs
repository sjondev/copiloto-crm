namespace Copiloto.Testes;

/// <summary>
/// As classes que usam o Postgres de verdade rodam UMA POR VEZ.
///
/// Cada uma comeca com `EnsureDeleted` + `Migrate` para nascer num banco limpo,
/// e o xUnit roda classes em paralelo por padrao: duas delas ao mesmo tempo
/// apagam o banco uma da outra, e a falha aparece como teste intermitente longe
/// da causa. Foi o que aconteceu quando a segunda classe apareceu.
///
/// Serializar so vale para quem depende do banco compartilhado. O resto da
/// suite continua em paralelo, que e o que a mantem em segundos.
/// </summary>
[CollectionDefinition(Nome)]
public class BancoPostgres
{
    public const string Nome = "postgres";

    /// <summary>
    /// A cadeia de conexao dos testes, apontando para um banco PROPRIO.
    ///
    /// Estes testes comecam com `EnsureDeleted`, e isso apaga o banco que a
    /// variavel apontar. Apontando POSTGRES_URL para o banco de desenvolvimento
    /// — o que e a coisa mais natural do mundo, porque e o que ja esta de pe —,
    /// rodar a suite destroi os dados de trabalho sem perguntar nada. Aconteceu
    /// duas vezes no dia em que estes testes nasceram.
    ///
    /// O sufixo resolve sem exigir disciplina de quem roda: mesmo servidor,
    /// mesma credencial, banco separado. `EnsureDeleted` e `Migrate` criam o
    /// banco sozinhos, entao nao ha passo manual.
    /// </summary>
    public const string Sufixo = "_teste";

    public static string? Cadeia()
    {
        var url = Environment.GetEnvironmentVariable("POSTGRES_URL");
        if (string.IsNullOrWhiteSpace(url)) return null;

        var partes = url.Split(';', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < partes.Length; i++)
        {
            var parte = partes[i].Trim();
            if (!parte.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)) continue;

            var banco = parte["Database=".Length..];

            // Ja tem o sufixo: nao empilhar `_teste_teste` quando alguem apontar
            // a variavel direto para o banco de teste.
            if (banco.EndsWith(Sufixo, StringComparison.Ordinal)) return url;

            partes[i] = $"Database={banco}{Sufixo}";
            return string.Join(';', partes);
        }

        return $"{url};Database=copiloto{Sufixo}";
    }
}
