namespace Copiloto.Api.Ia;

/// <summary>
/// Escolhe o provedor pela configuracao (#27).
///
/// Gemea de <see cref="Ingestao.FonteDeConversa"/>, e de proposito: entrada e
/// saida do sistema seguem a mesma regra, e quem aprendeu uma sabe a outra.
/// </summary>
public static class ProvedorDeModelo
{
    public const string Chave = "MODEL_PROVIDER";
    public const string ChaveDaPasta = "SEED_RESPOSTAS";

    /// <summary>
    /// O provedor que `MODEL_PROVIDER` pediu.
    ///
    /// Ausente vira <c>fake</c>: ninguem sobe o projeto pela primeira vez
    /// gastando dinheiro sem ter pedido, e a suite roda offline e de graca.
    ///
    /// Nome que nao existe DERRUBA a subida. Cair no fake seria pior aqui do
    /// que na ingestao: a API responderia com conselho gravado em arquivo como
    /// se fosse leitura de verdade, e o vendedor levaria para o cliente uma
    /// analise que nao olhou a conversa dele.
    /// </summary>
    public static IModelProvider Escolher(IConfiguration configuracao, string raizDoConteudo)
    {
        // A pasta e configuravel, com o padrao apontando para o seed do
        // repositorio: em container o conteudo vai para outro lugar, e um
        // caminho fixo em codigo faria a imagem subir sem resposta nenhuma.
        var pasta = configuracao[ChaveDaPasta]
                    ?? Path.Combine(raizDoConteudo, "..", "..", "seed", "respostas");

        var pedido = configuracao[Chave];
        if (string.IsNullOrWhiteSpace(pedido)) pedido = FakeProvider.NomeDoProvedor;

        return pedido.Trim().ToLowerInvariant() switch
        {
            FakeProvider.NomeDoProvedor => FakeProvider.DaPasta(pasta),
            _ => throw new ArgumentException(
                $"{Chave}='{pedido}' nao existe. Provedores: fake. "
                + "Provedor real ainda nao foi plugado — a subida para aqui de "
                + "proposito, porque cair no fake devolveria conselho gravado "
                + "em arquivo com cara de leitura da conversa do cliente.",
                nameof(configuracao)),
        };
    }
}
