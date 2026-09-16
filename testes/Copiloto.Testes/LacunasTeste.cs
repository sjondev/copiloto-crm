using Copiloto.Dominio.Fichas;

namespace Copiloto.Testes;

/// <summary>
/// O que ainda nao sabemos sobre o cliente (#8).
///
/// E o quadrante mais util do dossie e o unico verificavel: "voce ainda nao sabe
/// quem decide a compra" e um fato sobre a ficha, nao uma opiniao sobre a venda.
/// Por isso roda sem modelo — e por isso da para testar assim.
/// </summary>
public class LacunasTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static FichaCliente Ficha() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    // --- O basico ---

    [Fact]
    public void Lead_sem_ficha_nenhuma_e_o_que_MAIS_tem_lacuna()
    {
        // Devolver lista vazia aqui diria ao vendedor que nao falta nada sobre
        // alguem de quem nao se sabe absolutamente nada.
        Assert.NotEmpty(Lacunas.De(null));
    }

    [Fact]
    public void A_lacuna_vem_como_PERGUNTA_e_nao_como_rotulo_de_campo()
    {
        var primeira = Lacunas.De(null)[0];

        Assert.Equal("Papel na decisão", primeira.Rotulo);
        Assert.Contains("?", primeira.Pergunta, StringComparison.Ordinal);
        Assert.NotEqual(primeira.Rotulo, primeira.Pergunta);
    }

    /// <summary>
    /// Quem decide vem primeiro porque e o unico que invalida todo o resto:
    /// descobrir orcamento e prazo com quem nao assina e trabalho que recomeca
    /// do zero quando o decisor aparece.
    /// </summary>
    [Fact]
    public void Quem_decide_vem_antes_de_orcamento()
    {
        var rotulos = Lacunas.De(null).Select(l => l.Rotulo).ToList();

        Assert.True(
            rotulos.IndexOf("Papel na decisão") < rotulos.IndexOf("Orçamento estimado"),
            "quem decide precisa vir antes de orcamento");
    }

    [Fact]
    public void A_lista_para_no_maximo_para_nao_virar_interrogatorio()
    {
        // O Storybook da #170 mostrou o efeito no estado `CheioDemais`: passado
        // certo ponto, as lacunas caem abaixo da dobra.
        Assert.Equal(Lacunas.Maximo, Lacunas.De(null).Count);
    }

    // --- O que ja se sabe sai da lista ---

    [Fact]
    public void Fato_anotado_fecha_a_lacuna()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Fato("decide sozinho", "ele disse")));

        Assert.DoesNotContain(Lacunas.De(ficha), l => l.Rotulo == "Papel na decisão");
    }

    [Fact]
    public void Fechar_uma_lacuna_promove_a_proxima_para_a_lista()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Fato("decide sozinho", "ele disse")));

        // A lista continua cheia: entrou uma que estava fora do corte.
        Assert.Equal(Lacunas.Maximo, Lacunas.De(ficha).Count);
    }

    // --- Impressao nao e' saber ---

    /// <summary>
    /// O elo com a #88: impressao nao ancora. Um slot preenchido so com palpite
    /// continua aberto para efeito de conselho — "acho que ele decide" nao
    /// sustenta uma fala ao cliente.
    /// </summary>
    [Fact]
    public void Slot_com_APENAS_impressao_continua_aberto()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Impressao("parece decidir sozinho")));

        var lacuna = Assert.Single(Lacunas.De(ficha), l => l.Rotulo == "Papel na decisão");

        Assert.True(lacuna.ApenasImpressao);
    }

    [Fact]
    public void A_pergunta_de_quem_ja_tem_palpite_CITA_o_palpite()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Impressao("parece decidir sozinho")));

        var lacuna = Assert.Single(Lacunas.De(ficha), l => l.Rotulo == "Papel na decisão");

        // Mesma razao da citacao no sinal: o vendedor discorda de algo concreto,
        // em vez de aceitar ou ignorar.
        Assert.Contains("parece decidir sozinho", lacuna.Pergunta, StringComparison.Ordinal);
        Assert.Contains("confirmar", lacuna.Pergunta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fato_ganha_da_impressao_no_mesmo_slot()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Impressao("parece decidir sozinho")));
        ficha.Atualizar(T0.AddDays(1), pessoa: new SobreAPessoa(
            PapelNaDecisao: Anotacao.Fato("decide sozinho", "ele confirmou")));

        Assert.DoesNotContain(Lacunas.De(ficha), l => l.Rotulo == "Papel na decisão");
    }

    // --- Nao repetir o que o agente ja perguntou ---

    /// <summary>
    /// Observado rodando, e nao previsto: o dossie mostrava "Quem decide a
    /// compra e ele mesmo?" (do agente, lendo a conversa) e "Ele decide a
    /// compra, ou precisa levar para alguem?" (da ficha), uma embaixo da outra.
    /// Duplicata no painel mais util do dossie e o que ensina o vendedor a
    /// parar de ler a lista.
    /// </summary>
    [Fact]
    public void Nao_repete_a_pergunta_que_o_agente_ja_fez_com_outras_palavras()
    {
        var jaDitas = new[] { "Quem decide a compra e ele mesmo?" };

        Assert.DoesNotContain(
            Lacunas.De(null, jaDitas),
            l => l.Rotulo is "Papel na decisão" or "Quem mais decide");
    }

    [Fact]
    public void Assunto_diferente_continua_entrando()
    {
        var jaDitas = new[] { "Quem decide a compra e ele mesmo?" };

        // Orcamento nao foi perguntado: a lista nao pode encolher por tabela.
        Assert.Contains(Lacunas.De(null, jaDitas), l => l.Rotulo == "Orçamento estimado");
    }

    [Fact]
    public void Sem_nada_ja_dito_o_comportamento_nao_muda()
    {
        Assert.Equal(
            Lacunas.De(null).Select(l => l.Rotulo),
            Lacunas.De(null, []).Select(l => l.Rotulo));
    }

    // --- Ficha cheia ---

    [Fact]
    public void Ficha_com_tudo_apurado_nao_tem_lacuna()
    {
        var ficha = Ficha();
        ficha.Atualizar(T0,
            empresa: new SobreAEmpresa(
                Anotacao.Fato("cafeteria"), Anotacao.Fato("pequena"),
                Anotacao.Fato("abrindo filial"), Anotacao.Fato("indicação")),
            pessoa: new SobreAPessoa(
                Anotacao.Fato("dona"), Anotacao.Fato("decide sozinha"),
                Anotacao.Fato("ninguém"), Anotacao.Fato("objetiva")),
            negocio: new SobreONegocio(
                Anotacao.Fato("consumo no salão"), Anotacao.Fato("outro fornecedor"),
                Anotacao.Fato("R$ 2 mil/mês"), Anotacao.Fato("preço do concorrente")));

        Assert.Empty(Lacunas.De(ficha));
    }
}
