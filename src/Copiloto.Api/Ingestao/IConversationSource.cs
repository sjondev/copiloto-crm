namespace Copiloto.Api.Ingestao;

/// <summary>
/// A porta de entrada de conversa (#17).
///
/// Cada provedor entrega o seu proprio envelope: a Cloud API manda um lote
/// aninhado em `entry[].changes[].value.messages[]`, o WAHA manda o evento do
/// WhatsApp Web, e o seed manda a fala ja no formato de casa. A fonte e o unico
/// lugar do sistema que conhece esse formato — dali para dentro tudo e
/// <see cref="MensagemRecebida"/>.
///
/// E Ports and Adapters com motivo real, nao porque o livro mandou: as tres
/// implementacoes existem de verdade, e trocar entre elas e trocar
/// `CONVERSATION_SOURCE`, sem recompilar.
/// </summary>
public interface IConversationSource
{
    /// <summary>O valor de `CONVERSATION_SOURCE` que seleciona esta fonte.</summary>
    string Nome { get; }

    /// <summary>
    /// Traduz o corpo cru do provedor nas falas que ele carrega.
    ///
    /// Devolve LISTA e nao uma fala so porque webhook de WhatsApp entrega em
    /// lote: seis baloes digitados em quatro segundos chegam num payload unico,
    /// e quem recebe uma de cada vez perde cinco sem erro nenhum aparecer.
    ///
    /// Payload que o provedor manda e nao carrega fala — confirmacao de leitura,
    /// mudanca de status, evento de sistema — devolve lista vazia. Nao e erro:
    /// e a maior parte do trafego real.
    /// </summary>
    IReadOnlyList<MensagemRecebida> Traduzir(string corpo);
}
