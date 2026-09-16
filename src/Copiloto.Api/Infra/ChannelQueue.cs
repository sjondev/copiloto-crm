using System.Threading.Channels;

namespace Copiloto.Api.Infra;

/// <summary>
/// A fila em processo, com `Channel<T>` — o padrao (#40, #66).
///
/// E o padrao pelo mesmo motivo do `FakeSource`: a aplicacao sobe inteira sem
/// broker, e a demo nao tem cinco conteineres para falhar ao vivo. RabbitMQ
/// entra atras da mesma interface na #69, por variavel de ambiente.
///
/// PERDE MENSAGEM SE O PROCESSO CAIR, e isso e' aceito enquanto a fila e' de
/// memoria. A durabilidade e a #69, e ate' la' o webhook responder 202 e' uma
/// promessa de que a mensagem foi RECEBIDA, nao de que sera processada.
/// </summary>
public class ChannelQueue<T> : IQueue<T>
{
    /// <summary>
    /// Teto de itens esperando. Existe porque fila sem limite nao para de
    /// crescer: se o consumo ficar mais lento que a chegada — provedor de IA
    /// devagar, pico de conversas —, a memoria sobe ate' o processo morrer, e
    /// ai a perda e' de TUDO que estava na fila, nao de uma mensagem.
    /// </summary>
    public const int Capacidade = 1_000;

    private readonly Channel<T> _canal =
        Channel.CreateBounded<T>(new BoundedChannelOptions(Capacidade)
        {
            // Contrapressao: o produtor ESPERA em vez de a fila descartar. O
            // webhook prefere demorar a responder do que perder a fala do
            // cliente em silencio — e a espera aparece como latencia, que e'
            // mensuravel, enquanto o descarte nao apareceria em lugar nenhum.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

    public int Aguardando => _canal.Reader.Count;

    /// <summary>
    /// Em memoria a fila so para de aceitar no desligamento. Um broker
    /// responderia pela conexao, e e por isso que isto e do contrato e nao um
    /// detalhe do Channel.
    /// </summary>
    public bool Aceitando => !_fechada;

    private bool _fechada;

    /// <summary>
    /// Enfileira. Devolve false quando a fila esta cheia e a espera estourou o
    /// tempo — o chamador decide o que dizer ao provedor.
    /// </summary>
    public async Task<bool> Publicar(T item, CancellationToken ct)
    {
        try
        {
            await _canal.Writer.WriteAsync(item, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ChannelClosedException)
        {
            // Desligamento em andamento: nao aceita trabalho novo.
            return false;
        }
    }

    public IAsyncEnumerable<T> Ler(CancellationToken ct) =>
        _canal.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Fecha para escrita. O consumidor continua lendo o que ficou — e' o que
    /// faz o desligamento DRENAR em vez de descartar.
    /// </summary>
    public void PararDeAceitar()
    {
        _fechada = true;
        _canal.Writer.TryComplete();
    }
}
