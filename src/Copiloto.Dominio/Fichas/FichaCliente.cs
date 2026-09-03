namespace Copiloto.Dominio.Fichas;

/// <summary>O que o vendedor descobriu sobre a empresa. Tudo opcional.</summary>
public record SobreAEmpresa(
    Anotacao? Ramo = null,
    Anotacao? Porte = null,
    Anotacao? Momento = null,
    Anotacao? ComoChegou = null);

/// <summary>Quem esta do outro lado, e o poder que tem sobre a decisao.</summary>
public record SobreAPessoa(
    Anotacao? Cargo = null,
    Anotacao? PapelNaDecisao = null,
    Anotacao? QuemMaisDecide = null,
    Anotacao? EstiloObservado = null);

/// <summary>O negocio em si.</summary>
public record SobreONegocio(
    Anotacao? ProvavelNecessidade = null,
    Anotacao? UsaHoje = null,
    Anotacao? OrcamentoEstimado = null,
    Anotacao? RiscoConhecido = null);

/// <summary>Uma versao anterior da ficha, com quando e quem mudou.</summary>
public record VersaoDaFicha(
    DateTimeOffset Quando,
    SobreAEmpresa Empresa,
    SobreAPessoa Pessoa,
    SobreONegocio Negocio);

/// <summary>
/// O que o vendedor descobriu ANTES de falar (#86).
///
/// Resolve o cold start, que era um buraco real: sem conversa, o copiloto nao
/// servia para nada — justamente quando o vendedor mais precisa de ajuda.
///
/// TODOS os campos sao opcionais, e isso e a decisao central. Formulario longo
/// com campo obrigatorio nao e preenchido: fica pela metade, e o que se ganha e
/// um dado falso no campo que alguem foi forcado a inventar para salvar. Uma
/// ficha de tres linhas verdadeiras vale mais que quinze campos preenchidos no
/// chute.
/// </summary>
public class FichaCliente
{
    private readonly List<VersaoDaFicha> _historico = new();

    public FichaCliente(Guid id, Guid leadId, DateTimeOffset criadaEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Ficha sem id.", nameof(id));
        if (leadId == Guid.Empty) throw new ArgumentException("Ficha sem lead.", nameof(leadId));

        Id = id;
        LeadId = leadId;
        CriadaEm = criadaEm;
        AtualizadaEm = criadaEm;

        Empresa = new SobreAEmpresa();
        Pessoa = new SobreAPessoa();
        Negocio = new SobreONegocio();
    }

    public Guid Id { get; }
    public Guid LeadId { get; }
    public DateTimeOffset CriadaEm { get; }
    public DateTimeOffset AtualizadaEm { get; private set; }

    public SobreAEmpresa Empresa { get; private set; }
    public SobreAPessoa Pessoa { get; private set; }
    public SobreONegocio Negocio { get; private set; }

    /// <summary>
    /// O que a ficha dizia antes de cada mudanca, do mais recente para o mais
    /// antigo. "Ele era o decisor e agora nao e" e informacao de venda, nao
    /// auditoria: a mudanca em si diz algo.
    /// </summary>
    public IReadOnlyList<VersaoDaFicha> Historico => _historico;

    /// <summary>Ficha que ninguem preencheu. O sistema funciona assim.</summary>
    public bool EstaVazia => Preenchidos.Count == 0;

    /// <summary>
    /// Atualiza, guardando a versao anterior. Passar null em um bloco mantem o
    /// que ja estava — a ficha e PROGRESSIVA, cresce de tres linhas para quinze
    /// sem exigir que alguem redigite o que ja sabia.
    /// </summary>
    public void Atualizar(
        DateTimeOffset quando,
        SobreAEmpresa? empresa = null,
        SobreAPessoa? pessoa = null,
        SobreONegocio? negocio = null)
    {
        if (empresa is null && pessoa is null && negocio is null) return;

        _historico.Insert(0, new VersaoDaFicha(AtualizadaEm, Empresa, Pessoa, Negocio));

        Empresa = empresa ?? Empresa;
        Pessoa = pessoa ?? Pessoa;
        Negocio = negocio ?? Negocio;
        AtualizadaEm = quando;
    }

    /// <summary>Os campos que tem conteudo, com o rotulo que vai ao contexto.</summary>
    public IReadOnlyDictionary<string, Anotacao> Preenchidos
    {
        get
        {
            var d = new Dictionary<string, Anotacao>();
            void Por(string rotulo, Anotacao? anotacao)
            {
                if (anotacao is not null) d[rotulo] = anotacao;
            }

            Por("Ramo", Empresa.Ramo);
            Por("Porte", Empresa.Porte);
            Por("Momento", Empresa.Momento);
            Por("Como chegou", Empresa.ComoChegou);
            Por("Cargo", Pessoa.Cargo);
            Por("Papel na decisão", Pessoa.PapelNaDecisao);
            Por("Quem mais decide", Pessoa.QuemMaisDecide);
            Por("Estilo observado", Pessoa.EstiloObservado);
            Por("Provável necessidade", Negocio.ProvavelNecessidade);
            Por("Usa hoje", Negocio.UsaHoje);
            Por("Orçamento estimado", Negocio.OrcamentoEstimado);
            Por("Risco conhecido", Negocio.RiscoConhecido);

            return d;
        }
    }

    /// <summary>
    /// O que ainda nao se sabe — os campos vazios, pelo rotulo.
    ///
    /// E o que fecha o ciclo da issue: a IA aponta a lacuna, o vendedor vai
    /// descobrir, preenche, e a lacuna some. Sem isso a ficha seria formulario;
    /// com isso o sistema para de pedir dado generico e passa a pedir o que
    /// falta para dar um conselho melhor.
    /// </summary>
    public IReadOnlyList<string> Lacunas() =>
        TodosOsRotulos.Where(r => !Preenchidos.ContainsKey(r)).ToList();

    /// <summary>O que foi apurado. E so daqui que sai afirmacao (#88).</summary>
    public IReadOnlyDictionary<string, Anotacao> Fatos =>
        Preenchidos.Where(p => p.Value.EhFato).ToDictionary(p => p.Key, p => p.Value);

    /// <summary>
    /// O que alguem achou. Sustenta hipotese, e nada alem disso.
    ///
    /// Nao e' ruido a descartar: "parece desconfiado" muda a abordagem do
    /// vendedor e tem lugar na ficha. O que nao pode e' virar premissa de uma
    /// conclusao que volta com a autoridade do sistema.
    /// </summary>
    public IReadOnlyDictionary<string, Anotacao> Impressoes =>
        Preenchidos.Where(p => !p.Value.EhFato).ToDictionary(p => p.Key, p => p.Value);

    private static readonly string[] TodosOsRotulos =
    [
        "Ramo", "Porte", "Momento", "Como chegou",
        "Cargo", "Papel na decisão", "Quem mais decide", "Estilo observado",
        "Provável necessidade", "Usa hoje", "Orçamento estimado", "Risco conhecido",
    ];
}
