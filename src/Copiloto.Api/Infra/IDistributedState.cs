namespace Copiloto.Api.Infra;

/// <summary>
/// Estado que precisa valer para TODAS as instancias, nao so para esta (#66).
///
/// Sao tres usos, cada um com issue propria, e os tres quebram do mesmo jeito
/// com duas replicas atras de um balanceador: idempotencia de webhook (#67),
/// estado do circuit breaker (#68) e rate limit com cache de analise (#71). Com
/// o estado no processo, a segunda instancia nao sabe que a primeira ja pagou
/// pela analise, ja abriu o circuito ou ja atendeu aquela mensagem.
///
/// O contrato tem quatro operacoes porque sao essas que os tres usos pedem.
/// Redis sabe fazer muito mais, e o resto ficaria sem equivalente em memoria.
/// </summary>
public interface IDistributedState
{
    /// <summary>
    /// Marca a chave se ela ainda nao existir, e diz se a marcacao foi SUA.
    ///
    /// E a operacao da idempotencia (#67), e ela precisa ser atomica: ler,
    /// decidir e gravar em tres passos deixa a janela em que duas instancias
    /// leem "nao existe" e as duas processam a mesma mensagem — que e
    /// exatamente o que a idempotencia existe para impedir.
    /// </summary>
    Task<bool> TentarMarcar(string chave, TimeSpan validade, CancellationToken ct);

    Task<string?> Ler(string chave, CancellationToken ct);

    Task Gravar(string chave, string valor, TimeSpan validade, CancellationToken ct);

    /// <summary>
    /// Soma um a um contador que expira sozinho, e devolve o total. E o rate
    /// limit (#71): sem a janela, o contador so cresce e o limite vira um teto
    /// permanente em vez de uma taxa.
    /// </summary>
    Task<long> Incrementar(string chave, TimeSpan janela, CancellationToken ct);
}
