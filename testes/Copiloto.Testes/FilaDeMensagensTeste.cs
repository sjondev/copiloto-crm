using Copiloto.Api.Ingestao;

namespace Copiloto.Testes;

/// <summary>
/// A fila entre o webhook e o processamento (#40).
///
/// Processar IA dentro do handler e o erro classico: provedor lento vira timeout
/// na origem, timeout vira reentrega, reentrega vira custo DUPLICADO — e o custo
/// aqui e de dinheiro, nao de CPU.
/// </summary>
public class FilaDeMensagensTeste
{
    private static MensagemRecebida Fala(string id = "wamid.1") =>
        new(id, "+5511987654321", "+5511333334444", "qual o valor do kg?",
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task Publicar_entrega_para_quem_le()
    {
        var fila = new FilaDeMensagens();

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
        var fila = new FilaDeMensagens();

        for (var i = 0; i < FilaDeMensagens.Capacidade; i++)
            await fila.Publicar(Fala($"wamid.{i}"), CancellationToken.None);

        Assert.Equal(FilaDeMensagens.Capacidade, fila.Aguardando);
    }

    [Fact]
    public async Task Fila_cheia_aplica_contrapressao_em_vez_de_descartar()
    {
        // O produtor ESPERA. Descartar em silencio perderia a fala do cliente
        // sem aparecer em lugar nenhum; a espera aparece como latencia.
        var fila = new FilaDeMensagens();
        for (var i = 0; i < FilaDeMensagens.Capacidade; i++)
            await fila.Publicar(Fala($"wamid.{i}"), CancellationToken.None);

        using var prazo = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var publicou = await fila.Publicar(Fala("estoura"), prazo.Token);

        Assert.False(publicou);
        Assert.Equal(FilaDeMensagens.Capacidade, fila.Aguardando);
    }

    [Fact]
    public async Task Desligamento_drena_o_que_ja_estava_na_fila()
    {
        // O criterio de aceite: parar de aceitar nao pode descartar o que entrou.
        var fila = new FilaDeMensagens();
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
        var fila = new FilaDeMensagens();
        fila.PararDeAceitar();

        Assert.False(await fila.Publicar(Fala(), CancellationToken.None));
    }

    [Fact]
    public async Task A_leitura_termina_sozinha_quando_a_fila_fecha_e_esvazia()
    {
        // Sem isto, o desligamento dependeria do timeout do host: o laco ficaria
        // esperando trabalho que nunca chega.
        var fila = new FilaDeMensagens();
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
