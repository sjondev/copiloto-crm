namespace Copiloto.Api.Infra;

/// <summary>
/// Escolhe a implementacao pela variavel de ambiente (#66).
///
/// `STATE_BACKEND` e `QUEUE_BACKEND`, com `inmemory` como padrao: sem .env, sem
/// broker e sem cache, a aplicacao sobe inteira. E o mesmo principio do
/// `CONVERSATION_SOURCE=fake` — demonstracao que depende de cinco conteineres
/// no ar tem cinco maneiras de falhar ao vivo.
/// </summary>
public static class Backends
{
    public const string Padrao = "inmemory";

    public static IQueue<T> Fila<T>(IConfiguration configuracao) =>
        Escolhido(configuracao, "QUEUE_BACKEND") switch
        {
            Padrao => new ChannelQueue<T>(),
            "rabbitmq" => throw NaoImplementado("QUEUE_BACKEND", "rabbitmq", 69),
            var outro => throw Desconhecido("QUEUE_BACKEND", outro, "inmemory, rabbitmq"),
        };

    public static IDistributedState Estado(IConfiguration configuracao) =>
        Escolhido(configuracao, "STATE_BACKEND") switch
        {
            Padrao => new InMemoryState(),
            "redis" => throw NaoImplementado("STATE_BACKEND", "redis", 70),
            var outro => throw Desconhecido("STATE_BACKEND", outro, "inmemory, redis"),
        };

    private static string Escolhido(IConfiguration configuracao, string variavel) =>
        (configuracao[variavel] ?? Padrao).Trim().ToLowerInvariant();

    /// <summary>
    /// Backend previsto que ainda nao tem corpo. Falhar na SUBIDA e a escolha
    /// certa: cair para memoria em silencio daria uma aplicacao que parece
    /// distribuida, roda com duas replicas e perde idempotencia sem nenhum
    /// sinal — o modo de falhar mais caro que existe aqui.
    /// </summary>
    private static NotSupportedException NaoImplementado(
        string variavel, string valor, int issue) =>
        new($"{variavel}={valor} ainda nao tem implementacao (issue #{issue}). "
            + $"Use {variavel}={Padrao} ou implemente o backend — cair para memoria "
            + "em silencio esconderia justamente a perda de garantia.");

    private static ArgumentException Desconhecido(
        string variavel, string valor, string aceitos) =>
        new($"{variavel}={valor} nao existe. Valores aceitos: {aceitos}.");
}
