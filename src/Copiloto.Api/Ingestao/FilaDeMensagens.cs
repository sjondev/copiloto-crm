using System.Threading.Channels;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// A fila entre o webhook e o processamento (#40).
///
/// `Channel&lt;T&gt;` em memoria, e nao RabbitMQ: o volume nao justifica (YAGNI). A
/// troca fica barata porque quem publica so enxerga `Publicar` e quem consome
/// so enxerga `Ler` — o dia em que doer, o corpo muda e as duas pontas nao.
///
/// PERDE MENSAGEM SE O PROCESSO CAIR, e isso e' aceito enquanto a fila e' de
/// memoria. A durabilidade e a #69, e ate' la' o webhook responder 200 e' uma
/// promessa de que a mensagem foi RECEBIDA, nao de que sera processada.
/// </summary>
public class FilaDeMensagens
{
    /// <summary>
    /// Teto de itens esperando. Existe porque fila sem limite nao para de
    /// crescer: se o consumo ficar mais lento que a chegada — provedor de IA
    /// devagar, pico de conversas —, a memoria sobe ate' o processo morrer, e
    /// ai a perda e' de TUDO que estava na fila, nao de uma mensagem.
    /// </summary>
    public const int Capacidade = 1_000;

    private readonly Channel<MensagemRecebida> _canal =
        Channel.CreateBounded<MensagemRecebida>(new BoundedChannelOptions(Capacidade)
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
    /// Enfileira. Devolve false quando a fila esta cheia e a espera estourou o
    /// tempo — o chamador decide o que dizer ao provedor.
    /// </summary>
    public async Task<bool> Publicar(MensagemRecebida mensagem, CancellationToken ct)
    {
        try
        {
            await _canal.Writer.WriteAsync(mensagem, ct);
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

    public IAsyncEnumerable<MensagemRecebida> Ler(CancellationToken ct) =>
        _canal.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Fecha para escrita. O consumidor continua lendo o que ficou — e' o que
    /// faz o desligamento DRENAR em vez de descartar.
    /// </summary>
    public void PararDeAceitar() => _canal.Writer.TryComplete();
}
