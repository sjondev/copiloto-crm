using Copiloto.Dominio.Ia;

namespace Copiloto.Dominio.Vendas;

/// <summary>
/// Uma negociacao com um <see cref="Lead"/>: onde ela esta no funil e quanto de
/// IA ja custou.
///
/// As transicoes moram AQUI, e nao no controller (#48). Regra de negocio dentro
/// do controller nao cria dependencia errada nenhuma — o grafo depois do commit
/// e identico ao de antes —, entao nenhuma analise de arquitetura acusa. O que
/// acusa e um teste, e teste so alcanca a regra se ela estiver num lugar que o
/// teste consegue chamar sem subir a aplicacao.
/// </summary>
public class Deal
{
    private readonly List<AiInvocation> _invocacoes = new();

    public Deal(Guid id, Guid leadId, DateTimeOffset abertoEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Deal sem id.", nameof(id));
        if (leadId == Guid.Empty) throw new ArgumentException("Deal sem lead.", nameof(leadId));

        Id = id;
        LeadId = leadId;
        AbertoEm = abertoEm;
        Estagio = Estagio.Novo;
    }

    public Guid Id { get; }
    public Guid LeadId { get; }
    public DateTimeOffset AbertoEm { get; }
    public Estagio Estagio { get; private set; }
    public DateTimeOffset? FechadoEm { get; private set; }

    /// <summary>
    /// O vinculo custo-negocio, e ele nasce com o Deal por decisao (#48).
    ///
    /// Enxertar depois obrigaria a reprocessar historico para responder "quanto
    /// custou fechar este negocio?" — e a resposta seria uma estimativa para
    /// sempre, justamente na conta que decide se o produto se paga.
    /// </summary>
    public decimal CustoIaAcumulado { get; private set; }

    public IReadOnlyList<AiInvocation> Invocacoes => _invocacoes;

    public bool EstaFechado => Estagio is Estagio.Ganho or Estagio.Perdido;

    /// <summary>
    /// Move o Deal de estagio. Devolve o que impediu, ou null quando moveu.
    ///
    /// Devolve motivo em vez de lancar excecao porque tentativa invalida aqui e
    /// caso ESPERADO — o vendedor arrasta o card de volta, clica errado, ou o
    /// negocio realmente reabre. Excecao para fluxo esperado obriga o chamador a
    /// usar try/catch como if, e o motivo em texto e o que a tela mostra.
    /// </summary>
    public string? MoverPara(Estagio destino, DateTimeOffset quando)
    {
        if (EstaFechado)
            return $"Deal ja esta {Estagio} e nao volta ao funil. Abra um novo.";

        if (destino == Estagio)
            return null;   // idempotente: reenviar a mesma transicao nao e erro

        if (destino == Estagio.Novo)
            return "Novo e o estagio de entrada e ninguem volta para ele.";

        if (destino is not Estagio.Ganho and not Estagio.Perdido
            && destino > Estagio + 1)
            return $"Nao da para pular de {Estagio} para {destino}: o funil anda "
                 + "de um em um, e estagio pulado e negocio sem qualificacao.";

        Estagio = destino;
        if (EstaFechado) FechadoEm = quando;
        return null;
    }

    /// <summary>
    /// Registra o que uma chamada de IA custou, e soma ao acumulado.
    ///
    /// A soma vive no Deal em vez de ser SUM() na consulta porque a pergunta que
    /// importa ("este negocio se paga?") e feita na tela do vendedor, uma vez por
    /// card, e nao vale varrer a tabela de invocacoes a cada render.
    /// </summary>
    public void RegistrarInvocacao(AiInvocation invocacao)
    {
        ArgumentNullException.ThrowIfNull(invocacao);
        _invocacoes.Add(invocacao);
        CustoIaAcumulado += invocacao.CustoEmReais;
    }
}
