using System.Text.Json;
using Copiloto.Dominio.Conversas;

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

public record MensagemGravada(
    string De, int OffsetSegundos, string Texto, string? Tipo = null, int? DuracaoSegundos = null)
{
    public bool DoCliente => De.Equals("cliente", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// O anexo do roteiro, quando a fala gravada nao e texto (#23).
    ///
    /// O seed ja marcava `"tipo": "audio"` e o replay descartava — que e
    /// exatamente o "fingir que a midia nao existe" que a #23 existe para
    /// acabar.
    /// </summary>
    public Midia? Midia => Copiloto.Dominio.Conversas.Midia.PeloNome(
        Tipo, DuracaoSegundos is { } s ? TimeSpan.FromSeconds(s) : null);
}
