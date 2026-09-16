using Copiloto.Api.Leitura;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;
using Microsoft.AspNetCore.Http;

namespace Copiloto.Testes;

/// <summary>
/// O escopo por perfil, aplicado (#176).
///
/// A regra existia desde a #49, no `EscopoDeLeitura`, e nao era chamada de lugar
/// nenhum — o mesmo destino do `RequireAuthorization` que a #182 encontrou.
/// Regra escrita e nao aplicada e pior que regra ausente: ela aparece no codigo
/// como se estivesse valendo.
/// </summary>
public class CentralDoGestorTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private int _proximoNumero;

    private Lead Semear(Guid? dono, string nome)
    {
        using var ctx = NovoContexto();

        var lead = new Lead(
            Guid.NewGuid(), $"+55119{(++_proximoNumero):D4}0000", Agora.AddDays(-5), nome);

        if (dono is { } vendedor) lead.Assumir(vendedor);
        ctx.Leads.Add(lead);

        var conversa = new Conversa(Guid.NewGuid(), lead.Id);
        conversa.Registrar(new Mensagem(
            Guid.NewGuid(), Autor.Cliente, "qual o valor do kg?", Agora.AddHours(-2)));
        ctx.Conversas.Add(conversa);

        ctx.SaveChanges();
        return lead;
    }

    // --- A fila ---

    [Fact]
    public async Task O_vendedor_nao_ve_o_lead_de_outro_vendedor()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");

        Semear(dono: ana.Id, nome: "Da Ana");
        Semear(dono: bruno.Id, nome: "Do Bruno");

        var fila = await EndpointsDaFila.Montar(ctx, Agora, ana, CancellationToken.None);

        Assert.Equal(["Da Ana"], fila.Select(l => l.Nome));
    }

    /// <summary>
    /// Lead sem dono aparece para todo mundo, e isso e decisao (#49): ele chega
    /// pelo WhatsApp sem atribuicao, e escondê-lo ate alguem assumir faria a
    /// primeira mensagem de um cliente novo nao aparecer para ninguem.
    /// </summary>
    [Fact]
    public async Task O_lead_sem_dono_aparece_para_o_vendedor()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");

        Semear(dono: null, nome: "Da equipe");

        var fila = await EndpointsDaFila.Montar(ctx, Agora, ana, CancellationToken.None);

        Assert.Equal(["Da equipe"], fila.Select(l => l.Nome));
    }

    [Fact]
    public async Task O_gestor_ve_a_equipe_inteira()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var chefe = QuemPede.Gestor(ctx);

        Semear(dono: ana.Id, nome: "Da Ana");
        Semear(dono: bruno.Id, nome: "Do Bruno");
        Semear(dono: null, nome: "Da equipe");

        var fila = await EndpointsDaFila.Montar(ctx, Agora, chefe, CancellationToken.None);

        Assert.Equal(3, fila.Count);
    }

    // --- A conversa ---

    /// <summary>
    /// 404, e nao 403. "Voce nao pode ver o lead 123" confirma que o 123 existe,
    /// e quem varre ids aprende a carteira do colega sem ler uma conversa.
    /// </summary>
    [Fact]
    public async Task Lead_alheio_responde_igual_a_lead_inexistente()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");
        var bruno = QuemPede.Vendedor(ctx, "Bruno");

        var doBruno = Semear(dono: bruno.Id, nome: "Do Bruno");

        var alheio = await EndpointsDeLeitura.ConversaDoLead(
            doBruno.Id, ctx, QuemPede.Principal(ana));

        var inexistente = await EndpointsDeLeitura.ConversaDoLead(
            Guid.NewGuid(), ctx, QuemPede.Principal(ana));

        Assert.Equal(Status(inexistente), Status(alheio));
        Assert.Equal(404, Status(alheio));
    }

    [Fact]
    public async Task O_gestor_abre_a_conversa_de_qualquer_lead_da_equipe()
    {
        using var ctx = NovoContexto();
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var chefe = QuemPede.Gestor(ctx);

        var doBruno = Semear(dono: bruno.Id, nome: "Do Bruno");

        var resposta = await EndpointsDeLeitura.ConversaDoLead(
            doBruno.Id, ctx, QuemPede.Principal(chefe));

        Assert.Equal(200, Status(resposta));
    }

    // --- A trilha ---

    [Fact]
    public async Task O_gestor_lendo_conversa_alheia_deixa_rastro()
    {
        using var ctx = NovoContexto();
        var bruno = QuemPede.Vendedor(ctx, "Bruno");
        var chefe = QuemPede.Gestor(ctx);

        var doBruno = Semear(dono: bruno.Id, nome: "Do Bruno");

        await EndpointsDeLeitura.ConversaDoLead(doBruno.Id, ctx, QuemPede.Principal(chefe));

        using var conferindo = NovoContexto();
        var acesso = Assert.Single(conferindo.Acessos);

        Assert.Equal(chefe.Id, acesso.UsuarioId);
        Assert.Equal(doBruno.Id, acesso.LeadId);
        Assert.Contains("gestor", acesso.Detalhe!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registrar o vendedor abrindo o PROPRIO lead encheria a tabela com a
    /// operacao normal do dia e afogaria o que a trilha existe para achar.
    /// Trilha que registra tudo e trilha que ninguem consulta.
    /// </summary>
    [Fact]
    public async Task O_vendedor_abrindo_o_proprio_lead_nao_vira_linha_na_trilha()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");

        var dela = Semear(dono: ana.Id, nome: "Da Ana");

        await EndpointsDeLeitura.ConversaDoLead(dela.Id, ctx, QuemPede.Principal(ana));

        using var conferindo = NovoContexto();
        Assert.Empty(conferindo.Acessos);
    }

    [Fact]
    public async Task Abrir_lead_da_fila_comum_nao_vira_linha_na_trilha()
    {
        using var ctx = NovoContexto();
        var ana = QuemPede.Vendedor(ctx, "Ana");

        var semDono = Semear(dono: null, nome: "Da equipe");

        await EndpointsDeLeitura.ConversaDoLead(semDono.Id, ctx, QuemPede.Principal(ana));

        using var conferindo = NovoContexto();
        Assert.Empty(conferindo.Acessos);
    }

    private static int Status(IResult resposta) =>
        resposta is IStatusCodeHttpResult { StatusCode: { } codigo } ? codigo : 200;
}
