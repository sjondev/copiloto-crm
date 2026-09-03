namespace Copiloto.Dominio.Fichas;

/// <summary>
/// A ficha virando texto para o contexto do modelo — a camada C2, ao lado do
/// playbook (C1) e da conversa (C3).
///
/// So o que esta PREENCHIDO entra. Mandar "Porte: não informado" gasta token
/// para dizer nada e, pior, o modelo trata ausencia declarada como fato
/// apurado — passa a raciocinar sobre "uma empresa cujo porte e desconhecido"
/// em vez de simplesmente nao falar de porte.
/// </summary>
public static class CamadaC2
{
    /// <summary>O cabecalho que separa esta camada das outras no prompt.</summary>
    public const string Titulo = "O QUE O VENDEDOR JA SABIA ANTES DA CONVERSA";

    public const string TituloDosFatos = "FATOS APURADOS (podem sustentar afirmacao)";

    public const string TituloDasImpressoes =
        "IMPRESSOES DO VENDEDOR (podem sustentar no maximo uma hipotese, nunca uma afirmacao)";

    /// <summary>
    /// A instrucao vai NO BLOCO, e nao no prompt de sistema, porque e' aqui que
    /// ela e' verificavel: o texto que carrega a impressao carrega junto o que
    /// pode ser feito com ela, e as duas coisas nao se separam no caminho.
    /// </summary>
    public const string Instrucao =
        "Impressao nao ancora escassez, prazo, preco nem desconto. Ao usar uma "
        + "impressao, diga que e' hipotese e de onde ela saiu.";

    /// <summary>
    /// Monta o texto. Ficha vazia devolve string vazia, e nao um bloco com
    /// titulo e nada dentro: bloco vazio ocupa lugar e sugere que houve
    /// pesquisa que nao houve.
    ///
    /// Fatos e impressoes vao em secoes SEPARADAS (#88). Numa lista unica,
    /// "Cargo: gerente de compras" e "Estilo: parece desconfiado" chegam ao
    /// modelo com o mesmo peso, e o palpite do vendedor volta para ele
    /// reembalado como conclusao do sistema — o que confirma o vies em vez de
    /// corrigi-lo.
    /// </summary>
    public static string Montar(FichaCliente? ficha)
    {
        if (ficha is null || ficha.EstaVazia) return "";

        var partes = new List<string> { Titulo };

        if (ficha.Fatos.Count > 0)
            partes.Add($"{TituloDosFatos}\n{Listar(ficha.Fatos)}");

        if (ficha.Impressoes.Count > 0)
            partes.Add($"{TituloDasImpressoes}\n{Listar(ficha.Impressoes)}\n{Instrucao}");

        return string.Join("\n", partes);
    }

    private static string Listar(IReadOnlyDictionary<string, Anotacao> anotacoes) =>
        string.Join("\n", anotacoes.Select(a => $"- {a.Key}: {a.Value.Rotulado()}"));

    /// <summary>
    /// Estimativa de tokens do bloco, para a tela mostrar o que a #52 mostra no
    /// playbook.
    ///
    /// E ESTIMATIVA, e o nome diz: a conta real depende do tokenizador de cada
    /// modelo, e chamar o provedor so para contar seria pagar para saber quanto
    /// se vai pagar. A razao de ~4 caracteres por token vale para portugues em
    /// tokenizadores BPE; o numero serve para o vendedor perceber que a ficha
    /// cresceu demais, nao para faturar.
    /// </summary>
    public static int TokensEstimados(string texto) =>
        string.IsNullOrEmpty(texto) ? 0 : (int)Math.Ceiling(texto.Length / 4.0);
}
