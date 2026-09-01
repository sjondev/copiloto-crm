namespace Copiloto.Dominio.Vendas;

/// <summary>O vendedor. Quem decide, e a quem o dossie se dirige.</summary>
public class Usuario
{
    public Usuario(Guid id, string nome, string email)
    {
        if (id == Guid.Empty) throw new ArgumentException("Usuario sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Usuario sem nome.", nameof(nome));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Usuario sem email.", nameof(email));

        Id = id;
        Nome = nome.Trim();
        Email = email.Trim();
    }

    public Guid Id { get; }
    public string Nome { get; }
    public string Email { get; }
}
