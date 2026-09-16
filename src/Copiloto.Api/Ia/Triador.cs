using System.Text.Json;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>O que o triador poupou ate agora.</summary>
/// <param name="TokensPorLeitura">
/// O tamanho tipico de uma leitura, usado para estimar o que NAO foi gasto. E
/// estimativa declarada, e nao medicao: o custo de uma chamada que nunca
/// aconteceu nao existe em lugar nenhum para ser medido.
/// </param>
public record EconomiaDaTriagem(
    int Analisadas,
    int DescartadasPorHeuristica,
    int DescartadasPorModelo,
    decimal CustoPorMilTokens,
    int TokensPorLeitura)
{
    public int Total => Analisadas + DescartadasPorHeuristica + DescartadasPorModelo;
    public int Descartadas => DescartadasPorHeuristica + DescartadasPorModelo;

    public double PercentualDeDescarte => Total == 0 ? 0 : (double)Descartadas / Total;

    /// <summary>Em reais, e arredondado para baixo do que seria: ver o record.</summary>
    public decimal EconomiaEstimada =>
        Descartadas * CustoPorMilTokens * TokensPorLeitura / 1000m;
}

/// <summary>
/// Conta o que a triagem poupou (#37).
///
/// Vive em memoria e zera no restart. O numero que sobrevive e o do ledger
/// (#1); este aqui responde "esta funcionando agora?", que e a pergunta de
/// quem esta olhando o painel (#3) — e ele nao justifica uma tabela.
/// </summary>
public class ContadorDeTriagem
{
    private int _analisadas;
    private int _porHeuristica;
    private int _porModelo;

    public ContadorDeTriagem(decimal custoPorMilTokens = 0m, int tokensPorLeitura = 1450)
    {
        CustoPorMilTokens = custoPorMilTokens;
        TokensPorLeitura = tokensPorLeitura;
    }

    public decimal CustoPorMilTokens { get; }
    public int TokensPorLeitura { get; }

    public void Registrar(Triagem triagem)
    {
        if (triagem.Analisa) Interlocked.Increment(ref _analisadas);
        else if (triagem.PorHeuristica) Interlocked.Increment(ref _porHeuristica);
        else Interlocked.Increment(ref _porModelo);
    }

    public EconomiaDaTriagem Agora() => new(
        Volatile.Read(ref _analisadas),
        Volatile.Read(ref _porHeuristica),
        Volatile.Read(ref _porModelo),
        CustoPorMilTokens,
        TokensPorLeitura);
}

/// <summary>
/// O agente A0: decide se a fala vale acordar o modelo caro (#37).
///
/// Duas camadas. A heuristica resolve o obvio de graca e sem rede; o que ela
/// nao sabe vai para o modelo MAIS BARATO da tabela, nunca para o de leitura.
///
/// Na duvida, ANALISA. Errar analisando custa uma chamada barata; errar
/// descartando custa um sinal que o vendedor nunca vai saber que existiu — e
/// ele nao tem como reclamar de algo que nao apareceu na tela.
/// </summary>
public class Triador
{
    private readonly CascataDeModelos _cascata;
    private readonly ContadorDeTriagem _contador;
    private readonly ILogger<Triador> _log;

    public Triador(CascataDeModelos cascata, ContadorDeTriagem contador, ILogger<Triador> log)
    {
        _cascata = cascata;
        _contador = contador;
        _log = log;
    }

    public async Task<Triagem> Triar(Mensagem fala, Mensagem? anterior, CancellationToken ct)
    {
        var triagem = TriagemPorHeuristica.Avaliar(fala, anterior) ?? await PerguntarAoModelo(fala, ct);

        _contador.Registrar(triagem);

        if (!triagem.Analisa)
        {
            _log.LogInformation(
                "Fala {Fala} descartada na triagem ({Motivo}, heuristica={Heuristica})",
                fala.Id, triagem.Motivo, triagem.PorHeuristica);
        }

        return triagem;
    }

    private async Task<Triagem> PerguntarAoModelo(Mensagem fala, CancellationToken ct)
    {
        var resultado = await _cascata.Pedir(Tarefa.Triagem, fala.Texto, ct);

        if (resultado.Degradou)
        {
            // Sem triador, analisa. Degradar para "descartar tudo" faria o
            // dossie parar de atualizar em silencio justamente quando a
            // infraestrutura ja esta ruim.
            return new Triagem(Veredito.Analisar, "triador indisponivel: na duvida, analisa", false);
        }

        try
        {
            var lido = JsonDocument.Parse(resultado.Resposta!.Conteudo).RootElement;

            var vale = !lido.TryGetProperty("vale_analisar", out var v)
                       || v.ValueKind != JsonValueKind.False;

            var motivo = lido.TryGetProperty("motivo", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()!
                : "sem motivo declarado";

            return new Triagem(vale ? Veredito.Analisar : Veredito.Descartar, motivo, false);
        }
        catch (JsonException)
        {
            // Mesma razao: resposta ilegivel do triador nao pode virar descarte.
            return new Triagem(Veredito.Analisar, "triador respondeu ilegivel: na duvida, analisa", false);
        }
    }
}
