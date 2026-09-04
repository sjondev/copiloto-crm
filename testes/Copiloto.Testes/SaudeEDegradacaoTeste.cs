using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O que acontece quando a infraestrutura cai (#72).
///
/// O criterio mais contraintuitivo da issue: com a fila fora, RECUSAR a
/// mensagem e melhor que aceitar. Aceitar e perder e a falha silenciosa que a
/// fila duravel existe para eliminar — o WhatsApp reentrega o que deu erro, e
/// nao reentrega o que recebeu 202.
/// </summary>
public class SaudeEDegradacaoTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public SaudeEDegradacaoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    /// <summary>Um estado que cai quando mandam cair.</summary>
    private class EstadoQueCai : IDistributedState
    {
        public bool NoChao { get; set; }
        public int Chamadas { get; private set; }

        private readonly InMemoryState _real = new();

        private T Talvez<T>(Func<InMemoryState, T> operacao)
        {
            Chamadas++;
            if (NoChao) throw new InvalidOperationException("conexao recusada");

            return operacao(_real);
        }

        public Task<bool> TentarMarcar(string c, TimeSpan v, CancellationToken ct) =>
            Talvez(e => e.TentarMarcar(c, v, ct));
        public Task<string?> Ler(string c, CancellationToken ct) => Talvez(e => e.Ler(c, ct));
        public Task Gravar(string c, string v, TimeSpan t, CancellationToken ct) =>
            Talvez(e => e.Gravar(c, v, t, ct));
        public Task<long> Incrementar(string c, TimeSpan j, CancellationToken ct) =>
            Talvez(e => e.Incrementar(c, j, ct));
    }

    private static IConfiguration Config() => new ConfigurationBuilder().Build();

    // --- Degradacao do estado ---

    [Fact]
    public async Task Estado_fora_do_ar_nao_derruba_a_aplicacao()
    {
        // O vendedor nao pode perder o atendimento inteiro por causa de um cache.
        var primario = new EstadoQueCai { NoChao = true };
        var estado = new EstadoComDegradacao(primario, NullLogger<EstadoComDegradacao>.Instance);

        Assert.True(await estado.TentarMarcar("k", TimeSpan.FromMinutes(1), default));
        Assert.True(estado.Degradado);
    }

    [Fact]
    public async Task Enquanto_degradado_o_risco_fica_dito_e_nao_implicito()
    {
        // Cair para memoria em silencio esconde que idempotencia, rate limit e
        // circuito passaram a valer so nesta instancia — e isso reaparece
        // semanas depois como fatura maior, sem ninguem ligar uma coisa a outra.
        var estado = new EstadoComDegradacao(
            new EstadoQueCai { NoChao = true }, NullLogger<EstadoComDegradacao>.Instance);
        await estado.Ler("k", default);

        Assert.Contains("idempotencia", estado.RiscoAtual);
        Assert.Contains("apenas nesta instancia", estado.RiscoAtual);
    }

    [Fact]
    public async Task A_reserva_atende_de_verdade_e_nao_so_engole_a_chamada()
    {
        var estado = new EstadoComDegradacao(
            new EstadoQueCai { NoChao = true }, NullLogger<EstadoComDegradacao>.Instance);

        await estado.Gravar("k", "v", TimeSpan.FromMinutes(5), default);

        Assert.Equal("v", await estado.Ler("k", default));
    }

    [Fact]
    public async Task Degradado_nao_martela_o_primario_a_cada_chamada()
    {
        // Tentar a cada chamada transformaria cada operacao numa espera de
        // timeout: o remedio ficaria mais caro que a doenca.
        var agora = T0;
        var primario = new EstadoQueCai { NoChao = true };
        var estado = new EstadoComDegradacao(
            primario, NullLogger<EstadoComDegradacao>.Instance, agora: () => agora);

        await estado.Ler("k", default);
        var depoisDaPrimeira = primario.Chamadas;

        for (var i = 0; i < 10; i++) await estado.Ler("k", default);

        Assert.Equal(depoisDaPrimeira, primario.Chamadas);
    }

    [Fact]
    public async Task Passada_a_espera_ele_tenta_de_novo_e_volta_sozinho()
    {
        var agora = T0;
        var primario = new EstadoQueCai { NoChao = true };
        var estado = new EstadoComDegradacao(
            primario, NullLogger<EstadoComDegradacao>.Instance, agora: () => agora);
        await estado.Ler("k", default);

        primario.NoChao = false;
        agora += EstadoComDegradacao.EsperaParaTentarDeNovo + TimeSpan.FromSeconds(1);

        await estado.Ler("k", default);

        Assert.False(estado.Degradado);
        Assert.Equal("", estado.RiscoAtual);
    }

    [Fact]
    public async Task Com_o_primario_de_pe_a_reserva_nem_e_tocada()
    {
        var primario = new EstadoQueCai();
        var estado = new EstadoComDegradacao(primario, NullLogger<EstadoComDegradacao>.Instance);

        await estado.Gravar("k", "v", TimeSpan.FromMinutes(1), default);

        Assert.False(estado.Degradado);
        Assert.Equal(1, primario.Chamadas);
    }

    // --- O relatorio ---

    [Fact]
    public async Task O_relatorio_separa_cada_dependencia()
    {
        // Health check que responde so verde ou vermelho manda o plantonista
        // procurar do zero.
        using var ctx = new CopilotoDbContext(_opcoes);
        var saude = new Saude(ctx, new ChannelQueue<MensagemRecebida>(), new InMemoryState(), Config());

        var relatorio = await saude.Agora(default);

        Assert.Equal(3, relatorio.Dependencias.Count);
        Assert.Contains(relatorio.Dependencias, d => d.Nome == "postgres");
        Assert.Contains(relatorio.Dependencias, d => d.Nome == "fila");
        Assert.Contains(relatorio.Dependencias, d => d.Nome == "estado");
        Assert.True(relatorio.Apta);
        Assert.False(relatorio.Degradada);
    }

    [Fact]
    public async Task Fila_que_parou_de_aceitar_derruba_a_aptidao()
    {
        // O criterio contraintuitivo: melhor recusar e deixar o WhatsApp
        // reentregar do que aceitar e perder.
        var fila = new ChannelQueue<MensagemRecebida>();
        fila.PararDeAceitar();

        using var ctx = new CopilotoDbContext(_opcoes);
        var relatorio = await new Saude(ctx, fila, new InMemoryState(), Config()).Agora(default);

        Assert.False(relatorio.Apta);
        Assert.Contains(relatorio.Dependencias, d => d.Nome == "fila" && !d.Ok);
    }

    [Fact]
    public async Task Estado_degradado_aparece_no_relatorio_sem_derrubar_a_aptidao()
    {
        // Perda de garantia nao pode virar perda de atendimento: 503 aqui
        // tiraria do ar um sistema que ainda atende.
        var estado = new EstadoComDegradacao(
            new EstadoQueCai { NoChao = true }, NullLogger<EstadoComDegradacao>.Instance);
        await estado.Ler("k", default);

        using var ctx = new CopilotoDbContext(_opcoes);
        var relatorio = await new Saude(
            ctx, new ChannelQueue<MensagemRecebida>(), estado, Config()).Agora(default);

        Assert.True(relatorio.Apta);
        Assert.True(relatorio.Degradada);
        Assert.Contains(relatorio.Dependencias,
            d => d.Nome == "estado" && !d.Ok && d.Detalhe.Contains("degradado"));
    }

    [Fact]
    public async Task Banco_fora_aparece_com_o_motivo_e_nao_so_com_falhou()
    {
        // A diferenca entre o plantonista saber que e senha errada e ficar
        // adivinhando.
        var ctx = new CopilotoDbContext(
            new DbContextOptionsBuilder<CopilotoDbContext>()
                .UseNpgsql("Host=nao-existe-mesmo.invalid;Database=x;Username=y;Timeout=1")
                .Options);

        var relatorio = await new Saude(
            ctx, new ChannelQueue<MensagemRecebida>(), new InMemoryState(), Config()).Agora(default);

        var postgres = relatorio.Dependencias.Single(d => d.Nome == "postgres");
        Assert.False(postgres.Ok);
        Assert.False(relatorio.Apta);
        Assert.NotEmpty(postgres.Detalhe);
    }

    [Fact]
    public async Task A_fila_de_pe_informa_quantos_esperam()
    {
        var fila = new ChannelQueue<MensagemRecebida>();
        await fila.Publicar(
            new MensagemRecebida("wamid.1", "+5511988887777", "+5511333334444", "oi", T0), default);

        using var ctx = new CopilotoDbContext(_opcoes);
        var relatorio = await new Saude(ctx, fila, new InMemoryState(), Config()).Agora(default);

        Assert.Contains("1 aguardando", relatorio.Dependencias.Single(d => d.Nome == "fila").Detalhe);
    }
}
