namespace Copiloto.Api.Auth;

/// <summary>
/// Hash e conferencia de senha (#49).
///
/// BCrypt, e nao MD5 nem SHA-1: os dois ultimos sao RAPIDOS, e velocidade e
/// defeito aqui. Uma GPU testa bilhoes de MD5 por segundo, entao uma base
/// vazada com MD5 esta quebrada no mesmo fim de semana; com BCrypt no custo
/// certo, cada tentativa custa tempo tambem para quem ataca.
///
/// Fica na Api porque o dominio nao tem PackageReference (#48). O que o
/// dominio garante e o que ele consegue garantir sem pacote: que o hash
/// guardado tem tamanho de hash moderno.
/// </summary>
public static class Senhas
{
    /// <summary>
    /// Custo do BCrypt. Cada +1 DOBRA o tempo.
    ///
    /// 12 e o ponto em que o login ainda parece instantaneo para quem digita a
    /// senha e ja e caro para quem testa listas. Baixar isto para "acelerar o
    /// login" e o tipo de otimizacao que so aparece depois do vazamento.
    /// </summary>
    public const int Custo = 12;

    public static string Hash(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha))
            throw new ArgumentException("Senha vazia nao vira hash.", nameof(senha));

        return BCrypt.Net.BCrypt.HashPassword(senha, Custo);
    }

    /// <summary>
    /// Confere. Devolve false em vez de lancar quando o hash guardado esta
    /// corrompido: a resposta certa para "essa senha vale?" continua sendo
    /// "nao", e uma excecao aqui viraria erro 500 no login — que conta ao
    /// atacante que aquele usuario existe e tem algo diferente.
    /// </summary>
    public static bool Confere(string senha, string hash)
    {
        if (string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(hash)) return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
