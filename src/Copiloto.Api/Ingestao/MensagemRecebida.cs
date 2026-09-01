namespace Copiloto.Api.Ingestao;

/// <summary>O que o webhook do WhatsApp entrega, cru, antes de virar dominio.</summary>
/// <param name="ProviderMessageId">
/// O id do PROVEDOR, e nao um Guid nosso: e por ele que a reentrega e
/// reconhecida. O provedor reentrega quando nao ve o 200 a tempo, e sem esse id
/// a mesma fala vira duas — e duas chamadas de IA cobradas.
/// </param>
public record MensagemRecebida(
    string ProviderMessageId,
    string Telefone,
    string Texto,
    DateTimeOffset EnviadaEm);
