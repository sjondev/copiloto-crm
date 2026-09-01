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

    /// <summary>
    /// Monta o texto. Ficha vazia devolve string vazia, e nao um bloco com
    /// titulo e nada dentro: bloco vazio ocupa lugar e sugere que houve
    /// pesquisa que nao houve.
    /// </summary>
    public static string Montar(FichaCliente? ficha)
    {
        if (ficha is null || ficha.EstaVazia) return "";

        var linhas = ficha.Preenchidos.Select(p => $"- {p.Key}: {p.Value}");
        return $"{Titulo}\n{string.Join("\n", linhas)}";
    }

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
