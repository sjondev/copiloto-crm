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
}
