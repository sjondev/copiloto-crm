using System.Reflection;
using Copiloto.Api.Ingestao;
using Copiloto.Dominio.Produtos;

namespace Copiloto.Testes;

/// <summary>
/// A medicao que decide se o catalogo merece RAG (#63).
///
/// A issue podia legitimamente terminar em "nao vamos fazer", e terminou. O que
/// a torna honesta e o numero: adicionar RAG a um catalogo que cabe no contexto
/// e trocar uma solucao exata por uma aproximada, pagando latencia, custo de
/// embedding e reindexacao pela troca.
/// </summary>
public class CatalogoTeste
{
    private static Catalogo DoSeed() => CatalogoGravado.DoArquivo(
        Path.Combine(RaizDoRepositorio(), "seed", "catalogo.json"));

    [Fact]
    public void O_catalogo_do_seed_cabe_no_contexto()
    {
        // O gate da decisao. No dia em que o catalogo passar do que cabe, ESTE
        // teste falha — e ai "sem RAG" deixa de valer por medida, nao porque
        // alguem lembrou de reabrir a issue.
        var catalogo = DoSeed();

        Assert.True(catalogo.CabeNoContexto(),
            $"O catalogo tem {catalogo.Produtos.Count} produtos e "
            + $"{catalogo.TokensEstimados()} tokens estimados, e o teto e "
            + $"{catalogo.ProdutosQueCabem()} produtos. Passou do ponto em que "
            + "mandar inteiro ainda era a solucao exata: reabra a #63 com estes "
            + "numeros antes de escolher RAG.");
    }

    [Fact]
    public void A_medicao_que_sustenta_a_decisao_esta_aqui()
    {
        // Os numeros da #63, presos ao build. Se a ficha de produto mudar de
        // formato, este teste falha e a decisao volta para a mesa com o numero
        // novo — que e' o unico jeito de "medimos e decidimos" nao virar
        // folclore seis meses depois.
        var catalogo = DoSeed();

        Assert.Equal(3, catalogo.Produtos.Count);
        Assert.Equal(179, catalogo.TokensEstimados());
        Assert.Equal(60, catalogo.TokensPorProduto());
        Assert.Equal(8, catalogo.ProdutosQueCabem());
    }

    [Fact]
    public void A_medicao_devolve_numero_por_produto()
    {
        // E o numero que extrapola: a decisao nao vale so para os tres produtos
        // do seed, vale para "quantos produtos esta empresa pode ter".
        var catalogo = DoSeed();

        Assert.InRange(catalogo.TokensPorProduto(), 20, 200);
        Assert.True(catalogo.ProdutosQueCabem() >= catalogo.Produtos.Count);
    }

    [Fact]
    public void Catalogo_grande_nao_cabe_e_a_conta_diz_isso()
    {
        // A conta precisa saber dizer NAO, senao ela e decoracao.
        var muitos = Enumerable.Range(0, 200).Select(i => new FichaDeProduto(
            $"Cafe {i}", "Bourbon", "Sul de Minas, 1.000m",
            "chocolate e castanha", "media", "media", "pao de queijo")).ToList();

        Assert.False(new Catalogo(muitos).CabeNoContexto());
    }

    [Fact]
    public void Catalogo_vazio_nao_quebra_a_conta()
    {
        var vazio = new Catalogo([]);

        Assert.Equal(0, vazio.TokensEstimados());
        Assert.True(vazio.CabeNoContexto());
    }

    [Fact]
    public void A_ficha_leva_ao_contexto_o_que_o_vendedor_usa_para_vender()
    {
        // Nome sozinho nao responde "qual voces indicam para espresso doce?",
        // que e a pergunta real do balcao.
        var linha = DoSeed().Produtos[0].ParaContexto();

        Assert.Contains("caramelo", linha);
        Assert.Contains("moagem", linha);
        Assert.Contains("Harmoniza", linha);
    }

    private static string RaizDoRepositorio()
    {
        var raiz = typeof(CatalogoTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RaizDoRepositorio")
            ?.Value;

        Assert.False(string.IsNullOrWhiteSpace(raiz), "Metadado RaizDoRepositorio ausente.");
        return raiz!;
    }
}
