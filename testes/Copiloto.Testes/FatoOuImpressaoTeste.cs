using Copiloto.Dominio.Fichas;
using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// Fato e impressao separados na ficha, com procedencia (#88).
///
/// O risco que estes testes cobrem nao e a IA errar: e ela ACERTAR o eco. O
/// palpite do vendedor volta para ele reembalado como analise do sistema, o
/// que confirma o vies original em vez de corrigi-lo — e sai caro, porque cada
/// volta passa por um modelo pago.
/// </summary>
public class FatoOuImpressaoTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static FichaCliente Nova() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    [Fact]
    public void Anotacao_nasce_dizendo_qual_das_duas_e()
    {
        // Nao ha conversao implicita de string: `Ramo = "cafeteria"` nao
        // compila, entao nada entra como fato por omissao.
        Assert.True(Anotacao.Fato("gerente de compras").EhFato);
        Assert.False(Anotacao.Impressao("parece desconfiado").EhFato);
    }

    [Fact]
    public void Fato_carrega_a_fonte_quando_ela_existe()
    {
        var comFonte = Anotacao.Fato("12 lojas", fonte: "site da empresa");

        Assert.Equal("site da empresa", comFonte.Fonte);
        Assert.Contains("fato, site da empresa", comFonte.Rotulado());
    }

    [Fact]
    public void Fonte_e_opcional_no_fato()
    {
        // Exigir fonte faria o vendedor inventar uma para salvar, e fonte
        // inventada e pior que fonte ausente: ela parece verificavel.
        Assert.Equal("é gerente [fato]", Anotacao.Fato("é gerente").Rotulado());
    }

    [Fact]
    public void Impressao_nao_aceita_fonte()
    {
        // A assinatura nao tem o parametro: escrever "site" ao lado de um
        // palpite e o disfarce que a issue existe para impedir.
        var impressao = Anotacao.Impressao("acho que odeia enrolação");

        Assert.Null(impressao.Fonte);
        Assert.Contains("impressão do vendedor", impressao.Rotulado());
    }

    [Fact]
    public void Impressao_datada_diz_de_quando_e()
    {
        // "Isso saiu de uma impressao sua de tres semanas atras" e diferente de
        // uma conclusao sem procedencia: a primeira o vendedor pode contestar.
        var bloco = Anotacao.Impressao("me pareceu apressado", quando: T0).Rotulado();

        Assert.Contains("01/09/2026", bloco);
    }

    [Fact]
    public void A_ficha_separa_o_que_foi_apurado_do_que_foi_percebido()
    {
        var ficha = Nova();
        ficha.Atualizar(T0,
            empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria", "o cliente disse")),
            pessoa: new SobreAPessoa(
                Cargo: Anotacao.Fato("sócio", "LinkedIn"),
                EstiloObservado: Anotacao.Impressao("parece desconfiado")));

        Assert.Equal(2, ficha.Fatos.Count);
        Assert.Single(ficha.Impressoes);
        Assert.Equal("parece desconfiado", ficha.Impressoes["Estilo observado"].Valor);
    }

    [Fact]
    public void No_prompt_as_duas_vao_em_secoes_separadas()
    {
        // Numa lista unica, "Cargo: sócio" e "parece desconfiado" chegam ao
        // modelo com o mesmo peso.
        var ficha = Nova();
        ficha.Atualizar(T0,
            pessoa: new SobreAPessoa(
                Cargo: Anotacao.Fato("sócio", "LinkedIn"),
                EstiloObservado: Anotacao.Impressao("parece desconfiado")));

        var c2 = CamadaC2.Montar(ficha);

        Assert.Contains(CamadaC2.TituloDosFatos, c2);
        Assert.Contains(CamadaC2.TituloDasImpressoes, c2);
        Assert.True(c2.IndexOf(CamadaC2.TituloDosFatos, StringComparison.Ordinal)
                    < c2.IndexOf(CamadaC2.TituloDasImpressoes, StringComparison.Ordinal));
    }

    [Fact]
    public void O_bloco_leva_junto_o_que_pode_ser_feito_com_a_impressao()
    {
        var ficha = Nova();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            EstiloObservado: Anotacao.Impressao("parece apressado")));

        var c2 = CamadaC2.Montar(ficha);

        Assert.Contains(CamadaC2.Instrucao, c2);
        Assert.Contains("impressão do vendedor", c2);
    }

    [Fact]
    public void Ficha_so_de_fatos_nao_carrega_secao_de_impressao_vazia()
    {
        // Titulo sem conteudo gasta token e sugere que ha palpite onde nao ha.
        var ficha = Nova();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        var c2 = CamadaC2.Montar(ficha);

        Assert.Contains(CamadaC2.TituloDosFatos, c2);
        Assert.DoesNotContain(CamadaC2.TituloDasImpressoes, c2);
    }

    // --- O encontro com a ancoragem (#15, #57) ---

    [Fact]
    public void Impressao_nunca_ancora_escassez_prazo_ou_preco()
    {
        // O criterio de aceite. "Parece que ele tem pressa" sustentando um
        // prazo e o palpite do vendedor voltando com a autoridade do sistema.
        var impressao = Anotacao.Impressao("parece que ele tem pressa");

        foreach (var tatica in new[] { Tatica.Escassez, Tatica.Prazo, Tatica.Preco,
                                       Tatica.Desconto, Tatica.ProvaSocial })
        {
            var erro = Assert.Throws<ArgumentException>(
                () => BlocoSugerido.AncoradoEm(tatica, "fala qualquer", impressao));

            Assert.Contains("Perguntar", erro.Message);
        }
    }

    [Fact]
    public void Fato_ancora_e_a_sugestao_diz_de_onde_saiu()
    {
        // O quarto criterio: a ancoragem diz de qual dos dois a sugestao veio.
        var bloco = BlocoSugerido.AncoradoEm(
            Tatica.Prazo, "Entrega em 2 dias úteis",
            Anotacao.Fato("2 dias úteis para a zona sul", "tabela de frete"));

        Assert.False(bloco.EhPergunta);
        Assert.Contains("fato, tabela de frete", bloco.Ancora);
    }

    [Fact]
    public void Impressao_ainda_sustenta_a_tatica_livre()
    {
        // Impressao nao e ruido a descartar: "parece desconfiado" muda a
        // abordagem, e a tatica Livre e onde isso vale.
        var bloco = BlocoSugerido.AncoradoEm(
            Tatica.Livre, "Vale ir direto ao ponto com ele",
            Anotacao.Impressao("parece que odeia enrolação", quando: T0));

        Assert.False(bloco.EhPergunta);
        Assert.Contains("impressão do vendedor em 01/09/2026", bloco.Ancora);
    }
}
