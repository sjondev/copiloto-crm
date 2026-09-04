using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Copiloto.Api.Infra;

/// <summary>
/// A fila duravel, com confirmacao e dead letter queue (#69).
///
/// O argumento para RabbitMQ aqui NAO e vazao — o `ChannelQueue` faz 22 mil
/// req/s (#54). E DURABILIDADE: a fila em memoria perde o que estava dentro
/// quando o processo reinicia, e o WhatsApp nao reentrega, porque ja recebeu o
/// 202 do webhook. O resultado e uma conversa que o vendedor viu acontecer e o
/// dossie ignora, sem erro em lugar nenhum.
///
/// Tres garantias, e cada uma resolve um jeito de perder mensagem:
///
///   1. fila e mensagem PERSISTENTES  — reinicio do broker nao apaga
///   2. confirmacao manual (ack)      — mensagem so sai depois de processada
///   3. limite de entregas + DLQ      — o que falha sempre para de girar e vai
///                                      para um lugar de onde da para trazer
///                                      de volta
/// </summary>
public class RabbitMqQueue<T> : IQueue<T>, IAsyncDisposable
{
    /// <summary>
    /// Quantas vezes a mesma mensagem pode ser entregue antes de ir para a DLQ.
    ///
    /// Sem limite, mensagem que sempre falha volta para sempre: ela ocupa o
    /// consumidor, atrasa o resto e enche o log com o mesmo erro. Tres da
    /// espaco para falha transitoria (banco reiniciando, provedor fora) e nao
    /// da espaco para defeito permanente.
    /// </summary>
    public const int LimiteDeEntregas = 3;

    private readonly IConnection _conexao;
    private readonly IChannel _canal;
    private readonly string _fila;
    private bool _fechada;

    private RabbitMqQueue(IConnection conexao, IChannel canal, string fila)
    {
        _conexao = conexao;
        _canal = canal;
        _fila = fila;
    }

    public static string NomeDaDlq(string fila) => $"{fila}.dlq";

    /// <summary>
    /// Conecta e declara a topologia. Assincrono porque a conexao e' de rede —
    /// e por isso a fabrica e' estatica: construtor que abre socket esconde uma
    /// espera dentro de um `new`.
    /// </summary>
    public static async Task<RabbitMqQueue<T>> Conectar(
        string url, string fila, CancellationToken ct = default)
    {
        var conexao = await new ConnectionFactory { Uri = new Uri(url) }
            .CreateConnectionAsync(ct);
        // Confirmacao de PUBLICACAO ligada. Sem ela, `BasicPublishAsync` devolve
        // assim que entrega o byte ao socket, e o webhook responderia 202 para
        // uma mensagem que o broker pode nunca ter gravado — a mesma perda
        // silenciosa que esta issue existe para eliminar, so que na outra ponta.
        //
        // O preco e latencia por publicacao. E o preco certo: o 202 passa a
        // significar "esta gravado", que e o que ele promete a quem envia.
        var canal = await conexao.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken: ct);

        // A DLQ e declarada ANTES: ela precisa existir quando a fila principal
        // mandar a primeira mensagem para la, senao a mensagem descartada some
        // — que e exatamente o desfecho que esta issue existe para impedir.
        await canal.QueueDeclareAsync(NomeDaDlq(fila), durable: true, exclusive: false,
            autoDelete: false, cancellationToken: ct);

