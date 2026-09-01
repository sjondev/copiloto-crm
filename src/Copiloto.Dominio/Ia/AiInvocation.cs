namespace Copiloto.Dominio.Ia;

/// <summary>
/// Uma chamada a um modelo: qual, quanto custou, e quanto demorou.
///
/// Guardar o NOME do modelo junto do custo e o que permite a comparacao entre
/// provedores depois — sem ele, o total responde "gastamos X" e nao "gastamos X
/// com este e Y com aquele", que e a pergunta que decide a troca.
///
/// Imutavel: e um registro do que ja aconteceu.
/// </summary>
public class AiInvocation
{
    /// <param name="dealId">
    /// O negocio em que a invocacao aconteceu. Nulo so quando nao ha negocio no
    /// contexto — uma chamada de diagnostico, um teste de provedor. Havendo
    /// Deal, ele e obrigatorio, e quem cobra isso e o proprio Deal ao registrar.
    /// </param>
    public AiInvocation(
        Guid id, string modelo, decimal custoEmReais, DateTimeOffset quando,
        Guid? dealId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invocacao sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(modelo))
            throw new ArgumentException(
                "Invocacao sem modelo: o custo sem o nome do modelo nao responde "
                + "se vale trocar de provedor.", nameof(modelo));
        if (custoEmReais < 0)
            throw new ArgumentOutOfRangeException(nameof(custoEmReais),
                "Custo negativo nao existe, e somado ao acumulado ele o faria DIMINUIR.");

        if (dealId == Guid.Empty)
            throw new ArgumentException(
                "Guid.Empty nao e 'sem negocio': use null. Empty passaria por "
                + "preenchido e o custo seria somado a um Deal que nao existe.",
                nameof(dealId));

        Id = id;
        DealId = dealId;
        Modelo = modelo.Trim();
        CustoEmReais = custoEmReais;
        Quando = quando;
    }

    public Guid Id { get; }

    /// <summary>
    /// O vinculo custo-negocio. E barato agora e caro depois: enxertar rastreio
    /// num modelo ja povoado exige backfill e adivinhacao, e a resposta ficaria
    /// sendo estimativa para sempre — justamente na conta que decide se o
    /// produto se paga.
    /// </summary>
    public Guid? DealId { get; }

    public string Modelo { get; }
    public decimal CustoEmReais { get; }
    public DateTimeOffset Quando { get; }
}
