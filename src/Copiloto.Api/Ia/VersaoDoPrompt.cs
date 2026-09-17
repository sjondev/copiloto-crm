using System.Security.Cryptography;
using System.Text;

namespace Copiloto.Api.Ia;

/// <summary>
/// A versao de um prompt, derivada do proprio conteudo (#51).
///
/// Nao e um numero que alguem incrementa e esquece de incrementar. O hash muda
/// quando o texto muda, e so quando ele muda — entao responde sem depender de
/// disciplina a unica pergunta que interessa na auditoria: "esta sugestao saiu
/// do mesmo prompt daquela?".
///
/// Doze caracteres. Nao e criptografia, e identificacao: a chance de duas
/// versoes distintas colidirem em 48 bits, num repositorio com dezenas de
/// prompts ao longo de anos, e menor que a de alguem errar o numero manual que
/// isto substitui.
/// </summary>
public static class VersaoDoPrompt
{
    public const int Tamanho = 12;

    public static string De(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "sem-prompt";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(prompt));

        return Convert.ToHexStringLower(bytes)[..Tamanho];
    }
}