        await canal.QueueDeclareAsync(fila, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                // Quorum: replicada e com contagem de entregas nativa. Classic
                // queue nao conta entregas, e a contagem manual (header +
                // republicacao) perde o lugar na fila a cada tentativa.
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = LimiteDeEntregas,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = NomeDaDlq(fila),
            },
            cancellationToken: ct);

        // Um por vez: sem isto o broker despeja tudo no primeiro consumidor, e
        // uma segunda instancia sobe para nao fazer nada.
        await canal.BasicQosAsync(0, prefetchCount: 1, global: false, cancellationToken: ct);

        return new RabbitMqQueue<T>(conexao, canal, fila);
    }

    public int Aguardando => (int)_canal.MessageCountAsync(_fila).GetAwaiter().GetResult();

    public bool Aceitando => !_fechada && _conexao.IsOpen && _canal.IsOpen;

    public async Task<bool> Publicar(T item, CancellationToken ct)
    {
        if (!Aceitando) return false;

        try
        {
            var corpo = JsonSerializer.SerializeToUtf8Bytes(item);

            await _canal.BasicPublishAsync(
                exchange: "",
                routingKey: _fila,
                mandatory: true,
                // Persistente: sem isto a mensagem vive so na memoria do broker,
                // e um reinicio DELE apaga o que a nossa durabilidade deveria
                // ter protegido.
                basicProperties: new BasicProperties { Persistent = true },
                body: corpo,
                cancellationToken: ct);

            return true;
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Broker fora do ar: quem chama devolve 503 e o WhatsApp reentrega.
            // Recusar e melhor que aceitar e perder (#72).
            return false;
        }
    }

    /// <summary>
    /// Consome com confirmacao MANUAL: o ack de cada mensagem sai quando o
    /// consumidor pede a proxima.
    ///
    /// E o que faz "so sai depois de processada" valer de verdade — no
    /// `await foreach`, o corpo do laco roda inteiro antes do proximo
    /// `MoveNextAsync`. Se o processo morrer no meio do processamento, nao houve
    /// ack, e o broker entrega a mensagem de novo a outra instancia.
    /// </summary>
    public async IAsyncEnumerable<T> Ler([EnumeratorCancellation] CancellationToken ct)
    {
        var recebidas = System.Threading.Channels.Channel.CreateBounded<(T Item, ulong Tag)>(1);
        var consumidor = new AsyncEventingBasicConsumer(_canal);

        consumidor.ReceivedAsync += async (_, entrega) =>
        {
            T? item;
            try
            {
                item = JsonSerializer.Deserialize<T>(entrega.Body.Span);
            }
            catch (JsonException)
            {
                // Deixar a excecao subir aqui derruba o CONSUMIDOR: uma mensagem
                // malformada pararia a ingestao inteira, e o sintoma seria "as
                // conversas pararam de chegar" — sem erro no lugar onde alguem
                // procura.
                item = default;
            }

            if (item is null)
            {
                // Corpo que nao desserializa nao melhora tentando de novo: vai
                // direto para a DLQ, com o requeue desligado. Ele fica guardado
                // ali, e nao descartado, porque o defeito pode estar no nosso
                // formato — e ai a mensagem do cliente e recuperavel.
                await _canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            await recebidas.Writer.WriteAsync((item, entrega.DeliveryTag), ct);
        };

        var tag = await _canal.BasicConsumeAsync(_fila, autoAck: false, consumidor,
            cancellationToken: ct);

        ulong? pendente = null;

        try
        {
            await foreach (var (item, entrega) in recebidas.Reader.ReadAllAsync(ct))
            {
                if (pendente is { } anterior)
                    await _canal.BasicAckAsync(anterior, multiple: false, cancellationToken: ct);

                pendente = entrega;
                yield return item;
            }
        }
        finally
        {
            // O ultimo item processado tambem precisa de ack — e o desligamento
            // gracioso passa por aqui.
            if (pendente is { } ultimo)
                await _canal.BasicAckAsync(ultimo, multiple: false, CancellationToken.None);

            await _canal.BasicCancelAsync(tag, cancellationToken: CancellationToken.None);
        }
    }

    public void PararDeAceitar() => _fechada = true;

    /// <summary>Quantas mensagens desistiram, esperando alguem olhar.</summary>
    public async Task<int> ContarDlq(CancellationToken ct = default) =>
        (int)await _canal.MessageCountAsync(NomeDaDlq(_fila), ct);

    /// <summary>
    /// Devolve a DLQ para a fila principal. E o que transforma "sumiu" em "esta
    /// ali, para reprocessar" — depois de consertar a causa, porque sem isso a
    /// mensagem so vai passear e voltar.
    /// </summary>
    public async Task<int> ReprocessarDlq(int limite = 100, CancellationToken ct = default)
    {
        var devolvidas = 0;

        while (devolvidas < limite)
        {
            var mensagem = await _canal.BasicGetAsync(NomeDaDlq(_fila), autoAck: false, ct);
            if (mensagem is null) break;

            await _canal.BasicPublishAsync(
                exchange: "", routingKey: _fila, mandatory: true,
                basicProperties: new BasicProperties { Persistent = true },
                body: mensagem.Body.ToArray(), cancellationToken: ct);

            // Ack DEPOIS de republicar: na ordem inversa, uma queda no meio
            // perderia a mensagem nos dois lugares.
            await _canal.BasicAckAsync(mensagem.DeliveryTag, multiple: false, cancellationToken: ct);
            devolvidas++;
        }

        return devolvidas;
    }

    public async ValueTask DisposeAsync()
    {
        await _canal.CloseAsync();
        await _conexao.CloseAsync();
        GC.SuppressFinalize(this);
    }
}
