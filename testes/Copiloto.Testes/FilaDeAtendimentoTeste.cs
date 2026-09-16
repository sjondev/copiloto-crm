using Copiloto.Api.Leitura;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// A ordem da fila (#174).
///
/// A lista nao existe para mostrar todo mundo: ela responde "quem eu atendo
/// agora?". Por isso a ORDEM e a funcionalidade, e nao enfeite — lista
/// alfabetica de trezentos leads responde "todos", que e o mesmo que nao
/// responder. Os testes abaixo fixam essa ordem.
/// </summary>
public class FilaDeAtendimentoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    // De INSTANCIA, e nao estatico: cada teste recebe um banco novo, e um
    // contador compartilhado faria o telefone depender de quem rodou antes.
    private int _proximoNumero;

    /// <summary>Um lead com conversa, negocio e, se pedido, a ultima fala do cliente.</summary>
    private Guid Semear(
        string nome,
        DateTimeOffset? ultimaFalaDoCliente = null,
        Estagio estagio = Estagio.Novo,
        DateTimeOffset? estagioDesde = null,
        Termometro? termometro = null,
        bool analiseSuspensa = false)
    {
        using var ctx = NovoContexto();

        var telefone = $"+55119{(++_proximoNumero):D4}0000";
        var lead = new Lead(Guid.NewGuid(), telefone, Agora.AddDays(-60), nome);
        if (analiseSuspensa) lead.OporSeAAnalise(Agora.AddDays(-1));
        ctx.Leads.Add(lead);

        var deal = new Deal(Guid.NewGuid(), lead.Id, Agora.AddDays(-60));
        if (estagio != Estagio.Novo) deal.MoverPara(estagio, estagioDesde ?? Agora.AddDays(-1));
        ctx.Deals.Add(deal);

        var conversa = new Conversa(Guid.NewGuid(), lead.Id);
        if (ultimaFalaDoCliente is { } quando)
        {
            conversa.Registrar(new Mensagem(
                Guid.NewGuid(), Autor.Cliente, "vou ver com meu socio", quando));
        }

        ctx.Conversas.Add(conversa);

        if (termometro is not null)
        {
            var dossie = new Dossie(Guid.NewGuid(), deal.Id, Agora.AddHours(-1));
            dossie.Ler(termometro);
            dossie.Registrar(new Objecao(
                TipoDeObjecao.Preco, "achou caro", "ta caro", Guid.NewGuid(), false));
            ctx.Dossies.Add(dossie);
        }

        ctx.SaveChanges();
        return lead.Id;
    }

    /// <summary>Um gestor: enxerga tudo, entao os testes de ORDEM nao viram testes de escopo.</summary>
    private static Usuario Gestor() => new(
        Guid.NewGuid(), "Gestor", "gestor@copiloto.local",
        new string('h', Usuario.TamanhoMinimoDoHash), PerfilDeAcesso.Gestor);

    private IReadOnlyList<LeadNaFila> Fila(Usuario? quem = null)
    {
        using var ctx = NovoContexto();
        return EndpointsDaFila.Montar(ctx, Agora, quem ?? Gestor(), CancellationToken.None).Result;
    }

    // --- A ordem ---

    [Fact]
    public void Quem_tem_alerta_vem_antes_de_quem_nao_tem()
    {
        Semear("Sem alerta", ultimaFalaDoCliente: Agora.AddHours(-2));
        Semear("Em silencio", ultimaFalaDoCliente: Agora.AddDays(-9));

        Assert.Equal("Em silencio", Fila()[0].Nome);
    }

    [Fact]
    public void Entre_dois_calados_o_que_espera_ha_mais_tempo_vem_primeiro()
    {
        Semear("Calado ha 4 dias", ultimaFalaDoCliente: Agora.AddDays(-4));
        Semear("Calado ha 20 dias", ultimaFalaDoCliente: Agora.AddDays(-20));

        Assert.Equal("Calado ha 20 dias", Fila()[0].Nome);
    }

    /// <summary>
    /// Silencio na frente de proposta envelhecendo porque e o unico que PIORA
    /// sozinho: a proposta continua onde esta, e o cliente calado esta sendo
    /// atendido por outro fornecedor enquanto ninguem liga.
    /// </summary>
    [Fact]
    public void Cliente_calado_vem_antes_de_proposta_envelhecendo()
    {
        Semear("Proposta velha",
            ultimaFalaDoCliente: Agora.AddHours(-1),
            estagio: Estagio.Proposta,
            estagioDesde: Agora.AddDays(-30));

        Semear("Calado", ultimaFalaDoCliente: Agora.AddDays(-5));

        var fila = Fila();

        Assert.Equal("Calado", fila[0].Nome);
        Assert.Equal("Proposta velha", fila[1].Nome);
    }

    // --- O que cada linha carrega ---

    [Fact]
    public void A_linha_traz_o_alerta_com_a_fala_que_o_originou()
    {
        Semear("Calado", ultimaFalaDoCliente: Agora.AddDays(-9));

        var linha = Fila()[0];

        Assert.Equal("ClienteEmSilencio", linha.Alerta);

        // A citacao segue a regra do dossie: sem a fala, o alerta e opiniao do
        // sistema e o vendedor so pode aceitar ou ignorar.
        Assert.Contains("vou ver com meu socio", linha.Motivo, StringComparison.Ordinal);
        Assert.Equal(9, linha.DiasEmSilencio);
    }

    [Fact]
    public void A_linha_traz_temperatura_e_objecao_do_ultimo_dossie()
    {
        Semear("Com leitura",
            ultimaFalaDoCliente: Agora.AddHours(-2),
            termometro: new Termometro(Temperatura.Morna, Direcao.Esfriando));

        var linha = Fila()[0];

        Assert.Equal("morna e esfriando", linha.Temperatura);
        Assert.Equal("Preco", linha.Objecao);
    }

    /// <summary>
    /// `NaoClassificada` e informacao na tela do lead, onde vem com a fala
    /// citada ao lado. Na lista, reduzida a um rotulo solto, vira
    /// "naoclassificada" pendurado no nome do cliente — e rotulo que nao diz
    /// nada ensina o vendedor a parar de ler os que dizem.
    /// </summary>
    [Fact]
    public void Objecao_sem_tipo_nao_vira_rotulo_na_lista()
    {
        using (var ctx = NovoContexto())
        {
            var lead = new Lead(Guid.NewGuid(), "+5511990001111", Agora.AddDays(-2), "Sem tipo");
            ctx.Leads.Add(lead);

            var deal = new Deal(Guid.NewGuid(), lead.Id, Agora.AddDays(-2));
            ctx.Deals.Add(deal);

            var dossie = new Dossie(Guid.NewGuid(), deal.Id, Agora.AddHours(-1));
            dossie.Registrar(new Objecao(
                TipoDeObjecao.NaoClassificada, "resiste, nao da para dizer de que",
                "hmm", Guid.NewGuid(), true));
            ctx.Dossies.Add(dossie);

            ctx.SaveChanges();
        }

        Assert.Null(Fila()[0].Objecao);
    }

    /// <summary>
    /// O vendedor precisa saber ANTES de abrir que aquele dossie nao vai
    /// atualizar, senao ele espera uma leitura que nunca vem (#81).
    /// </summary>
    [Fact]
    public void Quem_se_opos_a_analise_aparece_marcado_na_lista()
    {
        Semear("Opositor", ultimaFalaDoCliente: Agora.AddHours(-2), analiseSuspensa: true);

        Assert.True(Fila()[0].AnaliseSuspensa);
    }

    // --- O que nao pode quebrar ---

    [Fact]
    public void Lead_sem_conversa_nenhuma_nao_derruba_a_fila()
    {
        Semear("Recem chegado");

        var linha = Fila()[0];

        Assert.Null(linha.UltimaFala);
        Assert.Null(linha.DiasEmSilencio);
    }

    [Fact]
    public void Banco_vazio_devolve_lista_vazia_e_nao_erro()
    {
        Assert.Empty(Fila());
    }
}
