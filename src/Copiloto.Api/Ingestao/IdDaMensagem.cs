using System.Security.Cryptography;
using System.Text;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// O id de casa derivado do id do provedor (#18, #158).
///
/// A `Mensagem` tem chave `Guid` e o provedor manda `string` — e o caminho
/// obvio, gerar `Guid.NewGuid()` na hora de salvar, quebra a idempotencia
/// inteira: o WhatsApp reentrega quando nao ve o 200 a tempo, e a mesma fala
/// entraria no banco duas vezes com ids diferentes. Duas falas iguais na tela,
/// e mais adiante duas chamadas de IA cobradas pela mesma coisa.
///
/// Derivando, a reentrega chega com o MESMO id e a chave primaria recusa
/// sozinha. O hash aqui nao e seguranca, e derivacao determinista: SHA-256 e
/// nao MD5 porque um scanner de segredo nao tem como saber a diferenca, e a
/// discussao que isso abriria custa mais que os bytes que economiza.
/// </summary>
public static class IdDaMensagem
{
    public static Guid De(string providerMessageId)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            throw new ArgumentException(
                "Sem id do provedor nao ha como reconhecer reentrega.", nameof(providerMessageId));

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(providerMessageId.Trim()));
        return new Guid(digest.AsSpan(0, 16));
    }
}
