using System.Text.Json;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Planos;

namespace Copiloto.Api.Ia;

/// <summary>
/// A5 por bloco: uma sugestao para UM campo do plano (#189).
///
/// Nao monta o plano inteiro. O vendedor pede ajuda no bloco em que travou, e
/// gerar os quatro de uma vez faria a IA escrever o plano dele — que e
/// exatamente o contrario da tese da #12.
///
/// Devolve <c>null</c> quando degrada, e nao levanta excecao: o plano continua
/// na tela com o que ele escreveu, e o botao volta ao normal. Sugestao que nao
/// veio nao pode custar o texto que ele ja tinha.
/// </summary>
public class AgenteDePlano
{
    private readonly CascataDeModelos _cascata;
    private readonly PrecoDoModelo _preco;
    private readonly string _instrucoes;
    private readonly ILogger<AgenteDePlano> _log;

    public AgenteDePlano(
        CascataDeModelos cascata, PrecoDoModelo preco, string instrucoes,
        ILogger<AgenteDePlano> log)
    {
        ArgumentNullException.ThrowIfNull(cascata);
        ArgumentNullException.ThrowIfNull(preco);

        _cascata = cascata;
        _preco = preco;
        _instrucoes = instrucoes;
        _log = log;
    }

    /// <summary>
    /// O que saiu da chamada: a sugestao (ou nenhuma) e a MEDICAO.
    ///
    /// A medicao vem sempre, inclusive quando nao ha sugestao — o provedor cobra
    /// pelo token gasto antes de falhar, e ledger que so conta acerto esconde
    /// justamente o custo que ninguem esperava ter (#1).
    /// </summary>
    public record Sugestao(string? Texto, MedicaoDaChamada Medicao);

    public async Task<Sugestao> Sugerir(
        BlocoDoPlano bloco, string contextoDoLead, CancellationToken ct)
    {
        var pedido = $"{_instrucoes}\n\n## Bloco pedido\n\n{bloco}\n\n"
                     + $"## O que sabemos deste cliente\n\n{contextoDoLead}";

        ResultadoDaCascata resultado;
        long latenciaMs;

        try
        {
            (resultado, latenciaMs) = await Ledger.Cronometrar(
                () => _cascata.Pedir(Tarefa.Plano, pedido, ct));
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // A cascata degrada sozinha nas falhas que ela CONHECE (provedor
            // fora do ar, limite de taxa). O que chega aqui e o que ela nao
            // previu — provedor mal configurado, resposta que nem chegou a ser
            // uma resposta.
            //
            // Vira sugestao ausente, e nao 500. O vendedor clicou num botao
            // opcional no meio de uma venda: derrubar a tela dele por isso
            // trocaria "a sugestao nao veio" por "o sistema quebrou". O erro
            // nao some — ele vai para o log COM o bloco e a excecao, que e o
            // que a #42 pede.
            _log.LogError(erro, "Sugestao do bloco {Bloco} falhou de forma nao prevista", bloco);

            // Sem resultado nao ha o que medir com honestidade: nao se sabe qual
            // modelo foi chamado nem se ele chegou a gastar token. Uma linha
            // inventada aqui seria pior que linha nenhuma.
            return new Sugestao(null, null!);
        }

        var medicao = Ledger.Medir(resultado, latenciaMs, _preco);

        if (resultado.Degradou)
        {
            _log.LogWarning(
                "Sugestao do bloco {Bloco} degradou apos {Falhas} degrau(s)",
                bloco, resultado.Falhas.Count);
            return new Sugestao(null, medicao);
        }

        var texto = Extrair(resultado.Resposta!.Conteudo, bloco);
        if (texto is null)
        {
            // A chamada ACONTECEU e custou: a linha entra no ledger mesmo com a
            // resposta ilegivel. Esconder isso faria o contrato quebrado sair de
            // graca no relatorio.
            _log.LogWarning("Sugestao do bloco {Bloco} veio em formato ilegivel", bloco);
            return new Sugestao(null, medicao);
        }

        return new Sugestao(texto, medicao);
    }

    /// <summary>
    /// Aceita as DUAS formas, e isso e deliberado.
    ///
    /// Modelo de verdade recebe um bloco e devolve <c>{"sugestao": "..."}</c>. O
    /// provedor fake responde de arquivo e guarda um mapa com os quatro — porque
    /// ele e deterministico por tarefa, e uma frase so faria os quatro campos da
    /// demo mostrarem o mesmo texto.
    ///
    /// A alternativa seria gravar quatro arquivos e ensinar o fake a escolher por
    /// prompt, o que troca um `if` aqui por um mecanismo de selecao la — mais
    /// peca para manter, e o fake deixaria de ser "responde o que esta no
    /// arquivo".
    /// </summary>
    private static string? Extrair(string json, BlocoDoPlano bloco)
    {
        JsonElement raiz;
        try
        {
            raiz = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        if (raiz.TryGetProperty("sugestao", out var uma) &&
            uma.ValueKind == JsonValueKind.String)
        {
            return Limpar(uma.GetString());
        }

        if (raiz.TryGetProperty("sugestoes", out var muitas) &&
            muitas.ValueKind == JsonValueKind.Object &&
            muitas.TryGetProperty(bloco.ToString(), out var doBloco) &&
            doBloco.ValueKind == JsonValueKind.String)
        {
            return Limpar(doBloco.GetString());
        }

        return null;
    }

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
