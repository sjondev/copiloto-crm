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

    /// <summary>
    /// O titular se opos a analise por IA (art. 18, #81).
    ///
    /// Nao apaga nada: o historico comercial continua, o vendedor continua
    /// vendendo, e a conversa continua sendo guardada pela mesma finalidade de
    /// antes. O que para e a ANALISE — dossie, sinais, plano.
    ///
    /// Separar as duas coisas e o que faz a oposicao ser usavel: se opor-se
    /// custasse o historico do negocio, ninguem se oporia, e a base de legitimo
    /// interesse ficaria fragil justamente por falta de canal real.
    /// </summary>
    public bool AnaliseDeIaSuspensa { get; private set; }

    public DateTimeOffset? OpostoEm { get; private set; }

    /// <summary>Nome descoberto no meio da conversa, que e como ele costuma chegar.</summary>
    public void Identificar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        Nome = nome.Trim();
    }

    /// <summary>
    /// Correcao de dado cadastral (art. 18, III).
    ///
    /// Vale para o nome, que e o que costuma vir errado — soletrado no
    /// atendimento, ou de outra pessoa que usou o mesmo telefone. O telefone
    /// nao muda aqui: ele e a identidade pratica do Lead, e troca-lo em silencio
    /// misturaria dois titulares num registro so.
    /// </summary>
    public void CorrigirNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException(
                "Correcao precisa do valor certo. Para APAGAR o nome, o pedido e "
                + "de exclusao (#46), que tem outro rito.", nameof(nome));

        Nome = nome.Trim();
    }

    public void OporSeAAnalise(DateTimeOffset quando)
    {
        AnaliseDeIaSuspensa = true;
        OpostoEm = quando;
    }

    /// <summary>O titular mudou de ideia. Acontece, e precisa ser tao facil quanto opor-se.</summary>
    public void RetomarAnalise()
    {
        AnaliseDeIaSuspensa = false;
        OpostoEm = null;
    }
}
