using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Xunit;

namespace Copiloto.Testes;

/// <summary>
/// A fila duravel contra um RabbitMQ de verdade (#69).
///
/// Os testes PULAM quando nao ha broker, e nao passam: teste que passa por nao
/// ter rodado e pior que teste vermelho — ele deixa a suite verde e a garantia
/// vazia. Para rodar:
///
///   docker run -d --rm -p 5673:5672 rabbitmq:4-alpine
///   RABBITMQ_URL=amqp://guest:guest@localhost:5673 dotnet test
/// </summary>
public class FilaDuravelTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private static string? Url => Environment.GetEnvironmentVariable("RABBITMQ_URL");

    private static MensagemRecebida Fala(string id) => new(
        id, "+55 11 98888-1111", "+55 11 3333-4444", "qual o valor do kg?", T0);

    /// <summary>Uma fila por teste: nome unico evita um teste ver a sobra do outro.</summary>
    private static async Task<RabbitMqQueue<MensagemRecebida>> Fila(string nome) =>
        await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, $"teste.{nome}.{Guid.NewGuid():N}");

    [SkippableFact]
    public async Task A_mensagem_publicada_chega_ao_consumidor()
    {
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        await using var fila = await Fila("entrega");
        await fila.Publicar(Fala("wamid.1"), default);

        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var lidas = new List<MensagemRecebida>();

        await foreach (var m in fila.Ler(prazo.Token))
        {
            lidas.Add(m);
            break;
        }

        Assert.Single(lidas);
        Assert.Equal("wamid.1", lidas[0].ProviderMessageId);
    }

    [SkippableFact]
    public async Task O_que_nao_foi_confirmado_continua_na_fila_depois_da_queda()
    {
        // O criterio central da issue: derrubar o consumidor com a fila cheia e
        // nada some.
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        var nome = $"teste.queda.{Guid.NewGuid():N}";
        await using (var produtor = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome))
        {
            // O retorno e conferido: `Publicar` devolve false quando o broker
            // recusa, e engolir isso faria o teste medir a fila errada e
            // culpar o consumo por uma mensagem que nunca entrou.
            for (var i = 0; i < 5; i++)
                Assert.True(await produtor.Publicar(Fala($"wamid.{i}"), default));
        }

        // Consumidor sobe, pega uma, e o processo "morre" antes de confirmar.
        //
        // O `await foreach` NAO serve aqui: ele descarta o enumerador ao sair, e
        // o descarte confirma o que estava em andamento — desligamento
        // gracioso, o oposto de uma queda. Entao o enumerador e conduzido a mao
        // e abandonado, e a conexao fecha embaixo dele.
        {
            var caindo = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome);
            using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var enumerador = caindo.Ler(prazo.Token).GetAsyncEnumerator();
            Assert.True(await enumerador.MoveNextAsync());   // pegou uma, sem ack

            await caindo.DisposeAsync();
        }

        await using var depois = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome);

        // A devolucao do que estava sem confirmacao acontece no broker, depois
        // que ele percebe a conexao caida: a espera e por isso, e nao por
        // lentidao da fila.
        var restantes = 0;
        for (var tentativa = 0; tentativa < 20 && restantes < 5; tentativa++)
        {
            restantes = depois.Aguardando;
            if (restantes < 5) await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.Equal(5, restantes);
    }

    [SkippableFact]
    public async Task A_mensagem_sobrevive_ao_reinicio_do_processo()
    {
        // Durabilidade e o argumento da issue: `Channel<T>` perde tudo aqui, e
        // o WhatsApp nao reentrega porque ja recebeu o 202.
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        var nome = $"teste.durabilidade.{Guid.NewGuid():N}";
        await using (var antes = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome))
        {
            await antes.Publicar(Fala("wamid.sobrevivente"), default);
        }

        // Conexao nova = processo novo, para o efeito deste teste.
        await using var depois = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome);

        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var m in depois.Ler(prazo.Token))
        {
            Assert.Equal("wamid.sobrevivente", m.ProviderMessageId);
            break;
        }
    }

    [SkippableFact]
    public async Task Depois_de_parar_de_aceitar_o_webhook_recusa()
    {
        // Recusar e melhor que aceitar e perder: o 503 faz o WhatsApp
        // reentregar (#72).
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        await using var fila = await Fila("parada");
        fila.PararDeAceitar();

        Assert.False(fila.Aceitando);
        Assert.False(await fila.Publicar(Fala("wamid.tarde"), default));
    }

    [SkippableFact]
    public async Task A_DLQ_existe_e_comeca_vazia()
    {
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        await using var fila = await Fila("dlq");

        Assert.Equal(0, await fila.ContarDlq());
    }

    [SkippableFact]
    public async Task Reprocessar_devolve_da_DLQ_para_a_fila_principal()
    {
        // E o que transforma "sumiu" em "esta ali, para reprocessar".
        Skip.If(Url is null, "sem RABBITMQ_URL: broker nao disponivel");

        var nome = $"teste.reprocessa.{Guid.NewGuid():N}";
        await using var fila = await RabbitMqQueue<MensagemRecebida>.Conectar(Url!, nome);

        // A mensagem vai para a DLQ pelo caminho de verdade: um corpo que nao
        // desserializa e recusado sem requeue. Publicar direto na DLQ testaria
        // o metodo e nao o mecanismo.
        await using var comOutroFormato = await RabbitMqQueue<string>.Conectar(Url!, nome);
        await comOutroFormato.Publicar("isto nao e uma MensagemRecebida", default);

        using (var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var enumerador = fila.Ler(prazo.Token).GetAsyncEnumerator();

            // `AsTask()` UMA vez: ValueTask so pode ser consumido uma vez, e
            // chamar duas vezes lanca InvalidOperationException.
            var lida = enumerador.MoveNextAsync().AsTask();

            // Nada chega ao consumidor: o corpo invalido foi para a DLQ, e o
            // consumidor continua de pe esperando a proxima.
            var chegou = await Task.WhenAny(lida, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.NotSame(lida, chegou);

            // O consumidor sai de cena ANTES da contagem: com ele ativo, o que
            // for republicado pelo reprocessamento e consumido de novo no meio
            // da medicao, e a conta vira corrida.
            //
            // Cancelar e ESPERAR vem antes do Dispose: descartar um iterador
            // async com um MoveNextAsync em voo lanca NotSupportedException.
            prazo.Cancel();
            try { await lida; } catch (OperationCanceledException) { }
            await enumerador.DisposeAsync();
        }

        Assert.Equal(1, await fila.ContarDlq());

        var devolvidas = await fila.ReprocessarDlq();

        Assert.Equal(1, devolvidas);
        Assert.Equal(0, await fila.ContarDlq());
    }

    [Fact]
    public void O_limite_de_entregas_existe_para_a_mensagem_parar_de_girar()
    {
        // Sem limite, mensagem que sempre falha volta para sempre: ocupa o
        // consumidor, atrasa o resto e enche o log com o mesmo erro.
        Assert.InRange(RabbitMqQueue<MensagemRecebida>.LimiteDeEntregas, 2, 5);
    }

    [Fact]
    public void O_nome_da_DLQ_deriva_do_nome_da_fila()
    {
        Assert.Equal("ingestao.dlq", RabbitMqQueue<MensagemRecebida>.NomeDaDlq("ingestao"));
    }
}
