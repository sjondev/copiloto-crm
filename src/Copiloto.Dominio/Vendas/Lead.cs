namespace Copiloto.Dominio.Vendas;

/// <summary>
/// Quem esta do outro lado da conversa.
///
/// O telefone e a identidade pratica: o WhatsApp e a origem, e la nao existe
/// cadastro. Nome pode faltar — chega "bom dia, vi o cafe" e mais nada, e um
/// Lead que so existe depois de alguem preencher formulario e um Lead que
/// nunca existe.
/// </summary>
public class Lead
{
    public Lead(Guid id, string telefone, DateTimeOffset criadoEm, string? nome = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Lead sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(telefone))
            throw new ArgumentException("Lead sem telefone nao tem como ser contatado.", nameof(telefone));

        Id = id;
        Telefone = telefone.Trim();
        CriadoEm = criadoEm;
        Nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
    }

    public Guid Id { get; }
    public string Telefone { get; }
    public string? Nome { get; private set; }
    public DateTimeOffset CriadoEm { get; }

    /// <summary>Nome descoberto no meio da conversa, que e como ele costuma chegar.</summary>
    public void Identificar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        Nome = nome.Trim();
    }
}
