namespace Copiloto.Dominio.Produtos;

/// <summary>A ficha de um produto, como a torrefacao a mantem.</summary>
public record FichaDeProduto(
    string Nome,
    string Variedade,
    string Origem,
    string Notas,
    string Torra,
    string Moagem,
    string Harmonizacao)
{
    /// <summary>A ficha como uma linha de contexto.</summary>
    public string ParaContexto() =>
        $"- {Nome} ({Variedade}, {Origem}): {Notas}. Torra {Torra}; moagem {Moagem}. "
        + $"Harmoniza com {Harmonizacao}.";
}

/// <summary>
/// O catalogo, e a conta que decide se ele precisa de RAG (#63).
///
/// A pergunta nao e "RAG e melhor?", e "o catalogo CABE no contexto?". Se cabe,
/// mandar inteiro e uma solucao exata; trocar por busca por similaridade seria
/// pagar latencia, custo de embedding e reindexacao para receber uma resposta
/// aproximada no lugar de uma certa.
///
/// Por isso a classe existe antes de qualquer decisao: ela devolve o numero.
/// </summary>
public class Catalogo
{
    /// <summary>
    /// O orcamento da camada C1 (ARQUITETURA secao 2), onde o catalogo entra
    /// junto do playbook. Nao e o orcamento do catalogo sozinho — o playbook
    /// tambem come dali, e por isso a conta usa uma folga.
    /// </summary>
    public const int OrcamentoC1EmTokens = 800;

    /// <summary>
    /// Quanto de C1 o catalogo pode ocupar. Os 40% restantes sao do playbook
    /// (tom, politica de desconto, o jeito da casa), que e a parte que a
    /// empresa escreve e nao pode ser espremida por causa do produto.
    /// </summary>
    public const double FatiaDoCatalogo = 0.6;

    private readonly IReadOnlyList<FichaDeProduto> _produtos;

    public Catalogo(IReadOnlyList<FichaDeProduto> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);
        _produtos = produtos;
    }

    public IReadOnlyList<FichaDeProduto> Produtos => _produtos;

    public string ParaContexto() =>
        string.Join("\n", _produtos.Select(p => p.ParaContexto()));

    /// <summary>
    /// Estimativa por ~4 caracteres/token, a mesma razao que a <c>CamadaC2</c>
    /// usa. E ESTIMATIVA: a conta exata depende do tokenizador de cada modelo,
    /// e chamar o provedor so para contar seria pagar para saber quanto se vai
    /// pagar. Serve para decidir ordem de grandeza, que e o que esta em jogo.
    /// </summary>
    public int TokensEstimados() => (int)Math.Ceiling(ParaContexto().Length / 4.0);

    public int TokensPorProduto() =>
        _produtos.Count == 0 ? 0 : (int)Math.Ceiling((double)TokensEstimados() / _produtos.Count);

    /// <summary>
    /// Quantos produtos ainda cabem mandando o catalogo inteiro. E o numero que
    /// a decisao da issue depende, e o que a torna revisavel: no dia em que o
    /// catalogo passar disso, "sem RAG" deixa de valer por medida, e nao por
    /// alguem ter achado.
    /// </summary>
    public int ProdutosQueCabem()
    {
        var porProduto = TokensPorProduto();
        if (porProduto == 0) return int.MaxValue;

        return (int)(OrcamentoC1EmTokens * FatiaDoCatalogo) / porProduto;
    }

    public bool CabeNoContexto() => _produtos.Count <= ProdutosQueCabem();
}
