namespace Copiloto.Api.Ingestao;

/// <summary>O que o webhook do WhatsApp entrega, cru, antes de virar dominio.</summary>
/// <param name="ProviderMessageId">
/// O id do PROVEDOR, e nao um Guid nosso: e por ele que a reentrega e
/// reconhecida. O provedor reentrega quando nao ve o 200 a tempo, e sem esse id
/// a mesma fala vira duas — e duas chamadas de IA cobradas.
/// </param>
/// <param name="De">Quem enviou.</param>
/// <param name="Para">Quem recebeu.</param>
/// <remarks>
/// Os dois numeros vem no payload, e o falante sai da COMPARACAO com o numero
/// da empresa — nao de um campo "direcao" que o provedor preenche. Assim a
/// mesma rotina resolve os dois sentidos, e a conversa que o vendedor iniciou
/// nao entra no historico como se o cliente tivesse falado primeiro.
/// </remarks>
public record MensagemRecebida(
    string ProviderMessageId,
    string De,
    string Para,
    string Texto,
    DateTimeOffset EnviadaEm);
