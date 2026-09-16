using Copiloto.Api.Leitura;
using Copiloto.Dominio.Planos;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Http;

namespace Copiloto.Testes;

/// <summary>
/// O editor do plano atravessando a persistencia e o porteiro (#12).
///
/// O teste de dominio prova as regras; este prova que elas sobrevivem ao banco e
/// ao escopo por perfil — e que o plano de outro vendedor responde igual a lead
/// inexistente (#176).
/// </summary>
public class EditorDoPlanoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private int _proximoNumero;

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

    // --- Abrir ---

    /// <summary>
    /// Plano que nao existe e plano EM BRANCO. Exigir um "criar plano" antes de
    /// escrever seria um clique que nao decide nada.
    /// </summary>
    [Fact]
    public async Task O_plano_nasce_na_primeira_vez_que_a_tela_abre()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        var resposta = await EndpointsDoPlano.PlanoDoLead(lead.Id, ctx, QuemPede.Principal(ana));

        var plano = Assert.IsType<PlanoNaTela>(Valor(resposta));
        Assert.Equal(4, plano.Blocos.Count);
        Assert.All(plano.Blocos, b => Assert.Equal("", b.Texto));
        Assert.Equal(1, plano.Versao);
    }

    [Fact]
    public async Task Abrir_duas_vezes_nao_cria_dois_planos()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        await EndpointsDoPlano.PlanoDoLead(lead.Id, ctx, QuemPede.Principal(ana));
        await EndpointsDoPlano.PlanoDoLead(lead.Id, ctx, QuemPede.Principal(ana));

        using var conferindo = NovoContexto();
        Assert.Single(conferindo.Planos);
    }

    // --- Escrever ---

    [Fact]
    public async Task O_que_o_vendedor_escreve_sobrevive_ao_restart()
    {
        Guid leadId;
        Usuario ana;

        using (var ctx = NovoContexto())
        {
            ana = QuemPede.Vendedor(ctx, "Ana");
            leadId = Semear(dono: ana.Id).Id;

            await EndpointsDoPlano.EscreverBloco(
                leadId, "Objetivo", new TextoDoBloco("fechar 5kg esta semana"),
                ctx, QuemPede.Principal(ana));
        }

        // Contexto novo = processo novo, para o efeito deste teste.
        using (var ctx = NovoContexto())
        {
            var resposta = await EndpointsDoPlano.PlanoDoLead(
                leadId, ctx, QuemPede.Principal(ana));

            var plano = Assert.IsType<PlanoNaTela>(Valor(resposta));
            var objetivo = Assert.Single(plano.Blocos, b => b.Bloco == "Objetivo");

            Assert.Equal("fechar 5kg esta semana", objetivo.Texto);
            Assert.Equal(2, plano.Versao);
        }
    }

    [Fact]
    public async Task Bloco_que_nao_existe_e_recusado_com_a_lista_dos_que_existem()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        var resposta = await EndpointsDoPlano.EscreverBloco(
            lead.Id, "Chutando", new TextoDoBloco("qualquer coisa"),
            ctx, QuemPede.Principal(ana));

        Assert.Equal(400, Status(resposta));
    }

    /// <summary>A tese: o plano inteiro se escreve sem nenhuma chamada de modelo.</summary>
    [Fact]
    public async Task Os_quatro_blocos_se_escrevem_sem_IA_nenhuma()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var lead = Semear(dono: ana.Id);

        foreach (var bloco in Enum.GetNames<BlocoDoPlano>())
        {
            await EndpointsDoPlano.EscreverBloco(
                lead.Id, bloco, new TextoDoBloco($"escrito em {bloco}"),
                ctx, QuemPede.Principal(ana));
        }

        var plano = Assert.IsType<PlanoNaTela>(
            Valor(await EndpointsDoPlano.PlanoDoLead(lead.Id, ctx, QuemPede.Principal(ana))));

        Assert.All(plano.Blocos, b => Assert.NotEmpty(b.Texto));
        Assert.All(plano.Blocos, b => Assert.Null(b.Sugestao));
    }

    // --- O escopo vale aqui tambem ---

    [Fact]
    public async Task O_plano_de_lead_alheio_responde_como_lead_inexistente()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var doBruno = Semear(dono: bruno.Id);

        var alheio = await EndpointsDoPlano.PlanoDoLead(
            doBruno.Id, ctx, QuemPede.Principal(ana));

        var inexistente = await EndpointsDoPlano.PlanoDoLead(
            Guid.NewGuid(), ctx, QuemPede.Principal(ana));

        Assert.Equal(404, Status(alheio));
        Assert.Equal(Status(inexistente), Status(alheio));
    }

    [Fact]
    public async Task Escrever_no_plano_alheio_tambem_e_barrado()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var doBruno = Semear(dono: bruno.Id);

        var resposta = await EndpointsDoPlano.EscreverBloco(
            doBruno.Id, "Objetivo", new TextoDoBloco("plano do colega"),
            ctx, QuemPede.Principal(ana));

        Assert.Equal(404, Status(resposta));

        using var conferindo = NovoContexto();
        Assert.Empty(conferindo.Planos);
    }

    private static int Status(IResult resposta) =>
        resposta is IStatusCodeHttpResult { StatusCode: { } codigo } ? codigo : 200;

    private static object? Valor(IResult resposta) =>
        resposta is IValueHttpResult { Value: var valor } ? valor : null;
}
