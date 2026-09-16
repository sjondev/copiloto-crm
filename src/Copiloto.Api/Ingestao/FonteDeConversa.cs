namespace Copiloto.Api.Ingestao;

/// <summary>
/// Escolhe a fonte pela configuracao (#17).
/// </summary>
public static class FonteDeConversa
{
    public const string Chave = "CONVERSATION_SOURCE";
    public const string Padrao = "fake";

    /// <summary>
    /// A fonte que `CONVERSATION_SOURCE` pediu.
    ///
    /// Ausente vira <c>fake</c>: a suite e a demo rodam offline e de graca, e
    /// um clone novo nao precisa de credencial para subir.
    ///
    /// Nome que nao existe DERRUBA a subida, em vez de cair no fake. Cair no
    /// fake seria o pior desfecho possivel em producao: a API sobe saudavel, o
    /// `/saude` responde ok, e as falas dos clientes simplesmente nunca chegam —
    /// sem log de erro, sem excecao, sem ninguem para notar antes do fim do mes.
    /// </summary>
    public static IConversationSource Escolher(IConfiguration configuracao)
    {
        var pedida = configuracao[Chave];
        if (string.IsNullOrWhiteSpace(pedida)) pedida = Padrao;

        return pedida.Trim().ToLowerInvariant() switch
        {
            "fake" => new FakeSource(),
            "waha" => throw new NotSupportedException(NaoImplementada("waha", 149)),
            "cloudapi" => throw new NotSupportedException(NaoImplementada("cloudapi", 150)),
            _ => throw new ArgumentException(
                $"{Chave}='{pedida}' nao existe. Fontes: fake, waha, cloudapi. "
                + "A subida para aqui de proposito: cair no fake por erro de "
                + "digitacao daria uma API saudavel que nunca recebe conversa.",
                nameof(configuracao)),
        };
    }

    private static string NaoImplementada(string nome, int issue) =>
        $"{Chave}='{nome}' ainda nao tem adaptador. O contrato e o "
        + $"{nameof(IConversationSource)} (#{issue}); o formato do payload e da "
        + "assinatura precisa ser confirmado na doc oficial antes de virar codigo.";
}
