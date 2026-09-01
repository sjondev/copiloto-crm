namespace Copiloto.Dominio.Planos;

/// <summary>
/// O plano de abordagem que o vendedor ve — blocos que ele escolhe usar ou nao.
/// </summary>
public class Plano
{
    private readonly List<BlocoSugerido> _blocos = new();

    public Plano(Guid id, Guid dealId, DateTimeOffset geradoEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Plano sem id.", nameof(id));
        if (dealId == Guid.Empty) throw new ArgumentException("Plano sem deal.", nameof(dealId));

        Id = id;
        DealId = dealId;
        GeradoEm = geradoEm;
    }

    public Guid Id { get; }
    public Guid DealId { get; }
    public DateTimeOffset GeradoEm { get; }
    public IReadOnlyList<BlocoSugerido> Blocos => _blocos;

    public void Adicionar(BlocoSugerido bloco)
    {
        ArgumentNullException.ThrowIfNull(bloco);
        _blocos.Add(bloco);
    }

    /// <summary>O que o vendedor precisa descobrir antes de usar o resto.</summary>
    public IReadOnlyList<BlocoSugerido> Perguntas =>
        _blocos.Where(b => b.EhPergunta).ToList();
}
