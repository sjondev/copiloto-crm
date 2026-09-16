using System.Text.Json;
using System.Text.Json.Serialization;
using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>O cenario ruim que se quer reproduzir de proposito.</summary>
public enum FalhaSimulada
{
    Nenhuma = 0,

    /// <summary>O provedor aceitou e nunca respondeu.</summary>
    Timeout = 1,

    /// <summary>HTTP 429: cota estourada.</summary>
    LimiteExcedido = 2,

    /// <summary>
    /// O modelo respondeu, mas o que veio nao e JSON valido.
    ///
    /// NAO levanta excecao — e resposta com sucesso e conteudo quebrado, que e
    /// exatamente como o caso acontece de verdade. Quem trata e o Contract
    /// Validator (#32), e ele so pode ser testado se o fake souber mentir assim.
    /// </summary>
    JsonInvalido = 3,

    /// <summary>Conexao recusada, 5xx, DNS morto.</summary>
    ProvedorForaDoAr = 4,
}

/// <summary>Uma resposta gravada em `seed/respostas/`.</summary>
public record RespostaGravada(Tarefa Tarefa, JsonElement Resposta, int TokensEntrada, int TokensSaida);

/// <summary>
/// Responde a partir de arquivo, sem tocar em rede (#27).
///
/// E a PRIMEIRA coisa construida na camada de IA, nao a ultima: a orquestracao
/// inteira — cascata, circuito, validador de contrato, triador — e feita de
/// tratamento de caso ruim, e caso ruim so se testa se der para provoca-lo na
/// hora exata. Contra API real isso nao existe: ninguem consegue um 429 sob
/// demanda, e o codigo que trata 429 fica sendo o unico que nunca rodou.
/// </summary>
public class FakeProvider : IModelProvider
{
    public const string NomeDoProvedor = "fake";

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        // A tarefa vem escrita por extenso no arquivo. "Leitura" diz o que e;
        // o 1 que o enum vale por baixo nao diz nada a quem edita o seed.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyDictionary<Tarefa, RespostaGravada> _respostas;
    private readonly Queue<FalhaSimulada> _combinadas = new();

    public FakeProvider(IReadOnlyDictionary<Tarefa, RespostaGravada>? respostas = null) =>
        _respostas = respostas ?? new Dictionary<Tarefa, RespostaGravada>();

    /// <summary>Carrega o que estiver em `seed/respostas/*.json`.</summary>
    public static FakeProvider DaPasta(string pasta)
    {
        if (!Directory.Exists(pasta))
            throw new DirectoryNotFoundException(
                $"Pasta de respostas nao encontrada: {pasta}. O FakeProvider e o padrao, "
                + "entao a ausencia dela quebraria o projeto no primeiro clone — e o erro "
                + "precisa dizer isso, nao 'sequencia vazia'.");

        var gravadas = Directory.GetFiles(pasta, "*.json")
            .OrderBy(a => a)
            .Select(a => JsonSerializer.Deserialize<RespostaGravada>(File.ReadAllText(a), Opcoes)
                         ?? throw new InvalidOperationException($"resposta gravada vazia: {a}"))
            .ToDictionary(r => r.Tarefa);

        return new FakeProvider(gravadas);
    }

    public string Nome => NomeDoProvedor;

    /// <summary>
    /// A espera artificial antes de responder.
    ///
    /// Zero por padrao: a suite roda em menos de um segundo e deve continuar
    /// assim. Vale para a demo, onde resposta instantanea parece falsa, e para
    /// medir o efeito de provedor lento sem depender de um.
    /// </summary>
    public TimeSpan Latencia { get; set; } = TimeSpan.Zero;

    public IReadOnlyCollection<Tarefa> TarefasGravadas => _respostas.Keys.ToList();

    /// <summary>
    /// Combina que as proximas <paramref name="vezes"/> chamadas falham assim.
    ///
    /// E fila e nao um estado unico porque o caso que interessa e "falha tres
    /// vezes e ai responde": e o que abre e fecha o circuito (#38) e o que
    /// exercita a cascata (#30). Com um estado so, esse teste viraria um
    /// liga-desliga manual no meio do cenario.
    /// </summary>
    public void CombinarFalha(FalhaSimulada falha, int vezes = 1)
    {
        for (var i = 0; i < vezes; i++) _combinadas.Enqueue(falha);
    }

    public async Task<RespostaDoModelo> Responder(PedidoAoModelo pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        if (Latencia > TimeSpan.Zero) await Task.Delay(Latencia, ct);

        var falha = _combinadas.Count > 0 ? _combinadas.Dequeue() : FalhaSimulada.Nenhuma;

        // Timeout levanta na hora em vez de esperar de verdade: uma suite que
        // espera o timeout acontecer demora o timeout inteiro, e o que se quer
        // provar e o TRATAMENTO, nao a passagem do tempo.
        if (falha == FalhaSimulada.Timeout)
            throw new TimeoutException($"Provedor '{Nome}' nao respondeu a tempo.");

        if (falha == FalhaSimulada.LimiteExcedido)
            throw new LimiteDeTaxaExcedido(Nome, TimeSpan.FromSeconds(2));

        if (falha == FalhaSimulada.ProvedorForaDoAr)
            throw new ProvedorForaDoAr(Nome);

        if (falha == FalhaSimulada.JsonInvalido)
            return new RespostaDoModelo("{\"sinais\": [{\"descricao\": \"preco cit", 100, 12);

        if (!_respostas.TryGetValue(pedido.Tarefa, out var gravada))
            throw new InvalidOperationException(
                $"Nao ha resposta gravada para a tarefa {pedido.Tarefa} em seed/respostas/. "
                + "O fake nao inventa conteudo: resposta improvisada passaria no teste e "
                + "esconderia que o agente daquela tarefa nunca foi exercitado.");

        return new RespostaDoModelo(
            gravada.Resposta.GetRawText(), gravada.TokensEntrada, gravada.TokensSaida);
    }
}
