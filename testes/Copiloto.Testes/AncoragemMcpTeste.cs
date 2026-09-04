using Copiloto.Api.Ancoragem;
using Copiloto.Dominio.Ancoragem;
using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// A ancoragem como propriedade da arquitetura, e nao como instrucao no prompt
/// (#57).
///
/// O teste que interessa e o do estoque farto: nao e' "o agente foi orientado a
/// nao mentir", e' "nao existe caminho de codigo que produza a fala". Um teste
/// que so conferisse o caminho feliz aprovaria uma implementacao que aceita
/// qualquer numero que o modelo devolva.
/// </summary>
public class AncoragemMcpTeste
{
    /// <summary>
    /// Ferramenta que responde o que o teste mandar. `Nada` por padrao: o
    /// caminho sem dado e' o mais importante daqui, entao ele e' o default.
    /// </summary>
    private class FerramentaCombinada : IFerramentasDeAncoragem
    {
        public Achado Estoque { get; init; } = Achado.Nada;
        public Achado Preco { get; init; } = Achado.Nada;
        public Achado Politica { get; init; } = Achado.Nada;
        public Achado Semelhantes { get; init; } = Achado.Nada;
        public Achado Prazo { get; init; } = Achado.Nada;

        public int Chamadas { get; private set; }

        public Achado ConsultarEstoque(string produto) { Chamadas++; return Estoque; }
        public Achado PrecoVigente(string produto, int quantidade) { Chamadas++; return Preco; }
        public Achado PoliticaDesconto(string perfil) { Chamadas++; return Politica; }
        public Achado ClientesSemelhantesQueCompraram(string perfil) { Chamadas++; return Semelhantes; }
        public Achado PrazoEntrega(string cep, string produto) { Chamadas++; return Prazo; }
    }

    [Fact]
    public void Com_estoque_farto_o_agente_nao_consegue_produzir_escassez()
    {
        // O criterio de aceite da issue, e o unico que prova a tese: a
        // ferramenta RESPONDEU, o dado existe, e ele nao sustenta a fala.
        // Conferir so "veio resposta?" aprovaria "restam 200 unidades!".
        var montador = new MontadorAncorado(new FerramentaCombinada
        {
            Estoque = Achado.De("200"),
        });

        var bloco = montador.Escassez("Bourbon Amarelo");

        Assert.True(bloco.EhPergunta);
        Assert.Null(bloco.Ancora);
        Assert.Contains("200", bloco.Texto);
    }

    [Fact]
    public void Com_estoque_baixo_a_escassez_sai_ancorada_na_ferramenta()
    {
        var montador = new MontadorAncorado(new FerramentaCombinada
        {
            Estoque = Achado.De("4"),
        });

        var bloco = montador.Escassez("Geisha Microlote");

        Assert.False(bloco.EhPergunta);
        Assert.Equal("consultar_estoque(Geisha Microlote)=4", bloco.Ancora);
    }

    [Fact]
    public void Ferramenta_sem_resultado_vira_pergunta_ao_vendedor_nunca_afirmacao()
    {
        var montador = new MontadorAncorado(new FerramentaCombinada());

        var blocos = new[]
        {
            montador.Escassez("cafe qualquer"),
            montador.Preco("cafe qualquer", 3),
            montador.Desconto("perfil novo"),
            montador.ProvaSocial("perfil novo"),
            montador.Prazo("04567-000", "cafe qualquer"),
        };

        Assert.All(blocos, b => Assert.True(b.EhPergunta));
        Assert.All(blocos, b => Assert.Null(b.Ancora));
    }

    [Fact]
    public void Nao_ha_montador_sem_ferramenta()
    {
        // "Chama a ferramenta ANTES de afirmar" nao e' disciplina de quem
        // escreve: sem ferramenta o objeto nao chega a existir.
        Assert.Throws<ArgumentNullException>(() => new MontadorAncorado(null!));
    }

    [Fact]
    public void Toda_chamada_entra_no_ledger_com_latencia()
    {
        var montador = new MontadorAncorado(new FerramentaCombinada
        {
            Estoque = Achado.De("4"),
        });

        montador.Escassez("Geisha Microlote");
        montador.Desconto("cafeteria");

        Assert.Equal(2, montador.Chamadas.Count);
        Assert.All(montador.Chamadas, c => Assert.True(c.LatenciaMs >= 0));
        Assert.Equal("consultar_estoque", montador.Chamadas[0].Ferramenta);
        Assert.True(montador.Chamadas[0].Achou);
    }

