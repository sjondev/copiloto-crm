using System.Text.Json;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>
/// O agente A1: le a conversa e diz o que ela mostra (#13).
///
/// Ele nao escreve para o cliente — entrega ao vendedor estagio, temperatura e
/// os sinais, cada um preso a fala que o originou.
///
/// A parte que importa nao e pedir a citacao ao modelo; e CONFERI-LA. Pedir no
/// prompt e pedido, e modelo atende pedido quando lhe convem. Aqui o trecho
/// citado e procurado na conversa de verdade, e o sinal que nao for encontrado
/// e descartado antes de existir — nao ha caminho pelo qual "o cliente
/// demonstrou interesse" chegue a tela sem uma frase real embaixo.
/// </summary>
public class AgenteDeLeitura
{
    private readonly CascataDeModelos _cascata;
    private readonly MontadorDeContexto _contexto;
    private readonly string _identidade;
    private readonly ILogger<AgenteDeLeitura> _log;

    public AgenteDeLeitura(
        CascataDeModelos cascata,
        MontadorDeContexto contexto,
        string identidade,
        ILogger<AgenteDeLeitura> log)
    {
        if (string.IsNullOrWhiteSpace(identidade))
            throw new ArgumentException(
                "O agente sem a camada C0 nao sabe que nao pode inventar. "
                + "Subir assim e pior que nao subir.", nameof(identidade));

        _cascata = cascata;
        _contexto = contexto;
        _identidade = identidade;
        _log = log;
    }

    /// <summary>
    /// Le a conversa. Devolve <c>null</c> quando a cascata se esgotou — a tela
    /// mantem o dossie anterior, e degradar em silencio e a decisao da #30.
    /// </summary>
    public async Task<Dossie?> Ler(
        Conversa conversa, Guid dealId, string playbook, string ficha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conversa);

        if (conversa.Mensagens.Count == 0)
        {
            // Conversa vazia nao tem o que ler, e um dossie vazio na tela
            // pareceria leitura feita. O modo abordagem inicial e a #87.
            _log.LogInformation("Conversa {Conversa} sem falas: nada a ler", conversa.Id);
            return null;
        }

        var falas = AgrupadorDeFalas.Agrupar(conversa.Mensagens);
        var contexto = _contexto.Montar(_identidade, playbook, ficha, falas);

        var resultado = await _cascata.Pedir(Tarefa.Leitura, contexto.Texto, ct);
        if (resultado.Degradou)
        {
            _log.LogWarning(
                "Leitura da conversa {Conversa} degradou apos {Falhas} degrau(s)",
                conversa.Id, resultado.Falhas.Count);
            return null;
        }

