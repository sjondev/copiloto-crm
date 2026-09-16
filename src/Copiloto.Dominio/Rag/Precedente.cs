namespace Copiloto.Dominio.Rag;

/// <summary>
/// Um trecho de conversa passada, guardado para ser recuperado por semelhanca
/// (#60, #61).
///
/// O vetor mora como `float[]` porque o dominio nao tem pacote (#48): o tipo do
/// pgvector e a conversao ficam no mapeamento, que e borda. O dominio sabe que
/// existe um vetor e o que ele representa; nao sabe em que banco ele cabe.
/// </summary>
public class Precedente
{
    public Precedente(
        Guid id, Guid leadId, string trecho, float[] vetor, DateTimeOffset criadoEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Precedente sem id.", nameof(id));
        if (leadId == Guid.Empty)
            throw new ArgumentException(
                "Precedente sem titular nao tem como ser expurgado quando ele pedir "
                + "exclusao — e vetor que sobrevive ao Lead e dado pessoal vivo depois "
                + "do pedido (#46, #62).", nameof(leadId));

        if (string.IsNullOrWhiteSpace(trecho))
            throw new ArgumentException("Precedente sem texto.", nameof(trecho));

        if (vetor.Length != Embedding.Dimensoes)
            throw new ArgumentException(
                $"Vetor com {vetor.Length} dimensoes; a coluna tem {Embedding.Dimensoes}. "
                + "Dimensao errada nao e detalhe: a busca por similaridade compararia "
                + "coisas de espacos diferentes, e o resultado pareceria plausivel.",
                nameof(vetor));

        Id = id;
        LeadId = leadId;
        Trecho = trecho.Trim();
        Vetor = vetor;
        CriadoEm = criadoEm;
    }

    public Guid Id { get; }

    /// <summary>De quem e o texto. E por aqui que o expurgo alcanca o vetor.</summary>
    public Guid LeadId { get; }

    public string Trecho { get; }
    public float[] Vetor { get; }
    public DateTimeOffset CriadoEm { get; }
}

/// <summary>
/// Quem transforma texto em vetor.
///
/// Interface porque o provedor real custa dinheiro e rede, e a suite roda
/// offline por decisao — o fake vive ao lado, com vetor DETERMINISTICO a partir
/// do texto (#60).
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>O nome vai junto do vetor no ledger: comparar modelos exige saber qual gerou.</summary>
    string Modelo { get; }

    Task<float[]> Vetorizar(string texto, CancellationToken ct);
}

/// <summary>O que vale para qualquer provedor de embedding.</summary>
public static class Embedding
{
    /// <summary>
    /// A largura do vetor, fixada na coluna.
    ///
    /// 1536 e a dimensao dos modelos pequenos de embedding mais usados. O numero
    /// mora aqui, e nao no mapeamento, porque trocar de modelo por um de outra
    /// dimensao NAO e configuracao: e reindexar tudo. Deixar isso implicito
    /// produziria uma base com vetores de dois espacos misturados, onde a busca
    /// devolve vizinho errado com cara de certo.
    /// </summary>
    public const int Dimensoes = 1536;
}

/// <summary>
/// Um vizinho encontrado, com a distancia que o trouxe.
///
/// A distancia sobe junto de proposito: sem ela, quem consome nao tem como
/// dizer se recuperou um precedente parecido ou o menos ruim de uma base que
/// nao tinha nada a ver.
/// </summary>
public record PrecedenteSemelhante(Precedente Precedente, double Distancia);

/// <summary>
/// A busca por semelhanca, atras de interface — para trocar o backend se a
/// escala exigir, sem mexer em quem pergunta (#60).
/// </summary>
public interface IBuscaPorSimilaridade
{
    Task Guardar(Precedente precedente, CancellationToken ct);

    Task<IReadOnlyList<PrecedenteSemelhante>> MaisParecidos(
        float[] consulta, int quantos, CancellationToken ct);

    /// <summary>Apaga o que e do titular. E o expurgo em cascata do art. 18 (#46).</summary>
    Task<int> EsquecerTitular(Guid leadId, CancellationToken ct);
}
