using System.Collections.Concurrent;
using System.Globalization;

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
    private sealed record Valor(string Conteudo, DateTimeOffset ExpiraEm);

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

        // NAO usar AddOrUpdate aqui. Ele nao garante que a fabrica rode uma vez
        // so: sob concorrencia, duas chamadas executam a fabrica de insercao, e
        // cada uma marcaria o seu proprio "fui eu" — so uma grava, mas as duas
        // devolvem true. Com 50 entregas simultaneas isso aparece como duas
        // vencedoras, e em producao como a cobranca dupla que a #67 existe para
        // impedir.
        //
        // TryAdd e TryUpdate sao comparacao-e-troca de verdade: exatamente um
        // chamador ganha, e quem perde descobre pelo retorno.
        while (true)
        {
            if (_itens.TryAdd(chave, novo)) return Task.FromResult(true);

            // Entre o TryAdd e a leitura a chave pode ter sido removida por um
            // Ler que a viu vencida. Tentar de novo e' o certo: o estado mudou.
            if (!_itens.TryGetValue(chave, out var existente)) continue;

            if (existente.ExpiraEm > agora) return Task.FromResult(false);

            // Vencida: so quem trocar ESTA versao ganha. Quem perder a troca
            // volta ao laco e vai encontrar a marcacao do vencedor.
            if (_itens.TryUpdate(chave, novo, existente)) return Task.FromResult(true);
        }
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
                : antigo with { Conteudo = (long.Parse(antigo.Conteudo, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) });

        return Task.FromResult(long.Parse(atualizado.Conteudo, CultureInfo.InvariantCulture));
    }
}