        return Montar(resultado.Resposta!.Conteudo, conversa, dealId);
    }

    private Dossie? Montar(string json, Conversa conversa, Guid dealId)
    {
        JsonElement lido;
        try
        {
            lido = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException e)
        {
            // O Contract Validator (#32) e quem vai repromptar. Ate la, resposta
            // ilegivel vale o mesmo que resposta nenhuma: a tela nao muda.
            _log.LogWarning("Leitura ilegivel da conversa {Conversa}: {Erro}", conversa.Id, e.Message);
            return null;
        }

        var dossie = new Dossie(Guid.NewGuid(), dealId, DateTimeOffset.UtcNow);
        dossie.Ler(LerTermometro(lido));

        // As lacunas que o modelo apontou. Elas NAO exigem citacao, e a
        // diferenca e o ponto: sinal afirma algo sobre a conversa e precisa da
        // frase que o sustente; lacuna diz o que a conversa NAO tem, e nao ha
        // frase para citar quando o assunto nunca apareceu.
        //
        // O agente dedicado de BANT e a #8. Aqui so se aproveita o que o A1 ja
        // devolveu — descartar seria jogar fora a parte mais util do dossie.
        foreach (var lacuna in Lista(lido, "lacunas"))
        {
            if (lacuna.ValueKind == JsonValueKind.String) dossie.RegistrarLacuna(lacuna.GetString()!);
        }

        dossie.DeclararMidiaNaoInterpretada(conversa.Mensagens);

        // A leitura de comportamento roda SEMPRE, com modelo ou sem ele: e
        // aritmetica sobre a conversa (#9). Entra antes das objecoes que o
        // modelo apontou porque, empatando tipo e trecho, a versao que nao
        // alucina e a que fica.
        dossie.DetectarObjecoes(conversa, DateTimeOffset.UtcNow);

        var inventados = 0;

        foreach (var bruto in Lista(lido, "objecoes"))
        {
            var trecho = Texto(bruto, "trecho_citado");
            var descricao = Texto(bruto, "descricao");
            if (string.IsNullOrWhiteSpace(trecho) || string.IsNullOrWhiteSpace(descricao)) continue;

            // Mesma conferencia dos sinais: a frase citada tem que existir na
            // conversa. Objecao inventada seria pior que sinal inventado — ela
            // manda o vendedor tratar uma resistencia que o cliente nao tem.
            var origem = QuemDisse(conversa, trecho);
            if (origem is null)
            {
                inventados++;
                continue;
            }

            dossie.Registrar(new Objecao(
                LerTipoDeObjecao(bruto), descricao, trecho, origem.Id, PorComportamento: false));
        }

        foreach (var bruto in Lista(lido, "sinais"))
        {
            var trecho = Texto(bruto, "trecho_citado");
            var descricao = Texto(bruto, "descricao");

            if (string.IsNullOrWhiteSpace(trecho) || string.IsNullOrWhiteSpace(descricao))
            {
                inventados++;
                continue;
            }

            // A conferencia: a frase citada tem que estar na conversa.
            var origem = QuemDisse(conversa, trecho);
            if (origem is null)
            {
                inventados++;
                _log.LogWarning(
                    "Sinal descartado na conversa {Conversa}: o trecho citado nao esta na conversa ({Trecho})",
                    conversa.Id, trecho);
                continue;
            }

            dossie.Registrar(new Sinal(descricao, origem.Id, trecho, LerTipo(bruto)));
        }

        if (inventados > 0)
        {
            _log.LogWarning(
                "Leitura da conversa {Conversa}: {Descartados} sinal(is) sem citacao real descartado(s)",
                conversa.Id, inventados);
        }

        return dossie;
    }

    /// <summary>
    /// A fala que contem o trecho citado, ou <c>null</c> se nenhuma contem.
    ///
    /// Comparacao frouxa no espaco e na caixa, e exata no resto: o modelo troca
    /// maiuscula e quebra de linha sem querer, mas trocar PALAVRA e o que
    /// distingue citar de inventar — e e justamente isso que nao pode passar.
    /// </summary>
    private static Mensagem? QuemDisse(Conversa conversa, string trecho)
    {
        var alvo = Achatar(trecho);
        if (alvo.Length == 0) return null;

        return conversa.Mensagens.FirstOrDefault(m => Achatar(m.Texto).Contains(alvo));
    }

    private static string Achatar(string texto) =>
        string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
              .ToLowerInvariant();

    private static Termometro LerTermometro(JsonElement lido) =>
        new(Enum.TryParse<Temperatura>(Texto(lido, "temperatura"), true, out var t) ? t : Temperatura.Morna,
            Enum.TryParse<Direcao>(Texto(lido, "direcao"), true, out var d) ? d : Direcao.Estavel);

    /// <summary>
    /// Tipo que nao reconhecemos vira <see cref="TipoDeObjecao.NaoClassificada"/>.
    ///
    /// Chutar "preco" porque e o mais comum mandaria o vendedor defender valor
    /// quando o problema era que ele nem falava com quem decide.
    /// </summary>
    private static TipoDeObjecao LerTipoDeObjecao(JsonElement bruto) =>
        Enum.TryParse<TipoDeObjecao>(Texto(bruto, "tipo"), true, out var tipo)
            ? tipo
            : TipoDeObjecao.NaoClassificada;

    private static TipoDeSinal LerTipo(JsonElement bruto) =>
        Enum.TryParse<TipoDeSinal>(Texto(bruto, "tipo"), true, out var tipo) ? tipo : TipoDeSinal.Compra;

    private static IEnumerable<JsonElement> Lista(JsonElement lido, string campo) =>
        lido.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : [];

    private static string? Texto(JsonElement bruto, string campo) =>
        bruto.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
