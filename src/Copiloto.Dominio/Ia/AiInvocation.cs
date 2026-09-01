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
    public AiInvocation(
        Guid id, string modelo, decimal custoEmReais, DateTimeOffset quando)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invocacao sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(modelo))
            throw new ArgumentException(
                "Invocacao sem modelo: o custo sem o nome do modelo nao responde "
                + "se vale trocar de provedor.", nameof(modelo));
        if (custoEmReais < 0)
            throw new ArgumentOutOfRangeException(nameof(custoEmReais),
                "Custo negativo nao existe, e somado ao acumulado ele o faria DIMINUIR.");

        Id = id;
        Modelo = modelo.Trim();
        CustoEmReais = custoEmReais;
        Quando = quando;
    }

    public Guid Id { get; }
    public string Modelo { get; }
    public decimal CustoEmReais { get; }
    public DateTimeOffset Quando { get; }
}
