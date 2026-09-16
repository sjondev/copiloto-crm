using Copiloto.Api.Ingestao;
using Microsoft.Extensions.Configuration;

namespace Copiloto.Testes;

/// <summary>
/// A porta de entrada de conversa (#17): quem a configuracao escolhe, e o que o
/// nucleo recebe depois que a fonte traduziu.
/// </summary>
public class FonteDeConversaTeste
{
    private static IConfiguration Config(string? fonte) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(fonte is null
                ? []
                : new Dictionary<string, string?> { [FonteDeConversa.Chave] = fonte })
            .Build();

    [Fact]
    public void Sem_configuracao_a_fonte_e_o_fake()
    {
        // Clone novo sobe sem credencial de provedor nenhum, e a suite roda
        // offline: o padrao nao e comodidade, e o que o CLAUDE.md exige.
        Assert.IsType<FakeSource>(FonteDeConversa.Escolher(Config(null)));
    }

    [Theory]
    [InlineData("fake")]
    [InlineData("FAKE")]
    [InlineData("  fake  ")]
    public void Nome_da_fonte_nao_depende_de_caixa_nem_de_espaco(string escrito)
    {
        // O valor vem de .env editado a mao, onde espaco sobrando e caixa
        // trocada sao o erro mais comum — e cair no ArgumentException por causa
        // de um espaco seria hostil sem motivo.
        Assert.IsType<FakeSource>(FonteDeConversa.Escolher(Config(escrito)));
    }

    [Fact]
    public void Fonte_desconhecida_derruba_a_subida_em_vez_de_cair_no_fake()
    {
        // O pior desfecho possivel: a API sobe saudavel, /saude responde ok, e
        // as falas dos clientes nunca chegam — sem erro, sem log, sem ninguem
        // para notar antes do fim do mes.
        var erro = Assert.Throws<ArgumentException>(() => FonteDeConversa.Escolher(Config("cloudpai")));

        Assert.Contains("cloudpai", erro.Message);
        Assert.Contains("fake, waha, cloudapi", erro.Message);
    }

    [Theory]
    [InlineData("waha")]
    [InlineData("cloudapi")]
    public void Fonte_sem_adaptador_diz_isso_em_vez_de_fingir(string fonte)
    {
        // Configurada mas nao implementada e diferente de inexistente, e a
        // mensagem precisa separar as duas: uma e erro de digitacao, a outra e
        // trabalho que ainda nao foi feito.
        var erro = Assert.Throws<NotSupportedException>(() => FonteDeConversa.Escolher(Config(fonte)));

        Assert.Contains(fonte, erro.Message);
        Assert.Contains("doc oficial", erro.Message);
    }

    [Fact]
    public void Fonte_entrega_a_fala_no_formato_do_nucleo()
    {
        IConversationSource fonte = new FakeSource();

        var falas = fonte.Traduzir("""
            {"providerMessageId":"wamid.1","de":"+5511988887777",
             "para":"+551133334444","texto":"qual o valor?",
             "enviadaEm":"2026-09-01T12:00:00+00:00"}
            """);

        var fala = Assert.Single(falas);
        Assert.Equal("wamid.1", fala.ProviderMessageId);
        Assert.Equal("qual o valor?", fala.Texto);
    }

    [Fact]
    public void Lote_chega_inteiro_e_nao_so_a_primeira_fala()
    {
        // Seis baloes em quatro segundos chegam num payload so (#19). Quem le
        // uma de cada vez perde cinco falas sem erro nenhum aparecer.
        IConversationSource fonte = new FakeSource();

        var falas = fonte.Traduzir("""
            [{"providerMessageId":"wamid.1","de":"+5511988887777","para":"+551133334444",
              "texto":"oi","enviadaEm":"2026-09-01T12:00:00+00:00"},
             {"providerMessageId":"wamid.2","de":"+5511988887777","para":"+551133334444",
              "texto":"tudo bem?","enviadaEm":"2026-09-01T12:00:02+00:00"}]
            """);

        Assert.Equal(2, falas.Count);
        Assert.Equal("tudo bem?", falas[1].Texto);
    }

    [Fact]
    public void Payload_sem_fala_nao_e_erro()
    {
        // Confirmacao de leitura e mudanca de status sao a maior parte do
        // trafego real. Tratar isso como falha encheria o log de erro que nao e
        // erro, e log assim ninguem le mais.
        IConversationSource fonte = new FakeSource();

        Assert.Empty(fonte.Traduzir("[]"));
        Assert.Empty(fonte.Traduzir("   "));
    }

    [Fact]
    public void Fala_sem_id_do_provedor_nao_entra_na_fila()
    {
        var semId = new MensagemRecebida("", "+5511988887777", "+551133334444", "oi", DateTimeOffset.UtcNow);

        Assert.Contains("ProviderMessageId", semId.PorQueNaoEntra());
    }

    [Fact]
    public void Fala_sem_os_dois_numeros_nao_entra_na_fila()
    {
        // Sem De e Para nao da para dizer quem falou, e o falante sai da
        // comparacao com o numero da empresa (#22).
        var semPara = new MensagemRecebida("wamid.1", "+5511988887777", " ", "oi", DateTimeOffset.UtcNow);

        Assert.Contains("quem falou", semPara.PorQueNaoEntra());
    }

    [Fact]
    public void Fala_completa_entra()
    {
        var completa = new MensagemRecebida(
            "wamid.1", "+5511988887777", "+551133334444", "oi", DateTimeOffset.UtcNow);

        Assert.Null(completa.PorQueNaoEntra());
    }
}
