using System.Collections.Concurrent;

namespace Copiloto.Api.Infra;

/// <summary>
/// O estado compartilhado quando ha um processo so — o padrao (#66).
///
/// Vale enquanto a aplicacao roda em uma instancia, que e o caso do
/// desenvolvimento e da demo. Com duas replicas ele passa a MENTIR: cada
/// processo tem o seu, e a idempotencia deixa de valer sem nenhum erro
/// aparecer. E por isso que a interface existe antes de o Redis existir — a
/// troca precisa ser de configuracao, nao de refatoracao com pressa.
/// </summary>
public class InMemoryState : IDistributedState
{
    private record Valor(string Conteudo, DateTimeOffset ExpiraEm);

    private readonly ConcurrentDictionary<string, Valor> _itens = new();
    private readonly Func<DateTimeOffset> _agora;

    /// <param name="agora">
    /// O relogio entra por parametro para o teste poder envelhecer a chave sem
    /// dormir. Suite que espera o TTL passar e suite que fica lenta e depois
    /// fica intermitente.
    /// </param>
    public InMemoryState(Func<DateTimeOffset>? agora = null) =>
        _agora = agora ?? (() => DateTimeOffset.UtcNow);

    public Task<bool> TentarMarcar(string chave, TimeSpan validade, CancellationToken ct)
    {
        var agora = _agora();
        var novo = new Valor("1", agora + validade);

        // AddOrUpdate resolve o vencido e o inexistente no mesmo passo atomico:
        // testar `ContainsKey` antes abriria a janela que a operacao fecha.
        var marcouAgora = false;
        _itens.AddOrUpdate(chave,
            _ => { marcouAgora = true; return novo; },
            (_, antigo) =>
            {
                if (antigo.ExpiraEm > agora) return antigo;

                marcouAgora = true;
                return novo;
            });

        return Task.FromResult(marcouAgora);
    }

    public Task<string?> Ler(string chave, CancellationToken ct)
    {
        if (!_itens.TryGetValue(chave, out var valor)) return Task.FromResult<string?>(null);

        if (valor.ExpiraEm <= _agora())
        {
            _itens.TryRemove(chave, out _);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(valor.Conteudo);
    }

    public Task Gravar(string chave, string valor, TimeSpan validade, CancellationToken ct)
    {
        _itens[chave] = new Valor(valor, _agora() + validade);
        return Task.CompletedTask;
    }

    public Task<long> Incrementar(string chave, TimeSpan janela, CancellationToken ct)
    {
        var agora = _agora();

        var atualizado = _itens.AddOrUpdate(chave,
            _ => new Valor("1", agora + janela),
            (_, antigo) => antigo.ExpiraEm <= agora
                // Janela vencida recomeca do 1 E renova o prazo: manter o
                // vencimento antigo faria o contador expirar no meio da janela
                // nova, e o limite deixaria de significar uma taxa.
                ? new Valor("1", agora + janela)
                : antigo with { Conteudo = (long.Parse(antigo.Conteudo) + 1).ToString() });

        return Task.FromResult(long.Parse(atualizado.Conteudo));
    }
}
