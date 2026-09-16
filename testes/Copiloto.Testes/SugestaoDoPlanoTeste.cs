using System.Reflection;
using System.Text.Json;
using Copiloto.Api.Ia;
using Copiloto.Api.Leitura;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O `[sugerir]` de cada bloco do plano (#189).
///
/// A IA e INSUMO: ela propoe em campo separado, e o vendedor aceita, edita ou
/// ignora. Nada aqui pode escrever por cima do que ele digitou, e sugestao que
/// nao vem nao pode custar o texto que ele ja tinha.
/// </summary>
public class SugestaoDoPlanoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly ModeloDisponivel Forte =
        new("fake-forte", "fake", 2m, 500, [Tarefa.Plano]);

    private int _proximoNumero;

    private static string Raiz => typeof(SugestaoDoPlanoTeste).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "RaizDoRepositorio").Value!;

    private static AgenteDePlano AgenteDoSeed() => Agente(
        FakeProvider.DaPasta(Path.Combine(Raiz, "seed", "respostas")));

    private static AgenteDePlano Agente(IModelProvider provedor) =>
        new(new CascataDeModelos(
                new RoteadorDeModelo([Forte]), provedor, NullLogger<CascataDeModelos>.Instance),
            new PrecoDoModelo([Forte]),
            "instrucoes de teste",
            NullLogger<AgenteDePlano>.Instance);

    private Lead Semear(Guid? dono = null)
    {
        using var ctx = NovoContexto();

        var lead = new Lead(
            Guid.NewGuid(), $"+55119{(++_proximoNumero):D4}0000", T0.AddDays(-5), "Marina");
        if (dono is { } vendedor) lead.Assumir(vendedor);
        ctx.Leads.Add(lead);
        ctx.Deals.Add(new Deal(Guid.NewGuid(), lead.Id, T0.AddDays(-5)));
        ctx.SaveChanges();

        return lead;
    }

    // --- A sugestao chega sem tocar no texto ---

    [Fact]
    public async Task A_sugestao_chega_em_campo_separado()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.EscreverBloco(
            lead.Id, "Objetivo", new TextoDoBloco("o que EU escrevi"),
            ctx, QuemPede.Principal(ana));

        var resposta = await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        var objetivo = Bloco(resposta, "Objetivo");

        Assert.Equal("o que EU escrevi", objetivo.Texto);
        Assert.False(string.IsNullOrWhiteSpace(objetivo.Sugestao));
    }

    [Fact]
    public async Task Cada_bloco_recebe_a_sugestao_do_SEU_bloco()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        var resposta = await EndpointsDoPlano.Sugerir(
            lead.Id, "ProximoPasso", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        // Quatro campos com a mesma frase seria pior que campo vazio: pareceria
        // ferramenta quebrada.
        Assert.NotEqual(Bloco(resposta, "Objetivo").Sugestao, Bloco(resposta, "ProximoPasso").Sugestao);
    }

    [Fact]
    public async Task Sugerir_nao_conta_versao()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        var resposta = await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        // A versao responde "o que ele tinha escrito", e sugestao que ele ainda
        // nao aceitou nunca fez parte do plano.
        Assert.Equal(1, Plano(resposta).Versao);
    }

    // --- Aceitar, editar, ignorar ---

    [Fact]
    public async Task Aceitar_promove_a_sugestao_e_conta_versao()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        var sugerida = Bloco(await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana)), "Objetivo");

        var resposta = await EndpointsDoPlano.Aceitar(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana));

        var objetivo = Bloco(resposta, "Objetivo");

        Assert.Equal(sugerida.Sugestao, objetivo.Texto);
        Assert.Null(objetivo.Sugestao);
        Assert.Equal(2, Plano(resposta).Versao);
    }

    [Fact]
    public async Task Descartar_tira_a_sugestao_e_deixa_o_texto()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.EscreverBloco(
            lead.Id, "Objetivo", new TextoDoBloco("o meu texto"), ctx, QuemPede.Principal(ana));
        await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        var objetivo = Bloco(await EndpointsDoPlano.Descartar(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana)), "Objetivo");

        Assert.Equal("o meu texto", objetivo.Texto);
        Assert.Null(objetivo.Sugestao);
    }

    // --- Quando a IA nao responde ---

    /// <summary>
    /// O vendedor clicou num botao OPCIONAL. Transformar a falha do modelo em
    /// tela vermelha ensinaria que a ferramenta esta quebrada quando so a
    /// sugestao nao veio — e o plano dele continua inteiro.
    /// </summary>
    [Fact]
    public async Task Modelo_que_nao_responde_devolve_o_plano_intacto()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.EscreverBloco(
            lead.Id, "Objetivo", new TextoDoBloco("o meu texto"), ctx, QuemPede.Principal(ana));

        // Provedor sem nenhuma resposta gravada: a cascata degrada.
        var resposta = await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, Agente(new FakeProvider()), QuemPede.Principal(ana));

        Assert.Equal(200, Status(resposta));
        Assert.Equal("o meu texto", Bloco(resposta, "Objetivo").Texto);
        Assert.Null(Bloco(resposta, "Objetivo").Sugestao);
    }

    [Fact]
    public async Task Resposta_ilegivel_nao_vira_sugestao()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        var provedor = new FakeProvider(new Dictionary<Tarefa, RespostaGravada>
        {
            [Tarefa.Plano] = new(
                Tarefa.Plano,
                JsonDocument.Parse("""{"outra_coisa": 42}""").RootElement.Clone(), 10, 5),
        });

        var resposta = await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, Agente(provedor), QuemPede.Principal(ana));

        Assert.Null(Bloco(resposta, "Objetivo").Sugestao);
    }

    // --- O ledger ---

    /// <summary>
    /// O ledger existia no dominio e NADA em producao gravava nele. Este e o
    /// primeiro caminho que grava: sem o Deal amarrado, a invocacao nao responde
    /// a unica pergunta que o ledger existe para responder — quanto custou ESTA
    /// venda (#1, #2).
    /// </summary>
    [Fact]
    public async Task A_sugestao_entra_no_ledger_amarrada_ao_negocio()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        using var conferindo = NovoContexto();
        var invocacao = Assert.Single(conferindo.Invocacoes);

        Assert.Equal("fake-forte", invocacao.Modelo);
        Assert.True(invocacao.CustoEmReais > 0, "a chamada precisa ter custo no ledger");
        Assert.NotNull(invocacao.DealId);
    }

    [Fact]
    public async Task Sugestao_que_nao_veio_nao_entra_no_ledger()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, Agente(new FakeProvider()), QuemPede.Principal(ana));

        using var conferindo = NovoContexto();
        Assert.Empty(conferindo.Invocacoes);
    }

    // --- O escopo vale aqui tambem ---

    [Fact]
    public async Task Sugerir_no_plano_alheio_e_barrado()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var doBruno = Semear(dono: bruno.Id);

        var resposta = await EndpointsDoPlano.Sugerir(
            doBruno.Id, "Objetivo", ctx, AgenteDoSeed(), QuemPede.Principal(ana));

        Assert.Equal(404, Status(resposta));

        using var conferindo = NovoContexto();
        Assert.Empty(conferindo.Invocacoes);
    }

    // --- Leitura da resposta ---

    private static PlanoNaTela Plano(IResult resposta) =>
        Assert.IsType<PlanoNaTela>(
            resposta is IValueHttpResult { Value: var valor } ? valor : null);

    private static BlocoNaTela Bloco(IResult resposta, string bloco) =>
        Assert.Single(Plano(resposta).Blocos, b => b.Bloco == bloco);

    private static int Status(IResult resposta) =>
        resposta is IStatusCodeHttpResult { StatusCode: { } codigo } ? codigo : 200;
}
