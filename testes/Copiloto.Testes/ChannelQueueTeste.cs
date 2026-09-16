using Copiloto.Api.Ingestao;
using Copiloto.Api.Infra;

namespace Copiloto.Testes;

/// <summary>
/// A fila entre o webhook e o processamento (#40).
///
/// Processar IA dentro do handler e o erro classico: provedor lento vira timeout
/// na origem, timeout vira reentrega, reentrega vira custo DUPLICADO — e o custo
/// aqui e de dinheiro, nao de CPU.
/// </summary>
public class ChannelQueueTeste
{
    private static MensagemRecebida Fala(string id = "wamid.1") =>
        new(id, "+5511987654321", "+5511333334444", "qual o valor do kg?",
            DateTimeOffset.UtcNow);

    /// <summary>
    /// As implementacoes de <see cref="IQueue{T}"/> cobradas pelo contrato.
    /// RabbitMqQueue (#69) entra aqui com UMA linha e passa a responder pelos
    /// mesmos testes — e o que impede a segunda implementacao de nascer com
    /// garantias mais fracas que a primeira sem ninguem notar.
    /// </summary>
    public static TheoryData<string, Func<IQueue<MensagemRecebida>>> Implementacoes => new()
    {
        { "inmemory", () => new ChannelQueue<MensagemRecebida>() },
    };

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task Contrato_entrega_na_ordem_em_que_publicou(
        string nome, Func<IQueue<MensagemRecebida>> criar)
    {
        var fila = criar();
        await fila.Publicar(Fala("um"), default);
        await fila.Publicar(Fala("dois"), default);
        fila.PararDeAceitar();

        var lidas = new List<string>();
        await foreach (var m in fila.Ler(default)) lidas.Add(m.ProviderMessageId);

        Assert.Equal(["um", "dois"], lidas);
        Assert.NotNull(nome);
    }

    [Theory]
    [MemberData(nameof(Implementacoes))]
    public async Task Contrato_depois_de_parar_recusa_trabalho_novo(
        string nome, Func<IQueue<MensagemRecebida>> criar)
    {
        var fila = criar();
        fila.PararDeAceitar();

        Assert.False(await fila.Publicar(Fala("tarde demais"), default));
        Assert.NotNull(nome);
    }

    [Fact]
    public async Task Publicar_entrega_para_quem_le()
    {
        var fila = new ChannelQueue<MensagemRecebida>();

        Assert.True(await fila.Publicar(Fala(), CancellationToken.None));

        await foreach (var m in fila.Ler(CancellationToken.None))
        {
            Assert.Equal("wamid.1", m.ProviderMessageId);
            break;
        }
    }

    [Fact]
    public async Task A_fila_tem_limite()
    {
        // Fila sem teto nao para de crescer: consumo mais lento que a chegada faz
        // a memoria subir ate o processo morrer, e ai a perda e de TUDO.
        var fila = new ChannelQueue<MensagemRecebida>();

        for (var i = 0; i < ChannelQueue<MensagemRecebida>.Capacidade; i++)
            await fila.Publicar(Fala($"wamid.{i}"), CancellationToken.None);

        Assert.Equal(ChannelQueue<MensagemRecebida>.Capacidade, fila.Aguardando);
    }

    [Fact]
    public async Task Fila_cheia_aplica_contrapressao_em_vez_de_descartar()
    {
        // O produtor ESPERA. Descartar em silencio perderia a fala do cliente
        // sem aparecer em lugar nenhum; a espera aparece como latencia.
        var fila = new ChannelQueue<MensagemRecebida>();
        for (var i = 0; i < ChannelQueue<MensagemRecebida>.Capacidade; i++)
            await fila.Publicar(Fala($"wamid.{i}"), CancellationToken.None);

        using var prazo = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var publicou = await fila.Publicar(Fala("estoura"), prazo.Token);

        Assert.False(publicou);
        Assert.Equal(ChannelQueue<MensagemRecebida>.Capacidade, fila.Aguardando);
    }

    [Fact]
    public async Task Desligamento_drena_o_que_ja_estava_na_fila()
    {
        // O criterio de aceite: parar de aceitar nao pode descartar o que entrou.
        var fila = new ChannelQueue<MensagemRecebida>();
        await fila.Publicar(Fala("a"), CancellationToken.None);
        await fila.Publicar(Fala("b"), CancellationToken.None);

        fila.PararDeAceitar();

        var lidas = new List<string>();
        await foreach (var m in fila.Ler(CancellationToken.None))
            lidas.Add(m.ProviderMessageId);

        Assert.Equal(new[] { "a", "b" }, lidas);
    }

    [Fact]
    public async Task Depois_de_parar_nao_aceita_trabalho_novo()
    {
        var fila = new ChannelQueue<MensagemRecebida>();
        fila.PararDeAceitar();

        Assert.False(await fila.Publicar(Fala(), CancellationToken.None));
    }

    [Fact]
    public async Task A_leitura_termina_sozinha_quando_a_fila_fecha_e_esvazia()
    {
        // Sem isto, o desligamento dependeria do timeout do host: o laco ficaria
        // esperando trabalho que nunca chega.
        var fila = new ChannelQueue<MensagemRecebida>();
        await fila.Publicar(Fala(), CancellationToken.None);
        fila.PararDeAceitar();

        var consumo = Task.Run(async () =>
        {
            var n = 0;
            await foreach (var _ in fila.Ler(CancellationToken.None)) n++;
            return n;
        });

        var terminou = await Task.WhenAny(consumo, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(consumo, terminou);
        Assert.Equal(1, await consumo);
    }
}
