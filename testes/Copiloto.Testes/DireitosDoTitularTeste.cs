using Copiloto.Api.Persistencia;
using Copiloto.Api.Titulares;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Titulares;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// Os direitos do art. 18 alem da exclusao (#81).
///
/// O criterio dificil nao e tecnico: o titular tem direito de saber que o
/// sistema o classificou. Protege-se o dado que entrou e esquece-se o que o
/// sistema produziu — e o segundo tambem e dado pessoal sobre ele.
/// </summary>
public class DireitosDoTitularTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private Guid GravarTitularCompleto()
    {
        using var ctx = NovoContexto();
        var leadId = Guid.NewGuid();

        var lead = new Lead(leadId, "+55 11 98888-1111", T0, "Marina");
        var deal = new Deal(Guid.NewGuid(), leadId, T0);
        var conversa = new Conversa(Guid.NewGuid(), leadId);
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor do kg?", T0));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Vendedor, "R$ 68", T0.AddMinutes(4)));

        var ficha = new FichaCliente(Guid.NewGuid(), leadId, T0);
        ficha.Atualizar(T0,
            empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria", "o cliente disse")),
            pessoa: new SobreAPessoa(EstiloObservado: Anotacao.Impressao("parece desconfiada", T0)));

        ctx.Leads.Add(lead);
        ctx.Deals.Add(deal);
        ctx.Conversas.Add(conversa);
        ctx.Fichas.Add(ficha);
        ctx.SaveChanges();

        return leadId;
    }

    private ExportacaoDoTitular Exportador(CopilotoDbContext ctx) =>
        new(ctx, ["Provedor de modelo: nenhum (MODEL_PROVIDER=fake)"]);

    // --- Confirmacao e acesso ---

    [Fact]
    public async Task Confirmacao_responde_sim_ou_nao()
    {
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        Assert.True(await Exportador(ctx).Confirmar(leadId, default));
        Assert.False(await Exportador(ctx).Confirmar(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task O_acesso_traz_conversa_ficha_e_negocio()
    {
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        var dados = await Exportador(ctx).Exportar(leadId, default);

        Assert.NotNull(dados);
        Assert.Equal("Marina", dados!.Nome);
        Assert.Equal(2, dados.Conversas.Count);
        Assert.Equal(2, dados.FichaDoCliente.Count);
        Assert.Single(dados.Negocios);
    }

    [Fact]
    public async Task A_exportacao_diz_o_que_e_fato_e_o_que_e_impressao()
    {
        // Ver "anotaram uma IMPRESSAO de que eu pareco desconfiada" e outra
        // coisa de ver uma lista de campos (#88).
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        var dados = await Exportador(ctx).Exportar(leadId, default);

        Assert.Contains(dados!.FichaDoCliente, a => a.Natureza == "fato" && a.Fonte == "o cliente disse");
        Assert.Contains(dados.FichaDoCliente, a => a.Natureza.StartsWith("impressão"));
        Assert.Contains(dados.Observacoes, o => o.Contains("IMPRESSÕES"));
    }

    [Fact]
    public async Task A_exportacao_explica_as_inferencias_que_nao_estao_no_arquivo()
    {
        // Arquivo que so lista campos deixa o titular concluir que aquilo e
        // tudo. O dossie e recalculado, e ele precisa saber disso para poder
        // contestar a CONCLUSAO, e nao so o dado.
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        var dados = await Exportador(ctx).Exportar(leadId, default);

        Assert.Contains(dados!.Observacoes, o => o.Contains("recalculadas"));
        Assert.Contains(dados.Observacoes, o => o.Contains("contestar"));
    }

    [Fact]
    public async Task O_titular_recebe_com_quem_o_dado_foi_compartilhado()
    {
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        var dados = await Exportador(ctx).Exportar(leadId, default);

        Assert.Contains(dados!.CompartilhadoCom, c => c.Contains("Provedor de modelo"));
    }

    [Fact]
    public async Task Titular_que_nao_existe_nao_vira_arquivo_vazio()
    {
        // Devolver um JSON com campos em branco pareceria "temos um cadastro
        // seu, mas esta vazio" — que e uma resposta errada, e nao uma resposta
        // pobre.
        using var ctx = NovoContexto();

        Assert.Null(await Exportador(ctx).Exportar(Guid.NewGuid(), default));
        Assert.Null(await Exportador(ctx).ExportarComoJson(Guid.NewGuid(), default));
    }

    // --- Portabilidade ---

    [Fact]
    public async Task A_portabilidade_sai_em_json_legivel_por_maquina_e_por_gente()
    {
        var leadId = GravarTitularCompleto();
        using var ctx = NovoContexto();

        var json = await Exportador(ctx).ExportarComoJson(leadId, default);

        Assert.NotNull(json);
        Assert.Contains("\"telefone\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qual o valor do kg?", json);
        Assert.Contains("\n", json);   // identado: quem abre e uma pessoa
    }

    // --- Correcao ---

    [Fact]
    public void Correcao_arruma_o_nome_errado()
    {
        var lead = new Lead(Guid.NewGuid(), "+55 11 98888-1111", T0, "Marinna");

        lead.CorrigirNome("Marina");

        Assert.Equal("Marina", lead.Nome);
    }

    [Fact]
    public void Correcao_vazia_nao_e_jeito_de_apagar_o_nome()
    {
        // Apagar tem outro rito, que e a exclusao (#46).
        var lead = new Lead(Guid.NewGuid(), "+55 11 98888-1111", T0, "Marina");

        Assert.Throws<ArgumentException>(() => lead.CorrigirNome("  "));
        Assert.Equal("Marina", lead.Nome);
    }

    // --- Oposicao ---

    [Fact]
    public void Oposicao_para_a_analise_sem_apagar_o_historico()
    {
        // Se opor-se custasse o historico do negocio, ninguem se oporia — e a
        // base de legitimo interesse ficaria fragil por falta de canal real.
        var lead = new Lead(Guid.NewGuid(), "+55 11 98888-1111", T0, "Marina");

        lead.OporSeAAnalise(T0);

        Assert.True(lead.AnaliseDeIaSuspensa);
        Assert.Equal(T0, lead.OpostoEm);
        Assert.Equal("Marina", lead.Nome);
        Assert.Equal("+55 11 98888-1111", lead.Telefone);
    }

    [Fact]
    public void Quem_se_opos_pode_mudar_de_ideia()
    {
        var lead = new Lead(Guid.NewGuid(), "+55 11 98888-1111", T0);
        lead.OporSeAAnalise(T0);

        lead.RetomarAnalise();

        Assert.False(lead.AnaliseDeIaSuspensa);
        Assert.Null(lead.OpostoEm);
    }

    [Fact]
    public async Task A_oposicao_sobrevive_ao_banco()
    {
        // "Parem de me analisar" que vale ate a proxima subida nao e oposicao.
        var leadId = GravarTitularCompleto();
        using (var ctx = NovoContexto())
        {
            var lead = ctx.Leads.Single(l => l.Id == leadId);
            lead.OporSeAAnalise(T0);
            ctx.SaveChanges();
        }

        using var leitura = NovoContexto();

        Assert.True(leitura.Leads.Single(l => l.Id == leadId).AnaliseDeIaSuspensa);
        Assert.True((await Exportador(leitura).Exportar(leadId, default))!.AnaliseDeIaSuspensa);
    }

    // --- Prazo ---

    [Fact]
    public void O_prazo_de_atendimento_corre_do_recebimento()
    {
        var pedido = new PedidoDoTitular(Guid.NewGuid(), Guid.NewGuid(), TipoDePedido.Acesso, T0);

        Assert.Equal(T0.AddDays(15), pedido.VenceEm);
        Assert.False(pedido.Vencido(T0.AddDays(14)));
        Assert.True(pedido.Vencido(T0.AddDays(16)));
    }

    [Fact]
    public void O_aviso_chega_antes_de_vencer_que_e_quando_ainda_adianta()
    {
        var pedido = new PedidoDoTitular(Guid.NewGuid(), Guid.NewGuid(), TipoDePedido.Portabilidade, T0);

        Assert.False(pedido.VencendoEm(T0.AddDays(9), TimeSpan.FromDays(5)));
        Assert.True(pedido.VencendoEm(T0.AddDays(11), TimeSpan.FromDays(5)));
    }

    [Fact]
    public void Pedido_atendido_nao_vence_depois()
    {
        // Contar atraso de quem ja respondeu encheria o painel de alarme falso
        // e esconderia os pedidos que ainda importam.
        var pedido = new PedidoDoTitular(Guid.NewGuid(), Guid.NewGuid(), TipoDePedido.Acesso, T0)
            .Atender(T0.AddDays(2));

        Assert.False(pedido.Vencido(T0.AddDays(30)));
        Assert.False(pedido.VencendoEm(T0.AddDays(30), TimeSpan.FromDays(5)));
        Assert.Equal(TimeSpan.FromDays(2), pedido.TempoDeResposta);
    }

    [Fact]
    public void Pedido_novo_ainda_nao_tem_tempo_de_resposta()
    {
        var pedido = new PedidoDoTitular(Guid.NewGuid(), Guid.NewGuid(), TipoDePedido.Oposicao, T0);

        Assert.Null(pedido.TempoDeResposta);
        Assert.False(pedido.Atendido);
    }
}
