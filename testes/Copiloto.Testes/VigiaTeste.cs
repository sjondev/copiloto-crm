using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;
using Copiloto.Dominio.Vigia;

namespace Copiloto.Testes;

/// <summary>
/// O agente A6, que varre em vez de responder (#53).
///
/// Negocio esquecido nao gera evento, nao abre tela e nao chega por webhook —
/// ele so fica parado. E a forma mais barata de perder venda ja qualificada.
/// </summary>
public class VigiaTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Deal DealNovo() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    private static Conversa ComFalaDoCliente(string texto, DateTimeOffset quando)
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, texto, quando));
        return conversa;
    }

    [Fact]
    public void Cliente_calado_ha_dias_vira_alerta_que_cita_a_ultima_fala()
    {
        // "Alerta cita o motivo e o dado que o originou": sem a citacao, o
        // vendedor so pode aceitar ou ignorar — com ela, ele discorda.
        var alertas = Vigia.Varrer(
            DealNovo(), ComFalaDoCliente("vou pensar", T0), T0.AddDays(4)).ToList();

        var silencio = Assert.Single(alertas, a => a.Motivo == MotivoDeAlerta.ClienteEmSilencio);
        Assert.Contains("4 dias", silencio.Texto);
        Assert.Contains("vou pensar", silencio.Texto);
    }

    [Fact]
    public void Silencio_curto_nao_incomoda_ninguem()
    {
        var alertas = Vigia.Varrer(
            DealNovo(), ComFalaDoCliente("vou pensar", T0), T0.AddHours(20));

        Assert.Empty(alertas);
    }

    [Fact]
    public void Negocio_parado_no_mesmo_estagio_vira_alerta()
    {
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0);

        var alertas = Vigia.Varrer(deal, conversa: null, T0.AddDays(11)).ToList();

        var parado = Assert.Single(alertas);
        Assert.Equal(MotivoDeAlerta.NegocioParado, parado.Motivo);
        Assert.Contains("Qualificacao", parado.Texto);
        Assert.Contains("11 dias", parado.Texto);
    }

    [Fact]
    public void O_relogio_do_parado_conta_da_ultima_mudanca_e_nao_da_abertura()
    {
        // Negocio de dois meses que andou ontem NAO esta parado.
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0.AddDays(60));

        Assert.Empty(Vigia.Varrer(deal, conversa: null, T0.AddDays(61)));
    }

    [Fact]
    public void Proposta_esfria_mais_rapido_que_o_resto_do_funil()
    {
        // O cliente pediu preco, recebeu, e cada dia sem resposta e uma
        // comparacao a mais com a concorrencia.
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0);
        deal.MoverPara(Estagio.Proposta, T0);

        var alertas = Vigia.Varrer(deal, conversa: null, T0.AddDays(6)).ToList();

        Assert.Single(alertas, a => a.Motivo == MotivoDeAlerta.PropostaEnvelhecendo);
    }

    [Fact]
    public void Proposta_velha_nao_gera_dois_alertas_pelo_mesmo_acontecimento()
    {
        // Quem esta em Proposta ha 12 dias tambem esta "parado ha 12 dias":
        // mandar as duas linhas cobraria em dobro a atencao do vendedor pelo
        // mesmo fato.
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0);
        deal.MoverPara(Estagio.Proposta, T0);

        var alertas = Vigia.Varrer(deal, conversa: null, T0.AddDays(12)).ToList();

        Assert.Single(alertas);
        Assert.Equal(MotivoDeAlerta.PropostaEnvelhecendo, alertas[0].Motivo);
    }

    [Fact]
    public void Deal_fechado_nao_gera_alerta()
    {
        // Alerta sobre negocio encerrado e o ruido que ensina o vendedor a
        // ignorar a lista inteira.
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0);
        deal.MoverPara(Estagio.Proposta, T0);
        deal.MoverPara(Estagio.Ganho, T0);

        Assert.Empty(Vigia.Varrer(deal, ComFalaDoCliente("fechado!", T0), T0.AddDays(30)));
    }

    [Fact]
    public void Deal_sem_conversa_ainda_e_vigiado_pelo_estagio()
    {
        var deal = DealNovo();
        deal.MoverPara(Estagio.Qualificacao, T0);

        Assert.NotEmpty(Vigia.Varrer(deal, conversa: null, T0.AddDays(15)));
    }

    // --- Nao avisar duas vezes ---

    [Fact]
    public void O_mesmo_alerta_nao_e_dado_duas_vezes()
    {
        // O job roda de hora em hora: sem isto, o vendedor recebe o mesmo aviso
        // 24 vezes por dia e aprende a fechar a lista sem ler.
        var deal = DealNovo();
        var conversa = ComFalaDoCliente("vou pensar", T0);
        var jaAvisados = new HashSet<string>();

        var primeira = Vigia.Novos(Vigia.Varrer(deal, conversa, T0.AddDays(4)), jaAvisados);
        Assert.Single(primeira);

        var segunda = Vigia.Novos(Vigia.Varrer(deal, conversa, T0.AddDays(5)), jaAvisados);
        Assert.Empty(segunda);
    }

    [Fact]
    public void Cliente_que_volta_a_falar_e_some_de_novo_gera_alerta_novo()
    {
        // E o ponto do marco na chave: o silencio passa a contar de OUTRA fala,
        // entao aquilo e outro acontecimento — nao a repeticao do anterior.
        var deal = DealNovo();
        var conversa = ComFalaDoCliente("vou pensar", T0);
        var jaAvisados = new HashSet<string>();

        Assert.Single(Vigia.Novos(Vigia.Varrer(deal, conversa, T0.AddDays(4)), jaAvisados));

        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "voltei", T0.AddDays(5)));

        Assert.Single(Vigia.Novos(Vigia.Varrer(deal, conversa, T0.AddDays(9)), jaAvisados));
    }

    [Fact]
    public void Alertas_de_deals_diferentes_nao_se_cancelam()
    {
        var jaAvisados = new HashSet<string>();
        var conversa = ComFalaDoCliente("vou pensar", T0);

        Assert.Single(Vigia.Novos(Vigia.Varrer(DealNovo(), conversa, T0.AddDays(4)), jaAvisados));
        Assert.Single(Vigia.Novos(Vigia.Varrer(DealNovo(), conversa, T0.AddDays(4)), jaAvisados));
    }

    [Fact]
    public void O_deal_registra_desde_quando_esta_no_estagio()
    {
        var deal = DealNovo();
        Assert.Equal(T0, deal.EstagioDesde);

        deal.MoverPara(Estagio.Qualificacao, T0.AddDays(3));

        Assert.Equal(T0.AddDays(3), deal.EstagioDesde);
    }

    [Fact]
    public void Transicao_recusada_nao_mexe_no_relogio_do_estagio()
    {
        // Arrastar o card para um estagio invalido nao pode "zerar" o parado —
        // seria uma forma silenciosa de o negocio nunca mais ser vigiado.
        var deal = DealNovo();

        // Novo -> Negociacao pula dois estagios, e o Deal recusa.
        Assert.NotNull(deal.MoverPara(Estagio.Negociacao, T0.AddDays(5)));

        Assert.Equal(T0, deal.EstagioDesde);
    }
}
