using Copiloto.Dominio.Fichas;

namespace Copiloto.Testes;

/// <summary>
/// A Ficha do Cliente (#86), que resolve o cold start: sem conversa, o copiloto
/// nao servia para nada — justamente quando o vendedor mais precisa de ajuda.
/// </summary>
public class FichaClienteTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static FichaCliente Nova() => new(Guid.NewGuid(), Guid.NewGuid(), T0);

    [Fact]
    public void Ficha_vazia_nao_quebra_nada()
    {
        // Criterio de aceite, e a razao: o sistema tem de funcionar sem ela.
        var ficha = Nova();

        Assert.True(ficha.EstaVazia);
        Assert.Empty(ficha.Preenchidos);
        Assert.Equal("", CamadaC2.Montar(ficha));
        Assert.Equal(0, CamadaC2.TokensEstimados(CamadaC2.Montar(ficha)));
    }

    [Fact]
    public void Nenhum_campo_e_obrigatorio()
    {
        // Formulario com campo obrigatorio fica pela metade, e o que se ganha e
        // um dado FALSO no campo que alguem foi forcado a inventar para salvar.
        var ficha = Nova();

        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        Assert.Single(ficha.Preenchidos);
        Assert.Equal("cafeteria", ficha.Preenchidos["Ramo"].Valor);
    }

    [Fact]
    public void A_ficha_e_progressiva_e_nao_apaga_o_que_ja_sabia()
    {
        // Nasce com tres linhas e cresce. Exigir que alguem redigite o que ja
        // preencheu e o mesmo que garantir que nao vao preencher de novo.
        var ficha = Nova();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        ficha.Atualizar(T0.AddDays(1), pessoa: new SobreAPessoa(Cargo: Anotacao.Fato("sócio")));

        Assert.Equal("cafeteria", ficha.Preenchidos["Ramo"].Valor);
        Assert.Equal("sócio", ficha.Preenchidos["Cargo"].Valor);
    }

    [Fact]
    public void O_historico_guarda_o_que_a_ficha_dizia_antes()
    {
        // "Ele era o decisor e agora nao e" e informacao de VENDA, nao
        // auditoria: a mudanca em si diz algo.
        var ficha = Nova();
        ficha.Atualizar(T0, pessoa: new SobreAPessoa(PapelNaDecisao: Anotacao.Fato("decisor")));
        ficha.Atualizar(T0.AddDays(2), pessoa: new SobreAPessoa(PapelNaDecisao: Anotacao.Fato("influenciador")));

        Assert.Equal("influenciador", ficha.Preenchidos["Papel na decisão"].Valor);
        Assert.Equal(2, ficha.Historico.Count);
        Assert.Equal("decisor", ficha.Historico[0].Pessoa.PapelNaDecisao!.Valor);
    }

    [Fact]
    public void Atualizar_sem_nada_nao_cria_versao()
    {
        // Salvar sem mudar nada encheria o historico de ruido, e ai ninguem le.
        var ficha = Nova();
        ficha.Atualizar(T0);

        Assert.Empty(ficha.Historico);
    }

    [Fact]
    public void As_lacunas_sao_os_campos_que_faltam()
    {
        // O que fecha o ciclo: a IA aponta a lacuna, o vendedor descobre,
        // preenche, e a lacuna some.
        var ficha = Nova();
        Assert.Equal(12, ficha.Lacunas().Count);

        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria"), Porte: Anotacao.Fato("3 lojas")));

        Assert.Equal(10, ficha.Lacunas().Count);
        Assert.DoesNotContain("Ramo", ficha.Lacunas());
        Assert.Contains("Orçamento estimado", ficha.Lacunas());
    }

    [Fact]
    public void So_o_que_esta_preenchido_entra_no_contexto()
    {
        // "Porte: não informado" gasta token para dizer nada e, pior, o modelo
        // trata ausencia declarada como fato apurado — passa a raciocinar sobre
        // "uma empresa cujo porte e desconhecido" em vez de nao falar de porte.
        var ficha = Nova();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria")));

        var c2 = CamadaC2.Montar(ficha);

        Assert.Contains("Ramo: cafeteria", c2);
        Assert.DoesNotContain("Porte", c2);
        Assert.DoesNotContain("não informado", c2);
    }

    [Fact]
    public void Ficha_vazia_nao_vira_bloco_vazio_no_prompt()
    {
        // Bloco com titulo e nada dentro ocupa lugar e sugere que houve pesquisa
        // que nao houve.
        Assert.Equal("", CamadaC2.Montar(Nova()));
        Assert.Equal("", CamadaC2.Montar(null));
    }

    [Fact]
    public void A_contagem_de_tokens_cresce_com_a_ficha()
    {
        // A #52 mostra isso no playbook; aqui e o mesmo sinal, para o vendedor
        // perceber quando a ficha ficou grande demais.
        var ficha = Nova();
        ficha.Atualizar(T0, empresa: new SobreAEmpresa(Ramo: Anotacao.Fato("cafeteria de bairro")));
        var pequena = CamadaC2.TokensEstimados(CamadaC2.Montar(ficha));

        ficha.Atualizar(T0, negocio: new SobreONegocio(
            RiscoConhecido: Anotacao.Fato("contrato vigente com fornecedor atual ate dezembro, "
                          + "e o socio majoritario indicou esse fornecedor")));
        var maior = CamadaC2.TokensEstimados(CamadaC2.Montar(ficha));

        Assert.True(maior > pequena);
    }
}
