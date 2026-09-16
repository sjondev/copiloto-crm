using System.Diagnostics;
using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>
/// Transforma o que a cascata devolveu numa linha do ledger (#1).
///
/// Mora aqui, e nao dentro da cascata, por uma razao de desenho: a cascata nao
/// conhece Deal, preco nem correlation-id, e enfiar isso nela faria a peca que
/// decide QUAL modelo chamar passar a saber de contabilidade. O que ela devolve
/// ja carrega tudo que a medicao precisa — inclusive as falhas, com o comentario
/// "vai inteiro ao ledger (#1)" escrito la desde o comeco.
/// </summary>
public static class Ledger
{
    /// <summary>
    /// Mede a chamada, tenha ela dado certo ou nao.
    ///
    /// Na falha, o modelo registrado e o ULTIMO degrau tentado: e ele que o
    /// provedor cobrou. Sem nenhum degrau — router sem modelo para a tarefa —, o
    /// nome fica explicito em vez de vazio, senao a linha vira custo orfao que
    /// ninguem consegue atribuir depois.
    /// </summary>
    public static MedicaoDaChamada Medir(
        ResultadoDaCascata resultado, long latenciaMs, PrecoDoModelo preco)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(preco);

        var modelo = resultado.Modelo
                     ?? (resultado.Falhas.Count > 0 ? resultado.Falhas[^1].Modelo : null)
                     ?? "nenhum-modelo-disponivel";

        var entrada = resultado.Resposta?.TokensEntrada ?? 0;
        var saida = resultado.Resposta?.TokensSaida ?? 0;

        return new MedicaoDaChamada(
            modelo,
            entrada,
            saida,
            (int)latenciaMs,

            // Contando o degrau que respondeu. Retentativa e um CAMPO desta
            // linha, e nao uma linha nova.
            resultado.Falhas.Count + (resultado.Degradou ? 0 : 1),
            !resultado.Degradou,
            preco.De(modelo, entrada + saida));
    }

    /// <summary>Cronometra a chamada e devolve o resultado com a latencia.</summary>
    public static async Task<(ResultadoDaCascata Resultado, long LatenciaMs)> Cronometrar(
        Func<Task<ResultadoDaCascata>> chamada)
    {
        ArgumentNullException.ThrowIfNull(chamada);

        var relogio = Stopwatch.StartNew();
        var resultado = await chamada();
        relogio.Stop();

        return (resultado, relogio.ElapsedMilliseconds);
    }
}