    [Fact]
    public void A_chamada_que_nao_achou_tambem_entra_no_ledger()
    {
        // E' a mais util das duas: buraco de catalogo aparece como serie de
        // `Achou=false` numa ferramenta so, e sem o registro ele viraria "a IA
        // pergunta demais".
        var montador = new MontadorAncorado(new FerramentaCombinada());

        montador.ProvaSocial("perfil que nao existe");

        var chamada = Assert.Single(montador.Chamadas);
        Assert.False(chamada.Achou);
    }

    [Fact]
    public void Quantidade_nao_positiva_nao_chega_a_consultar_preco()
    {
        var ferramentas = new FerramentaCombinada();
        var montador = new MontadorAncorado(ferramentas);

        Assert.Throws<ArgumentOutOfRangeException>(() => montador.Preco("Bourbon Amarelo", 0));
        Assert.Equal(0, ferramentas.Chamadas);
    }

    // --- O catalogo fake, que e' o que roda na demo (#20) ---

    [Fact]
    public void Catalogo_de_cafe_ancora_o_preco_do_seed()
    {
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        var bloco = montador.Preco("Bourbon Amarelo", 3);

        Assert.False(bloco.EhPergunta);
        Assert.Contains("68", bloco.Texto);
    }

    [Fact]
    public void Faixa_de_desconto_sai_da_tabela_e_nao_da_conversa()
    {
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        var bloco = montador.Preco("Bourbon Amarelo", 5);

        Assert.Contains("8%", bloco.Texto);
        Assert.Contains("preco_vigente(Bourbon Amarelo,5)", bloco.Ancora);
    }

    [Fact]
    public void Produto_fora_do_catalogo_nao_tem_preco_afirmavel()
    {
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        Assert.True(montador.Preco("Cafe que nao vendemos", 2).EhPergunta);
    }

    [Fact]
    public void Estoque_farto_do_catalogo_real_tambem_barra_a_escassez()
    {
        // 140kg de Bourbon no catalogo: o caminho de escassez fica fechado para
        // o produto mais vendido, que e' exatamente onde a tentacao existe.
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        Assert.True(montador.Escassez("Bourbon Amarelo").EhPergunta);
        Assert.False(montador.Escassez("Geisha Microlote").EhPergunta);
    }

    [Fact]
    public void Prova_social_com_poucos_compradores_nao_sai_da_ferramenta()
    {
        // Tres compradores identificam gente. A ferramenta responde `Nada`, e o
        // agente nao tem o que agregar por conta propria.
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        Assert.True(montador.ProvaSocial("assinatura corporativa").EhPergunta);
        Assert.False(montador.ProvaSocial("cafeteria").EhPergunta);
    }

    [Fact]
    public void Prova_social_nunca_cita_cliente_nominal()
    {
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        var bloco = montador.ProvaSocial("cafeteria");

        Assert.Contains("23 clientes", bloco.Ancora);
        Assert.DoesNotContain("Marina", bloco.Texto);
    }

    [Fact]
    public void Prazo_de_produto_sem_estoque_nao_e_afirmado()
    {
        var esgotado = new FerramentasFake(
            catalogo: new[] { new ProdutoDoCatalogo("Geisha Microlote", 0, 190m) },
            politicaPorPerfil: new Dictionary<string, string>(),
            compradoresPorPerfil: new Dictionary<string, int>(),
            prazoPorRegiao: new Dictionary<string, string> { ["04"] = "2 dias uteis" });

        var montador = new MontadorAncorado(esgotado);

        Assert.True(montador.Prazo("04567-000", "Geisha Microlote").EhPergunta);
    }

    [Fact]
    public void Cep_incompleto_nao_vira_prazo_chutado()
    {
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        Assert.True(montador.Prazo("04567", "Bourbon Amarelo").EhPergunta);
        Assert.False(montador.Prazo("04567-000", "Bourbon Amarelo").EhPergunta);
    }

    [Fact]
    public void Perfil_sem_politica_nao_vira_desconto_zero()
    {
        // "Ninguem decidiu" nao e' "nao ha desconto": afirmar o segundo fecha
        // negocio que o gestor teria aberto.
        var montador = new MontadorAncorado(FerramentasFake.DoCenarioDeCafe());

        Assert.True(montador.Desconto("assinatura corporativa").EhPergunta);
        Assert.False(montador.Desconto("cafeteria").EhPergunta);
    }

    [Fact]
    public void Bloco_de_preco_exige_ancora_como_as_demais_taticas()
    {
        Assert.True(BlocoSugerido.PrecisaDeAncora(Tatica.Preco));
        Assert.Throws<ArgumentException>(
            () => BlocoSugerido.Ancorado(Tatica.Preco, "sai a R$ 40 o quilo", ""));
    }
}
