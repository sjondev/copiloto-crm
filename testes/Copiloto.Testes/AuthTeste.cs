using Copiloto.Api.Auth;
using Copiloto.Dominio.Acesso;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// Auth com dois perfis (#49).
///
/// E o minimo para o painel por vendedor fazer sentido — e para o projeto nao
/// ter uma falha obvia numa vitrine publica.
/// </summary>
public class AuthTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
    private const string Segredo = "segredo-de-teste-com-tamanho-suficiente-aqui";

    private static Usuario Vendedor(Guid? id = null) => new(
        id ?? Guid.NewGuid(), "Marina", "marina@torrefacao.com.br",
        Senhas.Hash("senha-boa-de-verdade"), PerfilDeAcesso.Vendedor);

    private static Usuario Gestor() => new(
        Guid.NewGuid(), "Lucas", "lucas@torrefacao.com.br",
        Senhas.Hash("outra-senha-boa"), PerfilDeAcesso.Gestor);

    // --- Senha ---

    [Fact]
    public void A_senha_vira_hash_BCrypt_e_nunca_e_guardada()
    {
        var hash = Senhas.Hash("senha-boa-de-verdade");

        Assert.StartsWith("$2", hash);                      // prefixo do BCrypt
        Assert.DoesNotContain("senha-boa-de-verdade", hash);
        Assert.True(hash.Length >= Usuario.TamanhoMinimoDoHash);
    }

    [Fact]
    public void O_mesmo_texto_gera_hashes_diferentes()
    {
        // Sal por hash: sem isso, senhas iguais aparecem iguais no banco, e um
        // vazamento entrega de graca quem usa "123456".
        Assert.NotEqual(Senhas.Hash("mesma-senha"), Senhas.Hash("mesma-senha"));
    }

    [Fact]
    public void A_senha_certa_confere_e_a_errada_nao()
    {
        var hash = Senhas.Hash("senha-boa-de-verdade");

        Assert.True(Senhas.Confere("senha-boa-de-verdade", hash));
        Assert.False(Senhas.Confere("senha-boa-de-verdad", hash));
        Assert.False(Senhas.Confere("", hash));
    }

    [Fact]
    public void Hash_corrompido_devolve_false_em_vez_de_explodir()
    {
        // Excecao aqui viraria erro 500 no login — e 500 conta ao atacante que
        // aquele usuario tem algo diferente dos outros.
        Assert.False(Senhas.Confere("qualquer", "isto-nao-e-um-hash"));
    }

    [Fact]
    public void O_dominio_recusa_hash_com_cara_de_MD5_ou_SHA1()
    {
        // MD5 tem 32 caracteres, SHA-1 tem 40. Trocar o algoritmo e uma linha,
        // e o efeito de trocar para o errado so aparece no vazamento.
        var md5 = new string('a', 32);
        var sha1 = new string('b', 40);

        foreach (var fraco in new[] { md5, sha1 })
        {
            var erro = Assert.Throws<ArgumentException>(() => new Usuario(
                Guid.NewGuid(), "Marina", "m@x.com", fraco, PerfilDeAcesso.Vendedor));

            Assert.Contains("MD5", erro.Message);
        }
    }

    // --- Token ---

    [Fact]
    public void O_token_carrega_quem_e_e_o_perfil()
    {
        var tokens = new Tokens(Segredo);
        var gestor = Gestor();

        var lido = tokens.Ler(tokens.Emitir(gestor, T0));

        Assert.NotNull(lido);
        Assert.Equal(gestor.Id, lido!.Value.UsuarioId);
        Assert.Equal(PerfilDeAcesso.Gestor, lido.Value.Perfil);
    }

    [Fact]
    public void O_token_nao_leva_email_nem_hash()
    {
        // Ele fica no navegador e viaja em header: carrega o minimo que o
        // servidor precisa para decidir.
        var tokens = new Tokens(Segredo);
        var usuario = Vendedor();

        var token = tokens.Emitir(usuario, T0);

        Assert.DoesNotContain(usuario.Email, token);
        Assert.DoesNotContain(usuario.SenhaHash, token);
    }

    [Fact]
    public void Token_expirado_nao_vale()
    {
        var tokens = new Tokens(Segredo);

        // Emitido ontem, com validade de oito horas.
        var token = tokens.Emitir(Vendedor(), DateTimeOffset.UtcNow.AddDays(-1));

        Assert.Null(tokens.Ler(token));
    }

    [Fact]
    public void Token_assinado_com_outro_segredo_nao_vale()
    {
        // O caso que decide se a assinatura serve para alguma coisa.
        var deles = new Tokens("outro-segredo-completamente-diferente-aqui");
        var nosso = new Tokens(Segredo);

        var forjado = deles.Emitir(Gestor(), DateTimeOffset.UtcNow);

        Assert.Null(nosso.Ler(forjado));
    }

    [Fact]
    public void Texto_qualquer_no_lugar_do_token_nao_derruba_a_leitura()
    {
        Assert.Null(new Tokens(Segredo).Ler("nao-e-um-token"));
    }

    [Fact]
    public void Segredo_fraco_ou_ausente_derruba_a_subida()
    {
        // Cair para um segredo embutido seria pior que nao ter auth: daria a
        // impressao de proteger enquanto qualquer um forja um token de gestor.
        Assert.Throws<ArgumentException>(() => new Tokens(""));
        Assert.Throws<ArgumentException>(() => new Tokens("curto-demais"));
    }

    [Fact]
    public void A_validade_do_token_e_um_turno_e_nao_um_ano()
    {
        Assert.True(Tokens.Validade <= TimeSpan.FromHours(12));
        Assert.True(Tokens.Validade >= TimeSpan.FromHours(1));
    }

    [Fact]
    public void Nao_ha_tolerancia_silenciosa_de_relogio()
    {
        // O padrao da biblioteca sao cinco minutos: "expira em oito horas" com
        // bonus que ninguem escreveu e regra que ninguem sabe que existe.
        Assert.Equal(TimeSpan.Zero, new Tokens(Segredo).Validacao().ClockSkew);
    }

    // --- Escopo ---

    [Fact]
    public void Vendedor_nao_ve_lead_de_outro_vendedor()
    {
        // O criterio de aceite que existe para o projeto nao ter falha obvia.
        var marina = Vendedor();
        var doOutro = new Lead(Guid.NewGuid(), "+5511988887777", T0);
        doOutro.Assumir(Guid.NewGuid());

        Assert.False(EscopoDeLeitura.PodeVer(marina, doOutro));
    }

    [Fact]
    public void Vendedor_ve_o_proprio_lead()
    {
        var marina = Vendedor();
        var dela = new Lead(Guid.NewGuid(), "+5511988887777", T0);
        dela.Assumir(marina.Id);

        Assert.True(EscopoDeLeitura.PodeVer(marina, dela));
    }

    [Fact]
    public void Lead_sem_dono_e_da_equipe()
    {
        // Decisao de produto: o lead chega pelo WhatsApp sem atribuicao, e
        // esconde-lo ate alguem assumir faria a primeira mensagem de um cliente
        // novo nao aparecer para ninguem.
        var semDono = new Lead(Guid.NewGuid(), "+5511988887777", T0);

        Assert.True(EscopoDeLeitura.PodeVer(Vendedor(), semDono));
    }

    [Fact]
    public void Gestor_ve_tudo()
    {
        var doVendedor = new Lead(Guid.NewGuid(), "+5511988887777", T0);
        doVendedor.Assumir(Guid.NewGuid());

        Assert.True(EscopoDeLeitura.PodeVer(Gestor(), doVendedor));
    }

    [Fact]
    public void O_filtro_da_consulta_concorda_com_a_regra()
    {
        // Duas formas da mesma regra divergindo e o jeito classico de vazar:
        // a tela filtra certo e o relatorio, escrito depois, nao.
        var marina = Vendedor();
        var dela = new Lead(Guid.NewGuid(), "+5511900000001", T0);
        dela.Assumir(marina.Id);
        var doOutro = new Lead(Guid.NewGuid(), "+5511900000002", T0);
        doOutro.Assumir(Guid.NewGuid());
        var semDono = new Lead(Guid.NewGuid(), "+5511900000003", T0);

        var todos = new[] { dela, doOutro, semDono }.AsQueryable();

        var visiveis = EscopoDeLeitura.Visiveis(todos, marina).ToList();

        Assert.Equal(2, visiveis.Count);
        Assert.DoesNotContain(doOutro, visiveis);
        Assert.All(visiveis, l => Assert.True(EscopoDeLeitura.PodeVer(marina, l)));
        Assert.Equal(3, EscopoDeLeitura.Visiveis(todos, Gestor()).Count());
    }

    [Fact]
    public void Assumir_lead_de_outro_e_recusado_com_motivo()
    {
        // Dois vendedores no mesmo cliente sem saber um do outro e pior que
        // ninguem atender: o cliente recebe duas propostas diferentes.
        var lead = new Lead(Guid.NewGuid(), "+5511988887777", T0);
        lead.Assumir(Guid.NewGuid());

        var recusa = lead.Assumir(Guid.NewGuid());

        Assert.NotNull(recusa);
        Assert.Contains("liberar", recusa);
    }

    [Fact]
    public void Assumir_o_que_ja_e_seu_nao_e_erro()
    {
        var vendedor = Guid.NewGuid();
        var lead = new Lead(Guid.NewGuid(), "+5511988887777", T0);
        lead.Assumir(vendedor);

        Assert.Null(lead.Assumir(vendedor));
    }
}
