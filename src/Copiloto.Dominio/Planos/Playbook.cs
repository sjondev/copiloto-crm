namespace Copiloto.Dominio.Planos;

/// <summary>
/// O jeito da casa de vender: o que a empresa autoriza sugerir, por estagio.
///
/// Existe para o produto nao impor tatica de vendas generica a quem ja tem a
/// propria — e para o desconto maximo ser decisao da empresa, nao do modelo.
/// </summary>
public class Playbook
{
    private readonly List<Tatica> _taticasPermitidas = new();

    public Playbook(Guid id, string nome)
    {
        if (id == Guid.Empty) throw new ArgumentException("Playbook sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Playbook sem nome.", nameof(nome));

        Id = id;
        Nome = nome.Trim();
    }

    public Guid Id { get; }
    public string Nome { get; }
    public IReadOnlyList<Tatica> TaticasPermitidas => _taticasPermitidas;

    public void Permitir(Tatica tatica)
    {
        if (!_taticasPermitidas.Contains(tatica)) _taticasPermitidas.Add(tatica);
    }

    /// <summary>
    /// Playbook vazio permite tudo: uma empresa que ainda nao configurou nada
    /// nao pode receber um produto mudo. Restringir e uma escolha, e escolha
    /// nao configurada nao pode virar bloqueio silencioso.
    /// </summary>
    public bool Autoriza(Tatica tatica) =>
        _taticasPermitidas.Count == 0 || _taticasPermitidas.Contains(tatica);
}
