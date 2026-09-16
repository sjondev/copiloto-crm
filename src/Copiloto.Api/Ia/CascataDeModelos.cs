using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>Um degrau que nao deu certo, e por que.</summary>
public record DegrauQueFalhou(string Modelo, string Provedor, string Motivo);

/// <summary>
/// O que a cascata conseguiu, e o que custou chegar la.
/// </summary>
/// <param name="Resposta">Nula quando a cascata inteira se esgotou.</param>
/// <param name="Modelo">Quem respondeu, para o ledger saber de quem foi o custo.</param>
/// <param name="Falhas">Cada degrau tentado antes, na ordem. Vai inteiro ao ledger (#1).</param>
public record ResultadoDaCascata(
    RespostaDoModelo? Resposta,
    string? Modelo,
    IReadOnlyList<DegrauQueFalhou> Falhas)
{
    /// <summary>
    /// A cascata acabou sem resposta, e a tela precisa manter o ultimo estado
    /// valido com um aviso discreto — nunca um erro.
    /// </summary>
    public bool Degradou => Resposta is null;
}

/// <summary>
/// Desce a cascata de modelos ate alguem responder, ou ate degradar (#30).
///
/// A regra de produto que manda aqui: o vendedor esta no meio de uma venda, com
/// o cliente digitando do outro lado. Erro na tela naquele momento e pior que
/// informacao levemente desatualizada — entao a cascata esgotada NAO levanta
/// excecao. Ela devolve <see cref="ResultadoDaCascata.Degradou"/>, e a tela
/// mantem o que ja tinha.
///
/// O que esta cascata trata e falha de TRANSPORTE: o provedor caiu, estourou a
/// cota, nao respondeu a tempo. Resposta que chegou inteira mas veio com
/// conteudo quebrado e outro problema, e quem resolve e o Contract Validator
/// (#32) — misturar os dois aqui transformaria a cascata no try/catch em volta
/// de um HttpClient que ela existe para nao ser.
/// </summary>
public class CascataDeModelos
{
    private readonly RoteadorDeModelo _router;
    private readonly IModelProvider _provedor;
    private readonly ILogger<CascataDeModelos> _log;

    public CascataDeModelos(
        RoteadorDeModelo router, IModelProvider provedor, ILogger<CascataDeModelos> log)
    {
        _router = router;
        _provedor = provedor;
        _log = log;
    }

    public async Task<ResultadoDaCascata> Pedir(Tarefa tarefa, string prompt, CancellationToken ct)
    {
        var degraus = _router.Ordenar(tarefa);
        var falhas = new List<DegrauQueFalhou>();

        foreach (var degrau in degraus)
        {
            try
            {
                var resposta = await _provedor.Responder(
                    new PedidoAoModelo(tarefa, degrau.Nome, prompt), ct);

                if (falhas.Count > 0)
                {
                    _log.LogWarning(
                        "Tarefa {Tarefa} respondida por {Modelo} apos {Quedas} degrau(s) que falharam",
                        tarefa, degrau.Nome, falhas.Count);
                }

                return new ResultadoDaCascata(resposta, degrau.Nome, falhas);
            }
            catch (Exception e) when (EhFalhaDeTransporte(e))
            {
                falhas.Add(new DegrauQueFalhou(degrau.Nome, degrau.Provedor, Motivo(e)));

                _log.LogWarning(
                    "Degrau {Modelo} ({Provedor}) falhou na tarefa {Tarefa}: {Motivo}",
                    degrau.Nome, degrau.Provedor, tarefa, Motivo(e));
            }
        }

        // Nenhum erro propagado. A cascata esgotada e um desfecho previsto, e
        // nao uma excecao: quem chama precisa poder manter o ultimo estado
        // valido sem escrever try/catch para isso.
        _log.LogError(
            "Cascata esgotada na tarefa {Tarefa} apos {Degraus} degrau(s). Degradando.",
            tarefa, degraus.Count);

        return new ResultadoDaCascata(null, null, falhas);
    }

    /// <summary>
    /// O que faz descer um degrau.
    ///
    /// A lista e fechada de proposito. Um `catch (Exception)` engoliria bug
    /// nosso — NullReference, argumento invalido — como se fosse provedor
    /// caido, e a cascata desceria os degraus todos repetindo o mesmo defeito
    /// ate degradar em silencio. Defeito nosso tem que estourar.
    /// </summary>
    private static bool EhFalhaDeTransporte(Exception e) =>
        e is ProvedorForaDoAr or LimiteDeTaxaExcedido or TimeoutException or HttpRequestException;

    private static string Motivo(Exception e) => e switch
    {
        LimiteDeTaxaExcedido limite when limite.TentarDepoisDe is { } espera
            => $"limite de taxa, tentar em {espera.TotalSeconds:0}s",
        LimiteDeTaxaExcedido => "limite de taxa",
        TimeoutException => "nao respondeu a tempo",
        ProvedorForaDoAr => "fora do ar",
        _ => e.GetType().Name,
    };
}
