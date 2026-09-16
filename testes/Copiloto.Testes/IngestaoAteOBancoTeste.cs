using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O caminho inteiro: fila -> processador -> banco (#158).
///
/// Era o trecho que nenhum teste cobria, e por isso o defeito passou tanto
/// tempo invisivel: a suite provava o MAPEAMENTO em SQLite e provava a
/// RESOLUCAO de lead em memoria, e ninguem provava que uma fala que entra pelo
/// webhook chega a existir em algum lugar. Ela nao chegava.
/// </summary>
public class IngestaoAteOBancoTeste : IDisposable
{
    private const string NumeroDaEmpresa = "+55 11 3333-4444";
    private const string NumeroDoCliente = "+55 11 98888-7777";

    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly ServiceProvider _servicos;

    public IngestaoAteOBancoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var servicos = new ServiceCollection();
        servicos.AddDbContext<CopilotoDbContext>(o => o.UseSqlite(_conexao));
        servicos.AddScoped<IRepositorioDeLeads, LeadsNoBanco>();
        servicos.AddScoped(sp => new ResolvedorDeLead(
            NumeroDaEmpresa, sp.GetRequiredService<IRepositorioDeLeads>()));

        _servicos = servicos.BuildServiceProvider();

        using var escopo = _servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<CopilotoDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _servicos.Dispose();
        _conexao.Dispose();
    }

    private ProcessadorDeMensagens Processador(IQueue<MensagemRecebida> fila) =>
        new(fila,
            _servicos.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProcessadorDeMensagens>.Instance);

    /// <summary>Empurra as falas pela fila e espera o worker drenar, como em producao.</summary>
    private async Task Ingerir(params MensagemRecebida[] falas)
    {
        var fila = new ChannelQueue<MensagemRecebida>();
        var processador = Processador(fila);

        await processador.StartAsync(CancellationToken.None);
        foreach (var fala in falas) await fila.Publicar(fala, CancellationToken.None);

        // Fecha para escrita e espera o laco TERMINAR de drenar. Sem esperar a
        // propria task, o teste mediria o que o worker alcancou a gravar antes
        // de o assert rodar — e passaria ou falharia pela velocidade da maquina.
        fila.PararDeAceitar();
        await processador.ExecuteTask!;
        await processador.StopAsync(CancellationToken.None);
    }

    private CopilotoDbContext Leitura() =>
        _servicos.CreateScope().ServiceProvider.GetRequiredService<CopilotoDbContext>();

    private static MensagemRecebida Do(string id, string texto, int minuto = 0, Midia? midia = null) =>
        new(id, NumeroDoCliente, NumeroDaEmpresa, texto, Agora.AddMinutes(minuto), midia);

    private static MensagemRecebida DoVendedor(string id, string texto, int minuto) =>
        new(id, NumeroDaEmpresa, NumeroDoCliente, texto, Agora.AddMinutes(minuto));

    [Fact]
    public async Task A_fala_que_entra_pela_fila_existe_no_banco_depois()
    {
        await Ingerir(Do("wamid.1", "qual o valor do kg?"));

        using var ctx = Leitura();
        var conversa = ctx.Conversas.Include(c => c.Mensagens).Single();

        Assert.Equal("qual o valor do kg?", conversa.Mensagens.Single().Texto);
        Assert.Equal(Autor.Cliente, conversa.Mensagens.Single().Autor);
    }

    [Fact]
    public async Task O_lead_sobrevive_ao_restart_porque_foi_para_o_banco()
    {
        // O defeito da #158 em uma linha: o resolvedor era singleton e caia no
        // LeadsEmMemoria, entao o Lead sumia sem erro nenhum aparecer.
        await Ingerir(Do("wamid.1", "bom dia"));

        using var ctx = Leitura();
        Assert.Equal("+5511988887777", ctx.Leads.Single().Telefone);
    }

    [Fact]
    public async Task Reentrega_do_provedor_nao_duplica_a_fala()
    {
        // O WhatsApp reentrega quando nao ve o 200 a tempo. Sem id derivado do
        // provedor, a mesma fala entraria duas vezes com Guids diferentes.
        await Ingerir(
            Do("wamid.1", "qual o valor do kg?"),
            Do("wamid.1", "qual o valor do kg?"));

        using var ctx = Leitura();
        Assert.Equal(1, ctx.Mensagens.Count());
    }

    [Fact]
    public async Task As_duas_pontas_da_conversa_ficam_na_MESMA_conversa()
    {
        await Ingerir(
            Do("wamid.1", "qual o valor do kg?", 0),
            DoVendedor("wamid.2", "o bourbon sai a 78", 1),
            Do("wamid.3", "vou pensar", 2));

        using var ctx = Leitura();
        var conversa = ctx.Conversas.Include(c => c.Mensagens).Single();

        Assert.Equal(3, conversa.Mensagens.Count);
        Assert.Equal(1, ctx.Leads.Count());
        Assert.Equal(Autor.Vendedor, conversa.Mensagens[1].Autor);
    }

    [Fact]
    public async Task Fala_que_chega_fora_de_ordem_nao_se_perde()
    {
        // O celular do cliente estava sem sinal e o WhatsApp entrega a antiga
        // depois da nova. As duas precisam chegar ao banco.
        //
        // A ORDEM em que elas voltam da leitura e outro assunto, e ja tem dono:
        // #136 / PR #137, que nao esta nesta pilha. Aqui se prova que nada se
        // perde; la se prova que nada sai trocado.
        await Ingerir(
            Do("wamid.2", "vou pensar", 10),
            Do("wamid.1", "qual o valor do kg?", 0));

        using var ctx = Leitura();
        var conversa = ctx.Conversas.Include(c => c.Mensagens).Single();

        Assert.Equal(2, conversa.Mensagens.Count);
        Assert.Contains(conversa.Mensagens, m => m.Texto == "qual o valor do kg?");
        Assert.Equal(
            Agora,
            conversa.Mensagens.Min(m => m.EnviadaEm));
    }

    [Fact]
    public async Task O_audio_chega_ao_banco_como_lacuna_e_nao_como_texto_puro()
    {
        await Ingerir(Do("wamid.1", "", midia: new Midia(TipoDeMidia.Audio, TimeSpan.FromSeconds(14))));

        using var ctx = Leitura();
        var fala = ctx.Conversas.Include(c => c.Mensagens).Single().Mensagens.Single();

        Assert.True(fala.NaoInterpretada);
        Assert.Equal("[audio nao transcrito, 14s]", fala.Texto);
    }

    [Fact]
    public async Task Numero_irreconhecivel_nao_derruba_o_worker_nem_grava_lixo()
    {
        await Ingerir(
            new MensagemRecebida("wamid.1", "nao-e-telefone", "tambem-nao", "oi", Agora),
            Do("wamid.2", "essa aqui e valida"));

        using var ctx = Leitura();
        Assert.Equal(1, ctx.Mensagens.Count());
        Assert.Equal("essa aqui e valida", ctx.Mensagens.Single().Texto);
    }

    [Fact]
    public void O_id_da_fala_e_derivado_do_id_do_provedor()
    {
        Assert.Equal(IdDaMensagem.De("wamid.1"), IdDaMensagem.De("wamid.1"));
        Assert.NotEqual(IdDaMensagem.De("wamid.1"), IdDaMensagem.De("wamid.2"));
        Assert.Throws<ArgumentException>(() => IdDaMensagem.De("  "));
    }
}
