using Copiloto.Api.Infra;
using Microsoft.Extensions.Configuration;

namespace Copiloto.Testes;

/// <summary>
/// O contrato de <see cref="IDistributedState"/>, rodado contra CADA
/// implementacao (#66).
///
/// A lista `Implementacoes` e o ponto da suite: quando o RedisState existir
/// (#70), ele entra com UMA linha aqui e passa a ser cobrado pelos mesmos
/// testes. Suite escrita contra a implementacao, e nao contra o contrato,
/// precisaria ser reescrita — e reescrita sob pressao de "so falta o Redis"
/// vira suite mais fraca.
/// </summary>
public class EstadoCompartilhadoTeste
{
    public static TheoryData<string, Func<IDistributedState>> Implementacoes => new()
    {
        { "inmemory", () => new InMemoryState() },
    };

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task Quem_marca_primeiro_ganha(string nome, Func<IDistributedState> criar)
    {
        // A operacao da idempotencia (#67): a segunda instancia precisa
        // descobrir que a primeira ja pegou aquela mensagem.
        var estado = criar();
        var chave = $"webhook:{Guid.NewGuid()}";

        Assert.True(await estado.TentarMarcar(chave, TimeSpan.FromMinutes(5), default));
        Assert.False(await estado.TentarMarcar(chave, TimeSpan.FromMinutes(5), default));
        Assert.NotNull(nome);
    }

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task So_um_entre_muitos_concorrentes_marca(string nome, Func<IDistributedState> criar)
    {
        // Ler-decidir-gravar em tres passos deixaria a janela em que dois leem
        // "nao existe" e os dois processam a mesma mensagem.
        var estado = criar();
        var chave = $"corrida:{Guid.NewGuid()}";

        var tentativas = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => estado.TentarMarcar(chave, TimeSpan.FromMinutes(5), default))));

        Assert.Single(tentativas, ganhou => ganhou);
        Assert.NotNull(nome);
    }

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task Le_de_volta_o_que_gravou(string nome, Func<IDistributedState> criar)
    {
        var estado = criar();

        await estado.Gravar("analise:42", "resultado", TimeSpan.FromMinutes(5), default);

        Assert.Equal("resultado", await estado.Ler("analise:42", default));
        Assert.Null(await estado.Ler("analise:inexistente", default));
        Assert.NotNull(nome);
    }

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task O_contador_soma_e_devolve_o_total(string nome, Func<IDistributedState> criar)
    {
        var estado = criar();
        var chave = $"rate:{Guid.NewGuid()}";

        Assert.Equal(1, await estado.Incrementar(chave, TimeSpan.FromMinutes(1), default));
        Assert.Equal(2, await estado.Incrementar(chave, TimeSpan.FromMinutes(1), default));
        Assert.NotNull(nome);
    }

    // --- Expiracao: so no InMemoryState, com o relogio na mao ---

    [Fact]
    public async Task A_marca_vencida_libera_a_chave()
    {
        // Relogio injetado: suite que espera o TTL passar fica lenta e depois
        // fica intermitente, que e' o jeito de um teste deixar de ser lido.
        var agora = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var estado = new InMemoryState(() => agora);

        Assert.True(await estado.TentarMarcar("k", TimeSpan.FromMinutes(5), default));

        agora = agora.AddMinutes(6);

        Assert.True(await estado.TentarMarcar("k", TimeSpan.FromMinutes(5), default));
    }

    [Fact]
    public async Task O_valor_vencido_some_da_leitura()
    {
        var agora = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var estado = new InMemoryState(() => agora);
        await estado.Gravar("k", "v", TimeSpan.FromMinutes(1), default);

        agora = agora.AddMinutes(2);

        Assert.Null(await estado.Ler("k", default));
    }

    [Fact]
    public async Task A_janela_vencida_recomeca_do_um_e_renova_o_prazo()
    {
        // Manter o vencimento antigo faria o contador expirar no meio da janela
        // nova, e o limite deixaria de significar uma taxa.
        var agora = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var estado = new InMemoryState(() => agora);
        var janela = TimeSpan.FromMinutes(1);

        await estado.Incrementar("r", janela, default);
        await estado.Incrementar("r", janela, default);

        agora = agora.AddMinutes(2);
        Assert.Equal(1, await estado.Incrementar("r", janela, default));

        agora = agora.AddSeconds(30);
        Assert.Equal(2, await estado.Incrementar("r", janela, default));
    }

    // --- A escolha por variavel de ambiente ---

    private static IConfiguration Com(params (string Chave, string Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            valores.Select(v => new KeyValuePair<string, string?>(v.Chave, v.Valor))).Build();

    [Fact]
    public void Sem_variavel_nenhuma_a_aplicacao_sobe_em_memoria()
    {
        // O criterio que protege a demo: sem broker e sem cache, tudo de pe.
        var vazia = Com();

        Assert.IsType<InMemoryState>(Backends.Estado(vazia));
        Assert.IsType<ChannelQueue<string>>(Backends.Fila<string>(vazia));
    }

    [Fact]
    public void Backend_previsto_e_sem_corpo_derruba_a_subida()
    {
        // Cair para memoria em silencio daria uma aplicacao que PARECE
        // distribuida, roda com duas replicas e perde idempotencia sem sinal.
        var erro = Assert.Throws<NotSupportedException>(
            () => Backends.Estado(Com(("STATE_BACKEND", "redis"))));

        Assert.Contains("#70", erro.Message);
        Assert.Throws<NotSupportedException>(
            () => Backends.Fila<string>(Com(("QUEUE_BACKEND", "rabbitmq"))));
    }

    [Fact]
    public void Valor_escrito_errado_diz_quais_existem()
    {
        var erro = Assert.Throws<ArgumentException>(
            () => Backends.Estado(Com(("STATE_BACKEND", "reddis"))));

        Assert.Contains("inmemory, redis", erro.Message);
    }

    [Fact]
    public void A_variavel_nao_e_sensivel_a_caixa_nem_a_espaco()
    {
        // Valor de .env chega com espaco e com maiuscula mais vezes do que se
        // imagina, e derrubar a subida por isso seria pedantismo caro.
        Assert.IsType<InMemoryState>(Backends.Estado(Com(("STATE_BACKEND", " InMemory "))));
    }
}
