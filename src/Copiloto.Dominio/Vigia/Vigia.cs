using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Dominio.Vigia;

/// <summary>Por que o Vigia chamou a atencao para este negocio.</summary>
public enum MotivoDeAlerta
{
    /// <summary>O cliente parou de responder.</summary>
    ClienteEmSilencio = 0,

    /// <summary>O negocio nao anda de estagio ha tempo demais.</summary>
    NegocioParado = 1,

    /// <summary>Proposta na mesa esfriando.</summary>
    PropostaEnvelhecendo = 2,
}

/// <summary>
/// Um alerta do Vigia: o motivo, o negocio, e o DADO que o originou.
///
/// A citacao segue a mesma regra do sinal do dossie: sem ela, o alerta e
/// opiniao do sistema e o vendedor so pode aceitar ou ignorar. Com ela, ele
/// discorda com base em algo — "esse cliente respondeu por telefone ontem" — e
/// o alerta vira conversa em vez de ruido.
/// </summary>
public record Alerta(MotivoDeAlerta Motivo, Guid DealId, string Texto, DateTimeOffset Marco)
{
    /// <summary>
    /// A identidade do alerta para efeito de repeticao.
    ///
    /// Inclui o MARCO — a data do dado que o originou — e nao so o motivo. E o
    /// que faz o job rodar de hora em hora sem repetir, e ao mesmo tempo
    /// permite um alerta NOVO quando o cliente volta a falar e some de novo: o
    /// silencio passa a contar de outra fala, o marco muda, e aquilo e outro
    /// acontecimento.
    /// </summary>
    public string Chave => $"{DealId}:{Motivo}:{Marco:O}";
}

/// <summary>
/// O agente A6, que varre em vez de responder (#53).
///
/// Prova que a orquestracao nao e so request/response: ha trabalho autonomo,
/// agendado, que ninguem pediu — e resolve o problema real de negocio
/// esquecido, que e a forma mais barata de perder venda ja qualificada.
///
/// A varredura e DETERMINISTICA e nao chama modelo: silencio e tempo em
/// estagio sao contas de data. Gastar token para descobrir que faz nove dias
/// que ninguem fala seria pagar para fazer subtracao.
/// </summary>
public static class Vigia
{
    public static readonly TimeSpan SilencioQuePreocupa = TimeSpan.FromDays(3);
    public static readonly TimeSpan ParadoDemais = TimeSpan.FromDays(10);

    /// <summary>
    /// Proposta esfria mais rapido que negocio em qualificacao: o cliente
    /// pediu preco, recebeu, e cada dia sem resposta e uma comparacao a mais
    /// com a concorrencia.
    /// </summary>
    public static readonly TimeSpan PropostaEsfriando = TimeSpan.FromDays(5);

    /// <summary>
    /// O que merece a atencao do vendedor neste negocio, agora.
    ///
    /// Deal fechado nao gera alerta: ganho ou perdido, nao ha o que retomar, e
    /// alerta sobre negocio encerrado e o tipo de ruido que ensina o vendedor a
    /// ignorar a lista inteira.
    /// </summary>
    public static IEnumerable<Alerta> Varrer(Deal deal, Conversa? conversa, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(deal);

        if (deal.EstaFechado) yield break;

        var ultimaDoCliente = conversa?.UltimaDoCliente;
        if (ultimaDoCliente is not null)
        {
            var silencio = agora - ultimaDoCliente.EnviadaEm;
            if (silencio >= SilencioQuePreocupa)
            {
                yield return new Alerta(MotivoDeAlerta.ClienteEmSilencio, deal.Id,
                    $"Cliente sem responder há {(int)silencio.TotalDays} dias. "
                    + $"Última fala dele, em {ultimaDoCliente.EnviadaEm:dd/MM}: "
                    + $"\"{ultimaDoCliente.Texto}\"",
                    ultimaDoCliente.EnviadaEm);
            }
        }

        var parado = agora - deal.EstagioDesde;

        if (deal.Estagio == Estagio.Proposta && parado >= PropostaEsfriando)
        {
            yield return new Alerta(MotivoDeAlerta.PropostaEnvelhecendo, deal.Id,
                $"Proposta na mesa há {(int)parado.TotalDays} dias, desde "
                + $"{deal.EstagioDesde:dd/MM}. Cada dia é uma comparação a mais com a "
                + "concorrência.",
                deal.EstagioDesde);

            // Um acontecimento, um alerta: quem esta em Proposta ha 12 dias
            // tambem esta "parado ha 12 dias", e mandar as duas linhas seria o
            // mesmo aviso cobrado em dobro da atencao do vendedor.
            yield break;
        }

        if (parado >= ParadoDemais)
        {
            yield return new Alerta(MotivoDeAlerta.NegocioParado, deal.Id,
                $"Parado em {deal.Estagio} há {(int)parado.TotalDays} dias, desde "
                + $"{deal.EstagioDesde:dd/MM}.",
                deal.EstagioDesde);
        }
    }

    /// <summary>
    /// Filtra o que ja foi avisado. `jaAvisados` guarda <see cref="Alerta.Chave"/>.
    ///
    /// Ficar de fora daqui e o defeito mais caro do Vigia, e nao e o falso
    /// positivo: e repetir. Aviso repetido treina o vendedor a fechar a lista
    /// sem ler, e ai o alerta certo, quando vier, tambem nao e lido.
    /// </summary>
    public static IEnumerable<Alerta> Novos(
        IEnumerable<Alerta> alertas, ISet<string> jaAvisados) =>
        alertas.Where(a => jaAvisados.Add(a.Chave));
}
