using System.Text.Json;
using System.Text.Json.Serialization;
using Copiloto.Dominio.Produtos;

namespace Copiloto.Api.Ingestao;

/// <summary>
/// O catalogo do `seed/catalogo.json`, no formato em que ele esta no arquivo.
///
/// Mora ao lado do `ConversaGravada` pelo mesmo motivo: o dominio nao le
/// arquivo nem conhece JSON, e o seed e material de demonstracao — nao e
/// cadastro de produto, que seria tela e banco (fora de escopo hoje).
/// </summary>
public record CatalogoGravado(IReadOnlyList<FichaDeProduto> Produtos)
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static Catalogo Ler(string json)
    {
        var gravado = JsonSerializer.Deserialize<CatalogoGravado>(json, Opcoes)
            ?? throw new InvalidOperationException("catalogo vazio");

        return new Catalogo(gravado.Produtos);
    }

    public static Catalogo DoArquivo(string caminho)
    {
        if (!File.Exists(caminho))
            throw new FileNotFoundException(
                $"Catalogo nao encontrado em {caminho}. Ele e o que a medicao da #63 "
                + "usa para decidir se o contexto aguenta o catalogo inteiro.", caminho);

        return Ler(File.ReadAllText(caminho));
    }
}
