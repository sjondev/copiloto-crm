using Copiloto.Dominio.Conversas;

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
/// <param name="Midia">
/// O anexo, quando a fala nao e texto (#23). A Fase 1 nao transcreve nem
/// interpreta nada disso — mas descartar o TIPO na porta de entrada e o que
/// faria o dossie ler a conversa pela metade sem saber que era pela metade.
/// </param>
public record MensagemRecebida(
    string ProviderMessageId,
    string De,
    string Para,
    string Texto,
    DateTimeOffset EnviadaEm,
    Midia? Midia = null)
{
    /// <summary>
    /// O que impede a fala de entrar na fila, ou <c>null</c> se nada impede.
    ///
    /// A regra e do NUCLEO e nao do endpoint: vale igual para a fala que veio
    /// do webhook da Meta, do WAHA ou do seed, e cada fonte nova herdaria a
    /// checagem de graca — ou esqueceria dela, se ela morasse no handler.
    /// </summary>
    public string? PorQueNaoEntra() =>
        string.IsNullOrWhiteSpace(ProviderMessageId)
            ? "sem ProviderMessageId: a reentrega nao teria como ser reconhecida"
            : string.IsNullOrWhiteSpace(De) || string.IsNullOrWhiteSpace(Para)
                ? "sem De/Para: nao ha como dizer quem falou"
                : string.IsNullOrWhiteSpace(Texto) && Midia is null
                    ? "sem texto e sem midia: nao ha fala nenhuma nisso"
                    : null;
}
