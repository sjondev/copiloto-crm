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

    /// <summary>
    /// De que lado da mesa esta esta pessoa (#85).
    ///
    /// Nasce Cliente porque e o caso comum e porque errar para esse lado e
    /// menos grave: cliente marcado como parceiro perde recurso; parceiro
    /// marcado como cliente entra na base de precedentes de venda, e a
    /// negociacao de fornecimento — com margem e custo — pode ser recuperada
    /// enquanto o vendedor atende um comprador.
    /// </summary>
    public Relacao Relacao { get; private set; } = Relacao.Cliente;
    public string? Nome { get; private set; }
    public DateTimeOffset CriadoEm { get; }

    /// <summary>
    /// Marca quem esta do outro lado como fornecedor, transportadora,
    /// representante — quem vende PARA a empresa, e nao compra dela.
    /// </summary>
    public void MarcarComo(Relacao relacao) => Relacao = relacao;

    /// <summary>Nome descoberto no meio da conversa, que e como ele costuma chegar.</summary>
    public void Identificar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        Nome = nome.Trim();
    }
}
