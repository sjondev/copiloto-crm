using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Api.Infra;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Leitura;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O dossie guardado e as rotas que a tela chama (#161).
///
/// E a ultima peca entre o backend e o frontend: antes disto a API tinha
/// `/saude` e o webhook, e uma tela nao teria o que pedir.
/// </summary>
public class LeituraNaTelaTeste : IDisposable
{
    private const string NumeroDaEmpresa = "+55 11 3333-4444";
    private const string NumeroDoCliente = "+55 11 98888-7777";

    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly ServiceProvider _servicos;

    public LeituraNaTelaTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        var raiz = typeof(LeituraNaTelaTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddDbContext<CopilotoDbContext>(o => o.UseSqlite(_conexao));
        servicos.AddScoped<IRepositorioDeLeads, LeadsNoBanco>();
        servicos.AddScoped(sp => new ResolvedorDeLead(
            NumeroDaEmpresa, sp.GetRequiredService<IRepositorioDeLeads>()));

        servicos.AddSingleton<IModelProvider>(_ =>
            FakeProvider.DaPasta(Path.Combine(raiz, "seed", "respostas")));
        servicos.AddSingleton(_ => new RoteadorDeModelo(
            [new ModeloDisponivel("fake-mini", "fake", 0m, 1, [Tarefa.Leitura])]));
        servicos.AddSingleton<CascataDeModelos>();
        servicos.AddSingleton(sp => new AgenteDeLeitura(
            sp.GetRequiredService<CascataDeModelos>(),
            new MontadorDeContexto(),
            File.ReadAllText(Path.Combine(raiz, "prompts", "a1-leitura.md")),
            NullLogger<AgenteDeLeitura>.Instance));

        _servicos = servicos.BuildServiceProvider();

        using var escopo = _servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<CopilotoDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _servicos.Dispose();
        _conexao.Dispose();
    }

    /// <summary>O corpo do 404 e um objeto anonimo, entao o que se confere e o status.</summary>
    private static int Status(IResult resposta) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(resposta).StatusCode ?? 0;

    private CopilotoDbContext Ctx() =>
        _servicos.CreateScope().ServiceProvider.GetRequiredService<CopilotoDbContext>();

    /// <summary>A conversa do cafe, entrando pelo caminho de verdade.</summary>
    private async Task<Guid> IngerirAConversaDoCafe()
    {
        var fila = new ChannelQueue<MensagemRecebida>();
        var processador = new ProcessadorDeMensagens(
            fila,
            _servicos.GetRequiredService<IServiceScopeFactory>(),
            new GuardaDeReentrega(new InMemoryState()),
            NullLogger<ProcessadorDeMensagens>.Instance);

        await processador.StartAsync(CancellationToken.None);

        await fila.Publicar(new MensagemRecebida(
            "w.1", NumeroDoCliente, NumeroDaEmpresa, "qual o valor do kg?", Agora), CancellationToken.None);
        await fila.Publicar(new MensagemRecebida(
            "w.2", NumeroDaEmpresa, NumeroDoCliente, "o bourbon sai a 78", Agora.AddMinutes(2)), CancellationToken.None);
        await fila.Publicar(new MensagemRecebida(
            "w.3", NumeroDoCliente, NumeroDaEmpresa, "vou pensar melhor e te falo", Agora.AddMinutes(9)), CancellationToken.None);

        fila.PararDeAceitar();
        await processador.ExecuteTask!;
        await processador.StopAsync(CancellationToken.None);

        using var ctx = Ctx();
        return ctx.Leads.Single().Id;
    }

    [Fact]
    public async Task Do_webhook_ate_a_tela_sem_escrever_nada_na_mao()
    {
        // O caminho inteiro: fala entra pela fila, e a rota que a tela chama
        // devolve a leitura pronta.
        var leadId = await IngerirAConversaDoCafe();

        using var ctx = Ctx();
        var resposta = Assert.IsType<Ok<DossieNaTela>>(
            await EndpointsDeLeitura.DossieDoLead(leadId, ctx));

        var dossie = resposta.Value!;
        Assert.Equal("Morna", dossie.Temperatura);
        Assert.Equal("Esfriando", dossie.Direcao);
        Assert.Equal("morna e esfriando", dossie.Resumo);
        Assert.Single(dossie.SinaisDeCompra);
        Assert.Single(dossie.SinaisDeFuga);
    }

    [Fact]
    public async Task Todo_sinal_na_tela_chega_com_a_frase_que_o_originou()
    {
        var leadId = await IngerirAConversaDoCafe();

        using var ctx = Ctx();
        var dossie = Assert.IsType<Ok<DossieNaTela>>(
            await EndpointsDeLeitura.DossieDoLead(leadId, ctx)).Value!;

        Assert.All(dossie.SinaisDeCompra.Concat(dossie.SinaisDeFuga), s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.TrechoCitado));
            Assert.NotEqual(Guid.Empty, s.MensagemId);
        });

        Assert.Equal("qual o valor do kg?", dossie.SinaisDeCompra.Single().TrechoCitado);
        Assert.Equal("vou pensar melhor e te falo", dossie.SinaisDeFuga.Single().TrechoCitado);
    }

    [Fact]
    public async Task O_dossie_sobrevive_ao_restart()
    {
        // Escopo novo, contexto novo: o que volta veio do banco, e nao de
        // entidade que sobrou rastreada.
        var leadId = await IngerirAConversaDoCafe();

        using var ctx = Ctx();

        // Uma leitura por fala: a cada mensagem nova o A1 rele a conversa
        // inteira, e o dossie anterior fica como historico. E o que a #51 vai
        // precisar para responder "por que essa sugestao" — e o que o triador
        // (#37) vai reduzir, decidindo quais falas merecem releitura.
        var todos = ctx.Dossies.Include(d => d.Sinais).ToList();
        Assert.Equal(3, todos.Count);

        var doBanco = todos.MaxBy(d => d.GeradoEm)!;

        Assert.Equal(Temperatura.Morna, doBanco.Termometro!.Valor);
        Assert.Equal(Direcao.Esfriando, doBanco.Termometro.Para);
        Assert.Equal(2, doBanco.Sinais.Count);
        Assert.Equal(3, doBanco.Lacunas.Count);
        Assert.Contains(doBanco.Sinais, s => s.Tipo == TipoDeSinal.Fuga);

        // Guid preservado, que e o que liga o sinal a fala na tela.
        Assert.All(doBanco.Sinais, s => Assert.NotEqual(Guid.Empty, s.MensagemId));
    }

    [Fact]
    public async Task A_conversa_volta_para_a_tela_em_ordem()
    {
        var leadId = await IngerirAConversaDoCafe();

        using var ctx = Ctx();
        var falas = Assert.IsType<Ok<FalaNaTela[]>>(
            await EndpointsDeLeitura.ConversaDoLead(leadId, ctx)).Value!;

        Assert.Equal(3, falas.Length);
        Assert.Equal("qual o valor do kg?", falas[0].Texto);
        Assert.Equal("Cliente", falas[0].Autor);
        Assert.Equal("Vendedor", falas[1].Autor);
        Assert.Equal("vou pensar melhor e te falo", falas[2].Texto);
    }

    [Fact]
    public async Task Lead_que_nao_existe_devolve_404_e_nao_corpo_vazio()
    {
        using var ctx = Ctx();

        Assert.Equal(404, Status(await EndpointsDeLeitura.DossieDoLead(Guid.NewGuid(), ctx)));
        Assert.Equal(404, Status(await EndpointsDeLeitura.ConversaDoLead(Guid.NewGuid(), ctx)));
    }

    [Fact]
    public async Task Lead_sem_leitura_ainda_devolve_404_e_nao_dossie_vazio()
    {
        // Dossie vazio na tela pareceria leitura feita que nao achou nada, e o
        // vendedor leria "nenhum sinal" como certeza.
        using (var escrita = Ctx())
        {
            escrita.Leads.Add(new Lead(Guid.NewGuid(), "+5511977776666", Agora));
            escrita.SaveChanges();
        }

        using var ctx = Ctx();
        var lead = ctx.Leads.Single();

        Assert.Equal(404, Status(await EndpointsDeLeitura.DossieDoLead(lead.Id, ctx)));
    }

    [Fact]
    public async Task Lead_sem_conversa_devolve_lista_vazia_e_nao_404()
    {
        // Diferente do dossie de proposito: o lead existe e ainda nao falou, e a
        // tela mostra isso sem mentir.
        using (var escrita = Ctx())
        {
            escrita.Leads.Add(new Lead(Guid.NewGuid(), "+5511977776666", Agora));
            escrita.SaveChanges();
        }

        using var ctx = Ctx();
        var lead = ctx.Leads.Single();

        var falas = Assert.IsType<Ok<FalaNaTela[]>>(
            await EndpointsDeLeitura.ConversaDoLead(lead.Id, ctx)).Value!;

        Assert.Empty(falas);
    }

    [Fact]
    public async Task A_tela_recebe_a_leitura_MAIS_RECENTE()
    {
        // Leitura antiga na tela e pior que tela vazia, porque parece atual.
        var leadId = await IngerirAConversaDoCafe();

        using (var escrita = Ctx())
        {
            var deal = escrita.Deals.Single(d => d.LeadId == leadId);
            var velho = new Dossie(Guid.NewGuid(), deal.Id, Agora.AddDays(-30));
            velho.Ler(new Termometro(Temperatura.Quente, Direcao.Esquentando));
            escrita.Dossies.Add(velho);
            escrita.SaveChanges();
        }

        using var ctx = Ctx();
        var dossie = Assert.IsType<Ok<DossieNaTela>>(
            await EndpointsDeLeitura.DossieDoLead(leadId, ctx)).Value!;

        Assert.Equal("Morna", dossie.Temperatura);
    }

    [Fact]
    public async Task Um_deal_e_aberto_na_primeira_fala_e_reusado_nas_seguintes()
    {
        // Sem Deal o custo de IA nasce sem dono, e vincular depois exige
        // backfill e adivinhacao (#2).
        var leadId = await IngerirAConversaDoCafe();

        using var ctx = Ctx();
        var deal = Assert.Single(ctx.Deals);
        Assert.Equal(leadId, deal.LeadId);
    }
}
