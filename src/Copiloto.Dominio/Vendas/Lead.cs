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
    /// <summary>De onde veio o nome quando ele chega do provedor.</summary>
    public const string FontePerfilDoWhatsApp = "perfil do WhatsApp";

    private readonly List<string> _outrosNumeros = new();

    public Lead(Guid id, string telefone, DateTimeOffset criadoEm, string? nome = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Lead sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(telefone))
            throw new ArgumentException("Lead sem telefone nao tem como ser contatado.", nameof(telefone));

        Id = id;

        // NORMALIZA aqui, e nao no chamador. A regra ja estava escrita no
        // ResolvedorDeLead — "a resolucao e por telefone normalizado, nunca pela
        // string que chegou" —, mas vivia na convencao de quem chama: bastava um
        // caminho novo guardar "(11) 98765-4321" para o mesmo cliente virar dois
        // leads, e o defeito so apareceria como historico partido no meio.
        Telefone = Vendas.Telefone.Normalizar(telefone)?.E164
            ?? throw new ArgumentException(
                $"'{telefone}' nao e um telefone brasileiro valido, e e por ele que "
                + "esta pessoa e reconhecida.", nameof(telefone));
        CriadoEm = criadoEm;
        Nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
    }

    public Guid Id { get; }

    /// <summary>
    /// O numero por onde a pessoa apareceu primeiro. Continua sendo a
    /// identidade: e nele que esta o indice unico, e e a ele que a conversa que
    /// ja existe esta amarrada.
    /// </summary>
    public string Telefone { get; }

    /// <summary>
    /// Todos os numeros por onde esta pessoa fala (#177).
    ///
    /// O WhatsApp nao tem cadastro, entao a identidade pratica e o telefone — e
    /// quem troca de chip virava um lead novo, com a ficha num e o dossie no
    /// outro. O antigo NUNCA sai: some-lo trocaria dois leads partidos por um
    /// lead com historico faltando.
    /// </summary>
    public IReadOnlyList<string> Numeros => [Telefone, .. _outrosNumeros];

    /// <summary>Registra mais um numero desta mesma pessoa.</summary>
    public void TambemFalaPor(string telefone)
    {
        var normalizado = Vendas.Telefone.Normalizar(telefone)
            ?? throw new ArgumentException(
                $"'{telefone}' nao e um telefone brasileiro valido, e numero torto aqui "
                + "faria a pessoa deixar de ser reconhecida por ele.", nameof(telefone));

        if (FalaPor(normalizado.E164)) return;

        _outrosNumeros.Add(normalizado.E164);
    }

    /// <summary>Se esta pessoa e alcancavel por aquele numero, em qualquer formato.</summary>
    public bool FalaPor(string telefone)
    {
        var normalizado = Vendas.Telefone.Normalizar(telefone);
        if (normalizado is null) return false;

        return Numeros.Contains(normalizado.E164, StringComparer.Ordinal);
    }

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

    /// <summary>
    /// Marca quem esta do outro lado como fornecedor, transportadora,
    /// representante — quem vende PARA a empresa, e nao compra dela.
    /// </summary>
    public void MarcarComo(Relacao relacao) => Relacao = relacao;

    /// <summary>
    /// O vendedor dono deste lead (#49). Nulo enquanto ninguem assumiu.
    ///
    /// Lead entra pelo WhatsApp sem dono — nao ha formulario nem atribuicao no
    /// caminho —, e por isso o nulo NAO pode significar "de ninguem": um lead
    /// que so aparece depois de alguem atribuir e um lead que ninguem atende.
    /// Sem dono, ele e da equipe; com dono, e de quem assumiu.
    /// </summary>
    public Guid? VendedorId { get; private set; }

    /// <summary>
    /// Assume o lead. Nao rouba: quem ja tem dono precisa ser liberado antes,
    /// senao dois vendedores atendem o mesmo cliente sem saber um do outro.
    /// </summary>
    public string? Assumir(Guid vendedorId)
    {
        if (vendedorId == Guid.Empty)
            throw new ArgumentException("Sem vendedor.", nameof(vendedorId));

        if (VendedorId is { } dono && dono != vendedorId)
            return "Este lead ja e de outro vendedor. Peca para ele liberar.";

        VendedorId = vendedorId;
        return null;
    }

    public void Liberar() => VendedorId = null;

    /// <summary>
    /// De onde o nome saiu. O que chega do WhatsApp foi digitado pelo PROPRIO
    /// cliente no perfil dele, e nao apurado pelo vendedor — a #88 separou fato
    /// de impressao na ficha pelo mesmo motivo: sem procedencia, tudo entra no
    /// contexto com o mesmo peso.
    /// </summary>
    public string? NomeFonte { get; private set; }

    /// <summary>Nome descoberto no meio da conversa, que e como ele costuma chegar.</summary>
    public void Identificar(string nome, string? fonte = null)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;

        Nome = nome.Trim();
        NomeFonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
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
