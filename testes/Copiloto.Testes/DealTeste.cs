using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// As transicoes de estagio, e elas moram no dominio (#48).
///
/// Regra de negocio dentro do controller nao cria dependencia errada nenhuma —
/// o grafo depois do commit e identico ao de antes —, entao nenhuma analise de
/// arquitetura acusa. Quem acusa e este arquivo, e ele so consegue existir
/// porque a regra esta num lugar chamavel sem subir a aplicacao.
/// </summary>
public class DealTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Deal NovoDeal() => new(Guid.NewGuid(), Guid.NewGuid(), Agora);

    [Fact]
    public void Deal_nasce_em_Novo_e_com_custo_de_ia_zerado()
    {
        var deal = NovoDeal();

        Assert.Equal(Estagio.Novo, deal.Estagio);
        Assert.Equal(0m, deal.CustoIaAcumulado);
        Assert.Null(deal.FechadoEm);
    }

    [Fact]
    public void O_funil_anda_de_um_em_um()
    {
        var deal = NovoDeal();

        Assert.Null(deal.MoverPara(Estagio.Qualificacao, Agora));
        Assert.Null(deal.MoverPara(Estagio.Proposta, Agora));
        Assert.Equal(Estagio.Proposta, deal.Estagio);
    }

    [Fact]
    public void Pular_estagio_e_recusado_com_motivo()
    {
        var deal = NovoDeal();

        var motivo = deal.MoverPara(Estagio.Negociacao, Agora);

        Assert.NotNull(motivo);
        Assert.Contains("pular", motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Estagio.Novo, deal.Estagio);
    }

    [Fact]
    public void Fechar_e_possivel_de_qualquer_estagio_aberto()
    {
        // Negocio morre a qualquer altura do funil, e obrigar a passar por todos
        // os estagios antes de marcar Perdido faria o dado de funil mentir.
        var deal = NovoDeal();

        Assert.Null(deal.MoverPara(Estagio.Perdido, Agora));
        Assert.Equal(Estagio.Perdido, deal.Estagio);
        Assert.Equal(Agora, deal.FechadoEm);
    }

    [Fact]
    public void Deal_fechado_nao_volta_ao_funil()
    {
        var deal = NovoDeal();
        deal.MoverPara(Estagio.Ganho, Agora);

        var motivo = deal.MoverPara(Estagio.Negociacao, Agora);

        Assert.NotNull(motivo);
        Assert.Equal(Estagio.Ganho, deal.Estagio);
    }

    [Fact]
    public void Repetir_a_mesma_transicao_nao_e_erro()
    {
        // O vendedor clica duas vezes, o webhook reentrega. Tratar como erro
        // encheria a tela de aviso para uma acao que nao mudou nada.
        var deal = NovoDeal();
        deal.MoverPara(Estagio.Qualificacao, Agora);

        Assert.Null(deal.MoverPara(Estagio.Qualificacao, Agora));
        Assert.Equal(Estagio.Qualificacao, deal.Estagio);
    }

    [Fact]
    public void Custo_de_ia_acumula_por_invocacao()
    {
        var deal = NovoDeal();

        deal.RegistrarInvocacao(new AiInvocation(Guid.NewGuid(), "fake", 0.15m, Agora));
        deal.RegistrarInvocacao(new AiInvocation(Guid.NewGuid(), "fake", 0.25m, Agora));

        Assert.Equal(0.40m, deal.CustoIaAcumulado);
        Assert.Equal(2, deal.Invocacoes.Count);
    }

    [Fact]
    public void Custo_negativo_nao_entra_e_nao_reduz_o_acumulado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AiInvocation(Guid.NewGuid(), "fake", -1m, Agora));
    }
}
