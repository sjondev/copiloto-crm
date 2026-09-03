using System.Text.Json;
using System.Text.Json.Serialization;
using Copiloto.Dominio.Fichas;

namespace Copiloto.Api.Persistencia.Mapeamentos;

/// <summary>
/// Le e escreve <see cref="Anotacao"/> no JSON da ficha.
///
/// Existe porque `Anotacao` so nasce por `Fato(...)` ou `Impressao(...)`, e o
/// construtor privado e' o que garante isso — o `System.Text.Json` nao alcanca
/// construtor privado e reclama na desserializacao.
///
/// A saida barata seria abrir um construtor publico, ou marcar o privado com
/// `[JsonConstructor]`. As duas fazem o dominio carregar uma decisao de
/// serializacao, e a primeira ainda abriria o caminho que a #88 fechou: criar
/// anotacao sem dizer se e fato ou impressao. O conversor paga o preco aqui, no
/// mapeamento, que e' o lugar certo para pagar.
/// </summary>
public class AnotacaoJson : JsonConverter<Anotacao>
{
    /// <summary>A anotacao como ela esta no banco, antes de virar dominio.</summary>
    private record Bruta(
        string Valor,
        NaturezaDaInformacao Natureza,
        string? Fonte = null,
        DateTimeOffset? Quando = null);

    public override Anotacao? Read(
        ref Utf8JsonReader reader, Type tipo, JsonSerializerOptions opcoes)
    {
        var bruta = JsonSerializer.Deserialize<Bruta>(ref reader, SemEsteConversor(opcoes));
        if (bruta is null) return null;

        return bruta.Natureza == NaturezaDaInformacao.Fato
            ? Anotacao.Fato(bruta.Valor, bruta.Fonte, bruta.Quando)
            : Anotacao.Impressao(bruta.Valor, bruta.Quando);
    }

    public override void Write(
        Utf8JsonWriter escritor, Anotacao anotacao, JsonSerializerOptions opcoes) =>
        JsonSerializer.Serialize(
            escritor,
            new Bruta(anotacao.Valor, anotacao.Natureza, anotacao.Fonte, anotacao.Quando),
            SemEsteConversor(opcoes));

    /// <summary>
    /// `Bruta` e um record comum, mas serializa-lo com as mesmas opcoes traria
    /// este conversor de volta para um tipo que nao e o dele. Tirar da lista
    /// evita a duvida.
    /// </summary>
    private static JsonSerializerOptions SemEsteConversor(JsonSerializerOptions opcoes)
    {
        var limpas = new JsonSerializerOptions(opcoes);
        for (var i = limpas.Converters.Count - 1; i >= 0; i--)
            if (limpas.Converters[i] is AnotacaoJson) limpas.Converters.RemoveAt(i);

        return limpas;
    }
}
