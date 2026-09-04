using Copiloto.Dominio.Fichas;

namespace Copiloto.Testes;

/// <summary>
/// A Ficha do Cliente como coleta de dado de terceiro (#89).
///
/// O titular da ficha NAO esta na conversa e nao sabe que existe um registro
/// sobre ele: o vendedor pesquisou no LinkedIn, no site, perguntou a um
/// conhecido, e escreveu. E legitimo em B2B — e o que todo CRM faz — mas tem
/// consequencias que precisam estar tratadas, e nao ignoradas.
/// </summary>
public class FichaSobLgpdTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    private static FichaCliente Nova() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    [Theory]
    [InlineData("faz tratamento para ansiedade")]
    [InlineData("é evangélico, não bebe")]
    [InlineData("votei diferente dele, melhor não falar de eleição")]
    [InlineData("é sindicalizado")]
    public void Categoria_sensivel_nao_entra_na_ficha(string anotado)
    {
        // O vetor e realista: quem pesquisa alguem em rede social esbarra em
        // posicionamento politico e religioso sem procurar. Anotar isso e um
        // problema de outra magnitude — exige consentimento especifico, que
        // ninguem pediu ao titular antes de olhar o perfil dele.
        var ficha = Nova();

        var erro = Assert.Throws<ArgumentException>(() => ficha.Atualizar(T0,
            pessoa: new SobreAPessoa(EstiloObservado: Anotacao.Impressao(anotado))));

        Assert.Contains("categoria sensivel", erro.Message);
    }

    [Fact]
    public void O_bloqueio_vale_para_fato_e_para_impressao()
    {
        // Marcar como "fato" nao torna o dado sensivel anotavel: a natureza diz
        // se sustenta afirmacao, nao se pode ser coletado.
        var ficha = Nova();

        Assert.Throws<ArgumentException>(() => ficha.Atualizar(T0,
            negocio: new SobreONegocio(
                RiscoConhecido: Anotacao.Fato("tem gastrite, evita café forte", "ele contou"))));
    }

    [Fact]
    public void A_ficha_nao_fica_pela_metade_quando_o_bloqueio_dispara()
    {
        // A recusa vem antes de qualquer gravacao: meia atualizacao deixaria a
        // ficha num estado que ninguem pediu.
        var ficha = Nova();

        Assert.Throws<ArgumentException>(() => ficha.Atualizar(T0,
            empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")),
            pessoa: new SobreAPessoa(EstiloObservado: Anotacao.Impressao("é espírita"))));

        Assert.True(ficha.EstaVazia);
        Assert.Empty(ficha.Historico);
    }

    [Fact]
    public void O_ramo_do_cliente_PJ_continua_anotavel()
    {
        // A diferenca e juridica, nao de rigor: dado sensivel e sobre pessoa
        // natural. "Ramo: igreja" descreve o cliente, e bloquear isso impediria
        // o vendedor de registrar quem ele atende — o tipo de bloqueio que faz
        // a informacao ir para outro campo, e ai o controle so incomoda.
        var ficha = Nova();

        ficha.Atualizar(T0, empresa: new SobreAEmpresa(
            Ramo: Anotacao.Fato("igreja, compra para o café da comunidade"),
            ComoChegou: Anotacao.Fato("indicação do sindicato dos padeiros")));

        Assert.Equal(2, ficha.Fatos.Count);
    }

    [Fact]
    public void Anotacao_comercial_normal_passa_sem_atrito()
    {
        var ficha = Nova();

        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            Cargo: Anotacao.Fato("gerente de compras", "LinkedIn"),
            EstiloObservado: Anotacao.Impressao("prefere objetividade")));

        Assert.Equal(2, ficha.Preenchidos.Count);
    }

    [Fact]
    public void A_tela_de_edicao_tem_o_que_dizer_ao_vendedor()
    {
        // O criterio que muda o comportamento mais que qualquer politica:
        // saber que o titular pode ler faz escrever "prefere objetividade" em
        // vez de "chato pra caramba".
        Assert.Contains("pedir para ler", FichaCliente.AvisoAoVendedor);
        Assert.Contains("mostraria a ele", FichaCliente.AvisoAoVendedor);
    }

    // --- Retencao ---

    [Fact]
    public void Ficha_de_negocio_ativo_nao_expira_por_tempo()
    {
        // Enquanto ha negociacao, a finalidade esta viva.
        var ficha = Nova();

        Assert.False(ficha.DeveExpurgar(negocioPerdidoEm: null, T0.AddYears(5)));
    }

    [Fact]
    public void Ficha_de_lead_perdido_nao_fica_para_sempre()
    {
        var ficha = Nova();

        Assert.False(ficha.DeveExpurgar(T0, T0.AddDays(364)));
        Assert.True(ficha.DeveExpurgar(T0, T0.AddDays(365)));
    }

    [Fact]
    public void O_prazo_cobre_a_volta_do_cliente_que_disse_agora_nao()
    {
        // A ficha existe para a segunda conversa nao comecar do zero: expurgar
        // em trinta dias jogaria fora exatamente o caso que ela resolve.
        Assert.True(FichaCliente.RetencaoAposPerder >= TimeSpan.FromDays(180));
    }
}
