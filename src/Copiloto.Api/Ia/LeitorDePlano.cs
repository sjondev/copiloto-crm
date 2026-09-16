using System.Text.Json;
using Copiloto.Api.Seguranca;
using Copiloto.Dominio.Planos;

namespace Copiloto.Api.Ia;

/// <summary>O plano que sobreviveu a leitura, e o que ficou de fora.</summary>
public record PlanoLido(IReadOnlyList<BlocoSugerido> Blocos, IReadOnlyList<Bloqueio> Recusados);

/// <summary>
/// Traduz o JSON que o modelo devolveu em blocos do dominio (#15, #16).
///
/// E aqui que a regra de ancoragem encontra a resposta do modelo. O
/// <see cref="BlocoSugerido"/> ja torna impossivel CONSTRUIR uma sugestao
/// factual sem ancora — mas so se alguem passar por ele. Este leitor e o unico
/// caminho da resposta do modelo para a tela, e por isso e onde a tentativa de
/// invencao morre.
///
/// O que ele NAO faz e consertar. Bloco que afirma sem lastro e RECUSADO, nao
/// reescrito: texto remendado por nos apareceria como se o modelo tivesse
/// produzido, e o vendedor nao teria como saber qual e qual.
/// </summary>
public static class LeitorDePlano
{
    public static PlanoLido Ler(string json, Playbook playbook)
    {
        ArgumentNullException.ThrowIfNull(playbook);

        var blocos = new List<BlocoSugerido>();
        var recusados = new List<Bloqueio>();

        JsonElement raiz;
        try
        {
            raiz = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException e)
        {
            // Resposta ilegivel nao vira bloco nenhum, e isso e melhor que
            // adivinhar: metade de um plano e pior que plano nenhum.
            return new PlanoLido([], [new Bloqueio($"resposta ilegivel: {e.Message}", "")]);
        }

        if (!raiz.TryGetProperty("blocos", out var lista) || lista.ValueKind != JsonValueKind.Array)
            return new PlanoLido([], [new Bloqueio("resposta sem a lista de blocos", "")]);

        foreach (var bruto in lista.EnumerateArray())
        {
            var lido = LerBloco(bruto);
            if (lido.Recusa is not null) recusados.Add(lido.Recusa);
            else blocos.Add(lido.Bloco!);
        }

        // O guarda de saida (#44) e a segunda peneira: ele pega o que passa pelo
        // tipo mas nao devia chegar a tela — tatica fora do playbook, promessa
        // de gratuidade, desconto com numero.
        var (aprovados, barrados) = GuardaDeSaida.Filtrar(blocos, playbook);

        return new PlanoLido(aprovados, [.. recusados, .. barrados]);
    }

    private static (BlocoSugerido? Bloco, Bloqueio? Recusa) LerBloco(JsonElement bruto)
    {
        var tatica = LerTatica(bruto);
        var conselho = Texto(bruto, "conselho");
        var ancora = Texto(bruto, "ancorado_em");
        var pergunta = Texto(bruto, "pergunta_ao_vendedor");

        // Pergunta ao vendedor e a saida honesta quando falta dado, e ela vem
        // primeiro: um bloco que traz pergunta E conselho esta querendo as duas
        // coisas, e a pergunta e a que nao afirma nada ao cliente.
        if (!string.IsNullOrWhiteSpace(pergunta))
            return (BlocoSugerido.Perguntar(tatica, pergunta), null);

        if (string.IsNullOrWhiteSpace(conselho))
            return (null, new Bloqueio("bloco sem conselho e sem pergunta", bruto.GetRawText()));

        if (BlocoSugerido.PrecisaDeAncora(tatica) && string.IsNullOrWhiteSpace(ancora))
        {
            // O caso que a #16 existe para provar: o modelo afirmou escassez,
            // desconto, prazo ou prova social sem dado do CRM que sustente, e
            // sem oferecer a pergunta. Nao passa.
            return (null, new Bloqueio(
                $"a tatica {tatica} afirmou sem ancora no CRM e sem virar pergunta",
                conselho));
        }

        return (BlocoSugerido.Ancorado(tatica, conselho, ancora ?? ""), null);
    }

    /// <summary>
    /// Tatica que nao reconhecemos vira a mais restrita, e nao a mais livre.
    ///
    /// Cair em <c>Livre</c> por nome desconhecido seria a porta dos fundos da
    /// regra inteira: bastaria o modelo escrever "Escassez2" para afirmar o que
    /// quisesse sem precisar de ancora nenhuma.
    /// </summary>
    private static Tatica LerTatica(JsonElement bruto) =>
        Enum.TryParse<Tatica>(Texto(bruto, "tecnica"), ignoreCase: true, out var t)
            ? t
            : Tatica.Escassez;

    private static string? Texto(JsonElement bruto, string campo) =>
        bruto.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
