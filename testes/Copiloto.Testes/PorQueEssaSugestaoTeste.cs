using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Api.Leitura;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// "Por que essa sugestao?" (#51).
///
/// Auditabilidade: a resposta pronta para "como voce sabe que a IA nao esta
/// inventando?". Para o vendedor, e o que constroi confianca — ele ve o texto
/// EXATO que foi ao modelo, e o que aquilo custou.
/// </summary>
public class PorQueEssaSugestaoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly ModeloDisponivel Forte =
        new("fake-forte", "fake", 2m, 500, [Tarefa.Plano]);

    private const string Instrucoes = "Voce ajuda o vendedor a preparar a conversa.";

    private int _proximoNumero;

    private static string Raiz => typeof(PorQueEssaSugestaoTeste).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "RaizDoRepositorio").Value!;

    private static AgenteDePlano Agente() =>
        new(new CascataDeModelos(
                new RoteadorDeModelo([Forte]),
                FakeProvider.DaPasta(Path.Combine(Raiz, "seed", "respostas")),
                NullLogger<CascataDeModelos>.Instance),
            new PrecoDoModelo([Forte]),
            Instrucoes,
            NullLogger<AgenteDePlano>.Instance);

    private Lead Semear(Guid dono)
    {
        using var ctx = NovoContexto();

        var lead = new Lead(
            Guid.NewGuid(), $"+55119{(++_proximoNumero):D4}0000", T0.AddDays(-5), "Marina");
        lead.Assumir(dono);
        ctx.Leads.Add(lead);
        ctx.Deals.Add(new Deal(Guid.NewGuid(), lead.Id, T0.AddDays(-5)));
        ctx.SaveChanges();

        return lead;
    }

    // --- O que o botao mostra ---

    [Fact]
    public async Task O_porque_traz_modelo_custo_latencia_e_versao_do_prompt()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(ana.Id);

        await EndpointsDoPlano.Sugerir(
            lead.Id, "Objetivo", ctx, Agente(), QuemPede.Principal(ana));

        var porque = Assert.IsType<PorQueNaTela>(Valor(await EndpointsDoPlano.PorQue(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana))));

        Assert.Equal("fake-forte", porque.Modelo);
        Assert.Equal(VersaoDoPrompt.De(Instrucoes), porque.VersaoDoPrompt);
        Assert.True(porque.CustoEmReais > 0, "a chamada custou e o custo precisa aparecer");
        Assert.True(porque.TokensEntrada > 0);
        Assert.Equal(1, porque.Tentativas);
        Assert.True(porque.Sucesso);
    }

    /// <summary>
    /// O contexto EFETIVAMENTE enviado, e nao o que seria montado hoje: a
    /// pergunta e o que o modelo viu naquele momento.
    /// </summary>
    [Fact]
    public async Task O_porque_mostra_o_contexto_que_foi_ao_modelo()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(ana.Id);

        await EndpointsDoPlano.Sugerir(
            lead.Id, "ObjecaoProvavel", ctx, Agente(), QuemPede.Principal(ana));

        var porque = Assert.IsType<PorQueNaTela>(Valor(await EndpointsDoPlano.PorQue(
            lead.Id, "ObjecaoProvavel", ctx, QuemPede.Principal(ana))));

        Assert.NotNull(porque.ContextoEnviado);
        Assert.Contains(Instrucoes, porque.ContextoEnviado, StringComparison.Ordinal);
        Assert.Contains("ObjecaoProvavel", porque.ContextoEnviado, StringComparison.Ordinal);
    }

    // --- A versao do prompt ---

    [Fact]
    public void Mudar_uma_letra_do_prompt_muda_a_versao()
    {
        // Nao e um numero que alguem incrementa e esquece: ele muda quando o
        // texto muda, e so quando ele muda.
        Assert.NotEqual(VersaoDoPrompt.De("abra a conversa"), VersaoDoPrompt.De("abra a conversa."));
    }

    [Fact]
    public void O_mesmo_prompt_da_sempre_a_mesma_versao()
    {
        Assert.Equal(VersaoDoPrompt.De(Instrucoes), VersaoDoPrompt.De(Instrucoes));
    }

    // --- Quando nao ha o que mostrar ---

    /// <summary>
    /// Inventar uma procedencia plausivel seria pior que dizer que nao ha: o
    /// botao existe justamente para nao pedir confianca cega.
    /// </summary>
    [Fact]
    public async Task Bloco_sem_sugestao_nao_tem_porque()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(ana.Id);

        var resposta = await EndpointsDoPlano.PorQue(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana));

        Assert.Equal(404, Status(resposta));
    }

    [Fact]
    public async Task Descartar_a_sugestao_tira_o_porque_junto()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(ana.Id);

        await EndpointsDoPlano.Sugerir(lead.Id, "Objetivo", ctx, Agente(), QuemPede.Principal(ana));
        await EndpointsDoPlano.Descartar(lead.Id, "Objetivo", ctx, QuemPede.Principal(ana));

        Assert.Equal(404, Status(await EndpointsDoPlano.PorQue(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana))));
    }

    /// <summary>
    /// Aceitar torna a frase TEXTO DELE. Guardar "isto veio da IA" criaria duas
    /// categorias de frase numa tela que existe para ter uma.
    /// </summary>
    [Fact]
    public async Task Aceitar_a_sugestao_encerra_a_procedencia()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(ana.Id);

        await EndpointsDoPlano.Sugerir(lead.Id, "Objetivo", ctx, Agente(), QuemPede.Principal(ana));
        await EndpointsDoPlano.Aceitar(lead.Id, "Objetivo", ctx, QuemPede.Principal(ana));

        Assert.Equal(404, Status(await EndpointsDoPlano.PorQue(
            lead.Id, "Objetivo", ctx, QuemPede.Principal(ana))));
    }

    // --- O escopo vale aqui tambem ---

    [Fact]
    public async Task O_porque_de_lead_alheio_e_barrado()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var doBruno = Semear(bruno.Id);

        await EndpointsDoPlano.Sugerir(
            doBruno.Id, "Objetivo", ctx, Agente(), QuemPede.Principal(bruno));

        Assert.Equal(404, Status(await EndpointsDoPlano.PorQue(
            doBruno.Id, "Objetivo", ctx, QuemPede.Principal(ana))));
    }

    private static object? Valor(IResult resposta) =>
        resposta is IValueHttpResult { Value: var valor } ? valor : null;

    private static int Status(IResult resposta) =>
        resposta is IStatusCodeHttpResult { StatusCode: { } codigo } ? codigo : 200;
}
