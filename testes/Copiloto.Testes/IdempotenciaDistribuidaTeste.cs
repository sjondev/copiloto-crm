using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Microsoft.Extensions.Logging;

namespace Copiloto.Testes;

/// <summary>
/// Idempotencia que sobrevive a duas instancias (#67).
///
/// A #18 resolveu a reentrega dentro de um processo. O buraco que sobrou nao
/// aparece em teste de unidade nenhum e nem em ambiente de desenvolvimento: ele
/// so existe quando ha replica, e se manifesta como fatura maior — nunca como
/// erro.
/// </summary>
public class IdempotenciaDistribuidaTeste
{
    private const string Id = "wamid.HBgNNTUxMTk4ODg4MTExMRUCABIYFjNBMD";

    [Fact]
    public async Task A_mesma_mensagem_entregue_as_duas_instancias_processa_uma_vez()
    {
        // Estado compartilhado e o que Redis sera: as duas instancias olham o
        // mesmo registro, e so uma delas ganha a marcacao.
        var compartilhado = new InMemoryState();
        var instanciaA = new GuardaDeReentrega(compartilhado);
        var instanciaB = new GuardaDeReentrega(compartilhado);

        var naA = await instanciaA.EhAPrimeiraVez(Id, default);
        var naB = await instanciaB.EhAPrimeiraVez(Id, default);

        Assert.True(naA);
        Assert.False(naB);
    }

    [Fact]
    public async Task Com_estado_por_processo_a_reentrega_e_cobrada_de_novo()
    {
        // Este teste documenta o BUG que a issue existe para fechar, e falharia
        // se alguem "simplificasse" a guarda de volta para um dicionario local:
        // duas instancias, dois registros, dois processamentos, duas cobrancas.
        var instanciaA = new GuardaDeReentrega(new InMemoryState());
        var instanciaB = new GuardaDeReentrega(new InMemoryState());

        Assert.True(await instanciaA.EhAPrimeiraVez(Id, default));
        Assert.True(await instanciaB.EhAPrimeiraVez(Id, default));
    }

    [Fact]
    public async Task Instancia_unica_com_inmemory_continua_funcionando()
    {
        // O criterio que protege a demo: sem Redis, tudo de pe.
        var guarda = new GuardaDeReentrega(new InMemoryState());

        Assert.True(await guarda.EhAPrimeiraVez(Id, default));
        Assert.False(await guarda.EhAPrimeiraVez(Id, default));
        Assert.False(await guarda.EhAPrimeiraVez(Id, default));
    }

    [Fact]
    public async Task Mensagens_diferentes_nao_se_atrapalham()
    {
        var guarda = new GuardaDeReentrega(new InMemoryState());

        Assert.True(await guarda.EhAPrimeiraVez("wamid.1", default));
        Assert.True(await guarda.EhAPrimeiraVez("wamid.2", default));
    }

    [Fact]
    public async Task Depois_da_janela_o_id_deixa_de_ocupar_espaco()
    {
        // Guardar id para sempre e um vazamento lento: a chave nunca mais e
        // consultada e continua paga em memoria (ou em Redis, que custa mais).
        var agora = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var guarda = new GuardaDeReentrega(new InMemoryState(() => agora), TimeSpan.FromHours(24));

        Assert.True(await guarda.EhAPrimeiraVez(Id, default));

        agora = agora.AddHours(25);

        Assert.True(await guarda.EhAPrimeiraVez(Id, default));
    }

    [Fact]
    public async Task Dentro_da_janela_a_reentrega_tardia_ainda_e_barrada()
    {
        var agora = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var guarda = new GuardaDeReentrega(new InMemoryState(() => agora), TimeSpan.FromHours(24));
        await guarda.EhAPrimeiraVez(Id, default);

        agora = agora.AddHours(23);

        Assert.False(await guarda.EhAPrimeiraVez(Id, default));
    }

    [Fact]
    public async Task Cinquenta_entregas_simultaneas_processam_uma_vez_so()
    {
        // O balanceador nao entrega em ordem nem espacado: a corrida e real.
        var compartilhado = new InMemoryState();
        var instancias = Enumerable.Range(0, 50)
            .Select(_ => new GuardaDeReentrega(compartilhado)).ToList();

        var resultados = await Task.WhenAll(
            instancias.Select(i => Task.Run(() => i.EhAPrimeiraVez(Id, default))));

        Assert.Single(resultados, primeira => primeira);
    }

    [Fact]
    public async Task Sem_id_do_provedor_a_guarda_recusa_em_vez_de_inventar_chave()
    {
        var guarda = new GuardaDeReentrega(new InMemoryState());

        await Assert.ThrowsAsync<ArgumentException>(
            () => guarda.EhAPrimeiraVez("  ", default));
    }

    // --- Com a fila e o worker de verdade ---

    /// <summary>Conta quantas vezes o worker disse que PROCESSOU.</summary>
    private class LogEspiao : ILogger<ProcessadorDeMensagens>
    {
        public int Processadas { get; private set; }
        public int Ignoradas { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => true;

        public void Log<TState>(
            LogLevel nivel, EventId id, TState estado, Exception? erro,
            Func<TState, Exception?, string> formatar)
        {
            var linha = formatar(estado, erro);
            if (linha.Contains("processada fora do webhook")) Processadas++;
            if (linha.Contains("reentrega ignorada")) Ignoradas++;
        }
    }

    private static MensagemRecebida Fala() => new(
        Id, "+55 11 98765-4321", "+55 11 3333-4444", "qual o valor do kg?",
        new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Reentrega_na_fila_nao_vira_segundo_processamento()
    {
        // O caminho inteiro: webhook enfileira duas vezes o mesmo wamid — o que
        // acontece quando o provedor nao recebe o 202 a tempo — e o worker
        // trabalha uma vez so.
        var log = new LogEspiao();
        var fila = new ChannelQueue<MensagemRecebida>();
        var worker = new ProcessadorDeMensagens(
            fila, new ResolvedorDeLead("+55 11 3333-4444"),
            new GuardaDeReentrega(new InMemoryState()), log);

        await worker.StartAsync(default);
        await fila.Publicar(Fala(), default);
        await fila.Publicar(Fala(), default);
        await worker.StopAsync(default);

        Assert.Equal(1, log.Processadas);
        Assert.Equal(1, log.Ignoradas);
    }

    [Fact]
    public void A_chave_e_prefixada_para_nao_colidir_com_os_outros_usos()
    {
        // Rate limit, circuit breaker e cache de analise dividem o mesmo Redis.
        Assert.StartsWith("ingestao:msg:", GuardaDeReentrega.Chave("wamid.1"));
    }
}
