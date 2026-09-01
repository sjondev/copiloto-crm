using System.Text.Json;
using System.Text.Json.Serialization;

namespace Copiloto.Api.Ingestao;

/// <summary>Uma conversa do seed, como ela esta no JSON.</summary>
public record ConversaGravada(
    string Id,
    string Titulo,
    Participante Empresa,
    Participante Cliente,
    IReadOnlyList<MensagemGravada> Mensagens)
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        // O seed tem `_comentario` explicando as decisoes de cada roteiro, e
        // comentario em arquivo de dado e o que impede o proximo a mexer de
        // desfazer a escolha sem saber que era escolha.
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static ConversaGravada Ler(string json) =>
        JsonSerializer.Deserialize<ConversaGravada>(json, Opcoes)
        ?? throw new InvalidOperationException("conversa gravada vazia");
}

public record Participante(string? Nome, string Telefone);

public record MensagemGravada(string De, int OffsetSegundos, string Texto, string? Tipo = null)
{
    public bool DoCliente => De.Equals("cliente", StringComparison.OrdinalIgnoreCase);
}
