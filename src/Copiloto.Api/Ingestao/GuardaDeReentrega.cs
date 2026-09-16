using Copiloto.Api.Infra;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// Decide se uma mensagem ja foi processada, valendo para TODAS as instancias
/// (#67).
///
/// O webhook do WhatsApp reentrega. Com o registro dentro do processo, a
/// reentrega cai na outra instancia, que nunca viu aquele id — e a mensagem e
/// analisada de novo e COBRADA de novo. A garantia de "nao pagar duas vezes
/// pelo mesmo clique" passa a ter uma restricao de implantacao nao declarada:
/// so vale em processo unico. E o tipo de premissa que ninguem escreve e que
/// quebra no primeiro deploy com replica.
/// </summary>
public class GuardaDeReentrega
{
    /// <summary>
    /// Por quanto tempo o id fica marcado.
    ///
    /// 24h espelha a janela de atendimento do WhatsApp (ARQUITETURA 13.2, essa
    /// sim verificada na fonte): fora dela a conversa muda de regime, e uma
    /// reentrega tao tardia interessa menos que o custo de guardar id para
    /// sempre.
    ///
    /// O prazo REAL de reentrega do webhook nao foi verificado, e por isso o
    /// valor e configuravel em vez de constante — quando alguem conferir na
    /// fonte, muda a variavel e nao o codigo. Marcar isso como se fosse fato
    /// conferido seria arquitetar em cima de memoria, que e exatamente o que a
    /// #26 mostrou custar caro.
    /// </summary>
    public static readonly TimeSpan JanelaPadrao = TimeSpan.FromHours(24);

    private readonly IDistributedState _estado;
    private readonly TimeSpan _janela;

    public GuardaDeReentrega(IDistributedState estado, TimeSpan? janela = null)
    {
        ArgumentNullException.ThrowIfNull(estado);

        _estado = estado;
        _janela = janela ?? JanelaPadrao;
    }

    /// <summary>
    /// True quando ESTA chamada foi a que marcou o id — e so ela processa.
    ///
    /// A decisao e uma operacao atomica so, e nao ler-decidir-gravar: entre a
    /// leitura e a gravacao cabe a outra instancia lendo "nao existe", e as
    /// duas processariam a mesma mensagem. A janela e pequena e o custo dela e
    /// dinheiro.
    /// </summary>
    public Task<bool> EhAPrimeiraVez(string providerMessageId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            throw new ArgumentException(
                "Sem ProviderMessageId nao ha o que deduplicar: qualquer chave que "
                + "eu inventasse aqui marcaria mensagens diferentes como a mesma.",
                nameof(providerMessageId));

        return _estado.TentarMarcar(Chave(providerMessageId), _janela, ct);
    }

    /// <summary>
    /// O prefixo evita colisao com as outras chaves do mesmo Redis — rate
    /// limit, circuit breaker e cache de analise dividem o espaco (#68, #71).
    /// </summary>
    public static string Chave(string providerMessageId) =>
        $"ingestao:msg:{providerMessageId.Trim()}";
}
