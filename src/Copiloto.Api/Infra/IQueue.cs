namespace Copiloto.Api.Infra;

/// <summary>
/// A fila de trabalho, sem dizer quem esta atras dela (#66).
///
/// Nasce com duas implementacoes reais previstas — `ChannelQueue` em processo
/// e RabbitMQ na #69 —, entao nao e abstracao prematura: e a excecao que o
/// CLAUDE.md ja registrou.
///
/// O contrato e deliberadamente pequeno. Tudo que um broker sabe fazer e que
/// nao cabe aqui (roteamento, prioridade, reentrega com atraso) ficaria sem
/// equivalente em processo, e uma interface que so a metade das implementacoes
/// honra nao abstrai nada.
/// </summary>
public interface IQueue<T>
{
    /// <summary>Itens esperando. Aproximado num broker; exato em memoria.</summary>
    int Aguardando { get; }

    /// <summary>
    /// Enfileira. Devolve false quando nao deu — fila cheia com espera
    /// estourada, ou desligamento em andamento. Quem chama decide o que
    /// responder ao provedor: o webhook responde 503 para o WhatsApp
    /// REENTREGAR, porque 200 com a fila cheia perderia a fala do cliente em
    /// silencio.
    /// </summary>
    Task<bool> Publicar(T item, CancellationToken ct);

    IAsyncEnumerable<T> Ler(CancellationToken ct);

    /// <summary>
    /// Fecha para escrita, mantendo a leitura do que ficou. E o que faz o
    /// desligamento DRENAR em vez de descartar.
    /// </summary>
    void PararDeAceitar();
}
