using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// O plano de abordagem (#12).
///
/// E a tela onde o vendedor e o protagonista, e o criterio que prova isso e o
/// ultimo da issue: funciona sem nunca clicar em sugerir. Os testes abaixo estao
/// na ordem dessa ideia — primeiro o plano sem IA nenhuma, depois a IA como
/// insumo.
/// </summary>
public class PlanoDeAbordagemTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static PlanoDeAbordagem Novo() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    // --- Sem IA nenhuma ---

    [Fact]
    public void O_plano_nasce_em_branco_nos_quatro_blocos()
    {
        var plano = Novo();

        Assert.True(plano.EstaVazio);
        Assert.Equal(4, plano.Blocos.Count);
        Assert.All(plano.Blocos.Values, b => Assert.Equal("", b.Texto));
    }

    [Fact]
    public void O_vendedor_escreve_e_o_plano_deixa_de_estar_vazio()
    {
        var plano = Novo();

        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg esta semana", T0);

        Assert.Equal("fechar 5kg esta semana", plano[BlocoDoPlano.Objetivo].Texto);
        Assert.False(plano.EstaVazio);
    }

    /// <summary>
    /// A tese, em forma de teste: o plano inteiro funciona sem uma chamada de
    /// modelo. Se este teste precisar de IA um dia, a tese mudou.
    /// </summary>
    [Fact]
    public void Um_plano_completo_se_escreve_sem_nenhuma_sugestao()
    {
        var plano = Novo();

        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg", T0);
        plano.Escrever(BlocoDoPlano.PrecisoDescobrir, "quem decide", T0);
        plano.Escrever(BlocoDoPlano.ObjecaoProvavel, "vai achar caro", T0);
        plano.Escrever(BlocoDoPlano.ProximoPasso, "mandar amostra", T0);

        Assert.All(plano.Blocos.Values, b => Assert.NotEmpty(b.Texto));
        Assert.All(plano.Blocos.Values, b => Assert.False(b.TemSugestaoPendente));
    }

    // --- Versao ---

    [Fact]
    public void O_plano_em_branco_ja_e_a_versao_1()
    {
        // Zero diria "este plano nao existe", e ele existe: esta em branco.
        Assert.Equal(1, Novo().Versao);
    }

    [Fact]
    public void Cada_mudanca_do_vendedor_conta_versao()
    {
        var plano = Novo();

        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg", T0);
        plano.Escrever(BlocoDoPlano.ProximoPasso, "mandar amostra", T0);

        Assert.Equal(3, plano.Versao);
    }

    /// <summary>
    /// Salvar automatico a cada tecla criaria centenas de versoes identicas, e a
    /// versao deixaria de significar "ele mudou de ideia".
    /// </summary>
    [Fact]
    public void Escrever_o_mesmo_texto_de_novo_nao_conta_versao()
    {
        var plano = Novo();

        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg", T0);
        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg", T0.AddMinutes(1));
        plano.Escrever(BlocoDoPlano.Objetivo, "  fechar 5kg  ", T0.AddMinutes(2));

        Assert.Equal(2, plano.Versao);
    }

    // --- A IA como insumo ---

    /// <summary>
    /// Sugestao que cai dentro do texto do vendedor apaga o que ele pensou, e
    /// transforma "aceitar, editar ou ignorar" em "desfazer".
    /// </summary>
    [Fact]
    public void A_sugestao_chega_em_campo_separado_e_nao_toca_no_texto()
    {
        var plano = Novo();
        plano.Escrever(BlocoDoPlano.ObjecaoProvavel, "vai achar caro", T0);

        plano.Sugerir(BlocoDoPlano.ObjecaoProvavel, "ele comparou com o concorrente", T0);

        Assert.Equal("vai achar caro", plano[BlocoDoPlano.ObjecaoProvavel].Texto);
        Assert.Equal("ele comparou com o concorrente", plano[BlocoDoPlano.ObjecaoProvavel].Sugestao);
    }

    /// <summary>
    /// A versao responde "o que ele tinha escrito quando falou com o cliente".
    /// Sugestao que ele ignorou nunca fez parte do plano.
    /// </summary>
    [Fact]
    public void Sugestao_sozinha_nao_conta_versao()
    {
        var plano = Novo();

        plano.Sugerir(BlocoDoPlano.Objetivo, "fechar 5kg esta semana", T0);

        Assert.Equal(1, plano.Versao);
    }

    [Fact]
    public void Aceitar_promove_a_sugestao_a_texto_do_vendedor()
    {
        var plano = Novo();
        plano.Sugerir(BlocoDoPlano.Objetivo, "fechar 5kg esta semana", T0);

        plano.Aceitar(BlocoDoPlano.Objetivo, T0.AddMinutes(1));

        Assert.Equal("fechar 5kg esta semana", plano[BlocoDoPlano.Objetivo].Texto);
        Assert.False(plano[BlocoDoPlano.Objetivo].TemSugestaoPendente);
        Assert.Equal(2, plano.Versao);
    }

    [Fact]
    public void Descartar_tira_a_sugestao_sem_tocar_no_texto_nem_na_versao()
    {
        var plano = Novo();
        plano.Escrever(BlocoDoPlano.Objetivo, "o meu texto", T0);
        plano.Sugerir(BlocoDoPlano.Objetivo, "o texto da IA", T0);

        plano.Descartar(BlocoDoPlano.Objetivo, T0.AddMinutes(1));

        Assert.Equal("o meu texto", plano[BlocoDoPlano.Objetivo].Texto);
        Assert.False(plano[BlocoDoPlano.Objetivo].TemSugestaoPendente);
        Assert.Equal(2, plano.Versao);
    }

    [Fact]
    public void Editar_depois_de_aceitar_e_so_escrever_por_cima()
    {
        var plano = Novo();
        plano.Sugerir(BlocoDoPlano.Objetivo, "fechar 5kg esta semana", T0);
        plano.Aceitar(BlocoDoPlano.Objetivo, T0);

        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg ate sexta", T0.AddMinutes(1));

        Assert.Equal("fechar 5kg ate sexta", plano[BlocoDoPlano.Objetivo].Texto);
    }

    [Fact]
    public void Aceitar_sem_sugestao_nao_faz_nada()
    {
        var plano = Novo();
        plano.Escrever(BlocoDoPlano.Objetivo, "o meu texto", T0);

        plano.Aceitar(BlocoDoPlano.Objetivo, T0.AddMinutes(1));

        Assert.Equal("o meu texto", plano[BlocoDoPlano.Objetivo].Texto);
        Assert.Equal(2, plano.Versao);
    }

    [Fact]
    public void Uma_sugestao_nova_substitui_a_anterior_pendente()
    {
        var plano = Novo();

        plano.Sugerir(BlocoDoPlano.Objetivo, "primeira", T0);
        plano.Sugerir(BlocoDoPlano.Objetivo, "segunda", T0.AddMinutes(1));

        Assert.Equal("segunda", plano[BlocoDoPlano.Objetivo].Sugestao);
    }

    // --- Bordas ---

    [Fact]
    public void Plano_sem_negocio_e_recusado()
    {
        Assert.Throws<ArgumentException>(
            () => new PlanoDeAbordagem(Guid.NewGuid(), Guid.Empty, T0));
    }

    [Fact]
    public void Apagar_um_bloco_e_escrever_vazio_e_conta_versao()
    {
        var plano = Novo();
        plano.Escrever(BlocoDoPlano.Objetivo, "fechar 5kg", T0);

        plano.Escrever(BlocoDoPlano.Objetivo, "", T0.AddMinutes(1));

        Assert.True(plano.EstaVazio);
        Assert.Equal(3, plano.Versao);
    }
}
