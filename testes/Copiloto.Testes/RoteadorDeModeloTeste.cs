using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Configuration;

namespace Copiloto.Testes;

/// <summary>
/// O router (#29). E a peca central: sem ele, "orquestracao multi-modelo" e so
/// uma palavra no README.
/// </summary>
public class RoteadorDeModeloTeste
{
    // Tabela de exemplo, com a forma que a configuracao real tera.
    private static readonly ModeloDisponivel Barato =
        new("mini", "provedor-a", 0.0005m, 300, [Tarefa.Triagem, Tarefa.Leitura]);

    private static readonly ModeloDisponivel Medio =
        new("medio", "provedor-b", 0.0080m, 900, [Tarefa.Leitura, Tarefa.Conselho]);

    private static readonly ModeloDisponivel Forte =
        new("forte", "provedor-a", 0.0400m, 2500, [Tarefa.Conselho]);

    private static RoteadorDeModelo Router(Func<string, bool>? circuito = null) =>
        new([Barato, Medio, Forte], circuito);

    [Fact]
    public void Triagem_nunca_escolhe_modelo_caro()
    {
        // Criterio de aceite. A triagem roda em TODA fala; escolher o forte ali
        // multiplica a conta do mes por um fator de oitenta.
        var d = Router().Escolher(Tarefa.Triagem);

        Assert.Equal("mini", d!.Modelo);
    }

    [Fact]
    public void Conselho_pode_gastar_porque_o_barato_nao_atende()
    {
        // O "nao escolher caro" nao pode virar "nunca escolher caro": o filtro e
        // a capacidade, e quem decide isso e a tabela.
        var d = Router().Escolher(Tarefa.Conselho);

        Assert.Equal("medio", d!.Modelo);
    }

    [Fact]
    public void Com_circuito_aberto_no_preferido_escolhe_o_proximo()
    {
        // Criterio de aceite. Provedor caido nao e escolhido, mesmo sendo o ideal.
        var d = Router(p => p == "provedor-a").Escolher(Tarefa.Leitura);

        Assert.Equal("medio", d!.Modelo);
        Assert.Contains(d.Descartados, x => x.Contains("circuito aberto"));
    }

    [Fact]
    public void A_decisao_diz_qual_modelo_e_por_que()
    {
        // Vai ao ledger. Sem o motivo, "usou o medio" nao permite auditar gasto
        // nem entender por que a conta subiu no mes passado.
        var d = Router().Escolher(Tarefa.Leitura);

        Assert.Equal("mini", d!.Modelo);
        Assert.Equal("provedor-a", d.Provedor);
        Assert.Contains("mais barato", d.Motivo);
        Assert.Contains(d.Descartados, x => x.StartsWith("medio", StringComparison.Ordinal));
    }

    [Fact]
    public void Quando_tudo_da_tarefa_esta_fora_a_resposta_e_null()
    {
        // Cair no "melhor disponivel" mandaria a triagem para o modelo caro
        // justamente quando o barato caiu — o pior momento para gastar.
        var d = Router(_ => true).Escolher(Tarefa.Triagem);

        Assert.Null(d);
    }

    [Fact]
    public void Tarefa_sem_modelo_na_tabela_devolve_null_e_nao_improvisa()
    {
        var so_triagem = new RoteadorDeModelo([Barato]);

        Assert.Null(so_triagem.Escolher(Tarefa.Conselho));
        Assert.NotNull(so_triagem.Escolher(Tarefa.Triagem));
    }

    [Fact]
    public void Empate_de_custo_e_desfeito_pela_latencia()
    {
        var a = new ModeloDisponivel("lento", "p1", 0.001m, 3000, [Tarefa.Leitura]);
        var b = new ModeloDisponivel("rapido", "p2", 0.001m, 200, [Tarefa.Leitura]);

        var d = new RoteadorDeModelo([a, b]).Escolher(Tarefa.Leitura);

        Assert.Equal("rapido", d!.Modelo);
    }

    [Fact]
    public void Tabela_vazia_nao_sobe()
    {
        // Adiar o erro para a primeira conversa real seria descobri-lo no pior
        // momento possivel.
        Assert.Throws<ArgumentException>(() => new RoteadorDeModelo([]));
    }

    [Fact]
    public void O_circuito_e_por_PROVEDOR_e_nao_por_modelo()
    {
        // Quando um provedor cai, caem TODOS os modelos dele. Tratar por modelo
        // faria o router tentar o segundo modelo do mesmo provedor caido, e
        // gastar mais um timeout para descobrir o que ja sabia.
        var d = Router(p => p == "provedor-a").Escolher(Tarefa.Conselho);

        Assert.Equal("medio", d!.Modelo);
        Assert.Contains(d.Descartados, x => x.StartsWith("forte", StringComparison.Ordinal) && x.Contains("circuito", StringComparison.Ordinal));
    }
}

/// <summary>
/// A tabela vem de CONFIGURACAO, e nao de codigo (#29) — e este arquivo prova
/// que o appsettings de verdade fecha.
/// </summary>
public class TabelaDeModelosTeste
{
    private static IConfiguration Config(string json) =>
        new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

    [Fact]
    public void O_appsettings_do_projeto_carrega_e_o_router_sobe_com_ele()
    {
        // Sobre o arquivo REAL, e nao sobre um JSON escrito no teste: JSON
        // escrito para passar passa sempre, e o que quebra em producao e o
        // arquivo que esta no repositorio.
        var raiz = typeof(TabelaDeModelosTeste).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;
        var caminho = Path.Combine(raiz, "src", "Copiloto.Api", "appsettings.json");

        var config = new ConfigurationBuilder().AddJsonFile(caminho).Build();
        var tabela = TabelaDeModelos.Carregar(config);

        Assert.NotEmpty(tabela);
        var router = new RoteadorDeModelo(tabela);
        Assert.NotNull(router.Escolher(Tarefa.Triagem));
        Assert.NotNull(router.Escolher(Tarefa.Conselho));
    }

    [Fact]
    public void O_padrao_do_projeto_e_o_provedor_fake_e_custo_zero()
    {
        // Ninguem sobe o projeto pela primeira vez gastando dinheiro sem ter
        // pedido — e a suite roda offline por decisao do CLAUDE.md.
        var raiz = typeof(TabelaDeModelosTeste).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(raiz, "src", "Copiloto.Api", "appsettings.json")).Build();

        var tabela = TabelaDeModelos.Carregar(config);

        Assert.All(tabela, m => Assert.Equal("fake", m.Provedor));
        Assert.All(tabela, m => Assert.Equal(0m, m.CustoPorMilTokens));
    }

    [Fact]
    public void Trocar_de_modelo_e_editar_JSON_e_nao_recompilar()
    {
        // O ponto da issue: a tabela em configuracao. Se isto exigisse deploy,
        // o efeito pratico seria ninguem trocar nunca.
        var config = Config("""
        {
          "Modelos": [
            { "Nome": "novo-barato", "Provedor": "outro", "CustoPorMilTokens": 0.0001,
              "LatenciaTipicaMs": 50, "Atende": [ "Triagem" ] }
          ]
        }
        """);

        var d = new RoteadorDeModelo(TabelaDeModelos.Carregar(config)).Escolher(Tarefa.Triagem);

        Assert.Equal("novo-barato", d!.Modelo);
    }

    [Fact]
    public void Secao_ausente_falha_dizendo_o_que_e()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => TabelaDeModelos.Carregar(Config("{}")));

        Assert.Contains("Modelos", erro.Message);
    }
}
