using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// A cascata de fallback (#30).
///
/// A regra de produto que manda aqui nao e de engenharia: o vendedor esta no
/// meio de uma venda, com o cliente digitando do outro lado. Erro na tela
/// naquele momento e pior que informacao levemente desatualizada.
/// </summary>
public class CascataDeModelosTeste
{
    private static readonly ModeloDisponivel Barato =
        new("fake-mini", "provedor-a", 0.5m, 200, [Tarefa.Triagem, Tarefa.Leitura]);

    private static readonly ModeloDisponivel Medio =
        new("fake-medio", "provedor-b", 2.0m, 400, [Tarefa.Leitura]);

    private static readonly ModeloDisponivel Forte =
        new("fake-forte", "provedor-c", 9.0m, 900, [Tarefa.Leitura, Tarefa.Conselho]);

    private static FakeProvider Provedor()
    {
        var raiz = typeof(CascataDeModelosTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        return FakeProvider.DaPasta(Path.Combine(raiz, "seed", "respostas"));
    }

    private static CascataDeModelos Cascata(
        IModelProvider provedor, Func<string, bool>? circuitoAberto = null) =>
        new(new RoteadorDeModelo([Barato, Medio, Forte], circuitoAberto),
            provedor,
            NullLogger<CascataDeModelos>.Instance);

    private static Task<ResultadoDaCascata> Pedir(CascataDeModelos cascata) =>
        cascata.Pedir(Tarefa.Leitura, "leia esta conversa", CancellationToken.None);

    [Fact]
    public async Task Primeiro_degrau_respondendo_nao_gasta_os_outros()
    {
        var resultado = await Pedir(Cascata(Provedor()));

        Assert.False(resultado.Degradou);
        Assert.Equal("fake-mini", resultado.Modelo);
        Assert.Empty(resultado.Falhas);
    }

    [Fact]
    public async Task Degrau_caido_desce_para_o_proximo_mais_barato()
    {
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var resultado = await Pedir(Cascata(provedor));

        Assert.False(resultado.Degradou);
        Assert.Equal("fake-medio", resultado.Modelo);

        var caiu = Assert.Single(resultado.Falhas);
        Assert.Equal("fake-mini", caiu.Modelo);
        Assert.Equal("fora do ar", caiu.Motivo);
    }

    [Fact]
    public async Task Limite_de_taxa_desce_um_degrau_e_o_motivo_diz_a_espera()
    {
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.LimiteExcedido);

        var resultado = await Pedir(Cascata(provedor));

        Assert.Equal("fake-medio", resultado.Modelo);
        Assert.Contains("limite de taxa, tentar em 2s", resultado.Falhas.Single().Motivo);
    }

    [Fact]
    public async Task Timeout_desce_um_degrau()
    {
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.Timeout);

        var resultado = await Pedir(Cascata(provedor));

        Assert.Equal("fake-medio", resultado.Modelo);
        Assert.Equal("nao respondeu a tempo", resultado.Falhas.Single().Motivo);
    }

    [Fact]
    public async Task A_cascata_inteira_se_esgota_sem_erro_na_tela()
    {
        // O criterio central da issue. Degradar e desfecho previsto, nao
        // excecao: quem chama mantem o ultimo estado valido sem try/catch.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr, vezes: 3);

        var resultado = await Pedir(Cascata(provedor));

        Assert.True(resultado.Degradou);
        Assert.Null(resultado.Resposta);
        Assert.Null(resultado.Modelo);
    }

    [Fact]
    public async Task Cada_degrau_tentado_fica_registrado_na_ordem()
    {
        // E o que vai ao ledger (#1): sem os degraus, o custo de uma tarefa que
        // desceu a cascata inteira aparece como se fosse de uma chamada so.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.Timeout);
        provedor.CombinarFalha(FalhaSimulada.LimiteExcedido);
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var resultado = await Pedir(Cascata(provedor));

        Assert.True(resultado.Degradou);
        Assert.Equal(
            ["fake-mini", "fake-medio", "fake-forte"],
            resultado.Falhas.Select(f => f.Modelo));
        Assert.Equal(
            ["provedor-a", "provedor-b", "provedor-c"],
            resultado.Falhas.Select(f => f.Provedor));
    }

    [Fact]
    public async Task Circuito_aberto_tira_o_provedor_da_cascata_inteira()
    {
        // Tentar quem se sabe caido gasta o tempo do vendedor para chegar ao
        // mesmo lugar.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var resultado = await Pedir(Cascata(provedor, circuitoAberto: p => p == "provedor-b"));

        Assert.Equal("fake-forte", resultado.Modelo);
        Assert.DoesNotContain(resultado.Falhas, f => f.Provedor == "provedor-b");
    }

    [Fact]
    public async Task Tarefa_sem_modelo_na_tabela_degrada_em_vez_de_estourar()
    {
        var soLeitura = new CascataDeModelos(
            new RoteadorDeModelo([Medio]), Provedor(), NullLogger<CascataDeModelos>.Instance);

        var resultado = await soLeitura.Pedir(Tarefa.Conselho, "monte o plano", CancellationToken.None);

        Assert.True(resultado.Degradou);
        Assert.Empty(resultado.Falhas);
    }

    [Fact]
    public async Task A_cascata_de_cada_tarefa_sai_da_tabela_e_nao_do_codigo()
    {
        // "Cascata configuravel por tipo de tarefa": quem atende Conselho e
        // decidido pelo appsettings, nao por uma lista aqui dentro.
        var provedor = Provedor();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var resultado = await Cascata(provedor)
            .Pedir(Tarefa.Conselho, "monte o plano", CancellationToken.None);

        // So o forte atende Conselho, entao o unico degrau falhou e nao ha para onde ir.
        Assert.True(resultado.Degradou);
        Assert.Equal("fake-forte", resultado.Falhas.Single().Modelo);
    }

    [Fact]
    public async Task Bug_nosso_estoura_em_vez_de_degradar_em_silencio()
    {
        // A lista de falhas tratadas e fechada de proposito. Um catch(Exception)
        // engoliria defeito nosso como se fosse provedor caido, e a cascata
        // desceria os degraus todos repetindo o mesmo bug ate degradar — que e
        // a forma mais cara possivel de esconder um NullReference.
        var semRespostaGravada = new FakeProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pedir(Cascata(semRespostaGravada)));
    }
}
