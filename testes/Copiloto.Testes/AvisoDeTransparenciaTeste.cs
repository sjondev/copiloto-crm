using Copiloto.Dominio.Titulares;

namespace Copiloto.Testes;

/// <summary>
/// O aviso ao titular (#80), cujo desafio e de formato e nao de conteudo.
///
/// Ninguem le termo de quatro paragrafos no WhatsApp. Aviso que o cliente pula
/// cumpre a formalidade e falha na finalidade — e a finalidade importa porque a
/// base de legitimo interesse depende dela: so se opoe a analise quem sabe que
/// ela existe.
/// </summary>
public class AvisoDeTransparenciaTeste
{
    private const string Empresa = "Torrefação Serra Alta";
    private const string Link = "serraalta.com.br/privacidade";

    [Fact]
    public void O_aviso_curto_cabe_em_uma_frase_que_alguem_le_ate_o_fim()
    {
        var aviso = AvisoDeTransparencia.Curto(Empresa, Link);

        Assert.True(aviso.Length <= AvisoDeTransparencia.TetoDoAvisoCurto,
            $"O aviso tem {aviso.Length} caracteres.");
    }

    [Fact]
    public void O_aviso_curto_diz_as_tres_coisas_que_nao_podem_faltar()
    {
        // Registro, IA, e a pessoa do outro lado. O resto cabe no link.
        var aviso = AvisoDeTransparencia.Curto(Empresa, Link);

        Assert.Contains("registrada", aviso);
        Assert.Contains("IA", aviso);
        Assert.Contains("uma pessoa", aviso);
        Assert.Contains(Link, aviso);
    }

    [Fact]
    public void O_aviso_nomeia_a_empresa_e_nao_fala_em_terceira_pessoa()
    {
        // "Esta conversa e registrada" sem dizer por quem deixa o cliente sem
        // saber de quem cobrar.
        Assert.Contains(Empresa, AvisoDeTransparencia.Curto(Empresa, Link));
    }

    [Fact]
    public void Link_comprido_demais_falha_alto_em_vez_de_estourar_o_aviso()
    {
        // Falhar na configuracao e melhor que mandar ao cliente um bloco que
        // ele nao vai ler: o erro aparece para quem instala, e nao para quem
        // compra.
        var linkEnorme = "empresa.com.br/" + new string('a', 250);

        var erro = Assert.Throws<InvalidOperationException>(
            () => AvisoDeTransparencia.Curto(Empresa, linkEnorme));

        Assert.Contains("Encurte o link", erro.Message);
    }

    [Fact]
    public void Aviso_sem_empresa_ou_sem_link_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => AvisoDeTransparencia.Curto("  ", Link));
        Assert.Throws<ArgumentException>(() => AvisoDeTransparencia.Curto(Empresa, ""));
    }

    [Fact]
    public void A_versao_completa_traz_os_cinco_pontos_da_issue()
    {
        var completo = AvisoDeTransparencia.Completo(Empresa, "atendimento@serraalta.com.br");

        Assert.Contains("O que registramos", completo);
        Assert.Contains("inteligência artificial", completo);
        Assert.Contains("Para quê", completo);
        Assert.Contains("Com quem compartilhamos", completo);
        Assert.Contains("Seus direitos", completo);
    }

    [Fact]
    public void A_versao_completa_diz_que_quem_responde_e_gente()
    {
        // E a vantagem real que este produto tem a comunicar: dizer isso e
        // melhor para a empresa do que ficar calado e o cliente descobrir por
        // conta.
        var completo = AvisoDeTransparencia.Completo(Empresa, "atendimento@serraalta.com.br");

        Assert.Contains("A IA não conversa com você", completo);
        Assert.Contains("escrita por uma pessoa", completo);
    }

    [Fact]
    public void A_versao_completa_informa_o_direito_de_opor_se_a_analise()
    {
        // Sem isto, a base de legitimo interesse fica no papel: o titular nao
        // tem como exercer um direito que ninguem contou que ele tem (#77, #81).
        var completo = AvisoDeTransparencia.Completo(Empresa, "atendimento@serraalta.com.br");

        Assert.Contains("análise por IA pare", completo);
        Assert.Contains("sem perder o atendimento", completo);
    }

    [Fact]
    public void A_versao_completa_evita_juridiques()
    {
        // Linguagem simples e criterio de aceite, e o jeito de verificar e
        // procurar as palavras que so aparecem em contrato.
        var completo = AvisoDeTransparencia.Completo(Empresa, "atendimento@serraalta.com.br");

        foreach (var palavra in new[] { "outrossim", "doravante", "nos termos do", "art." })
            Assert.DoesNotContain(palavra, completo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_cliente_e_avisado_uma_vez_e_nao_a_cada_conversa()
    {
        // Repetir o aviso a cada retomada transforma transparencia em ruido, e
        // ruido e a forma mais eficiente de nao ser lido.
        Assert.True(AvisoDeTransparencia.PrecisaAvisar(null));
        Assert.False(AvisoDeTransparencia.PrecisaAvisar(
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));
    }
}
