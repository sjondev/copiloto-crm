namespace Copiloto.Dominio.Planos;

/// <summary>
/// Os quatro blocos do plano (#12).
///
/// Sao quatro e nao um campo livre porque a pergunta muda o que o vendedor
/// escreve: "anote sobre a conversa" produz resumo do passado, e "o que voce
/// quer que aconteca nesta conversa" produz plano. O formulario e o conselho.
/// </summary>
public enum BlocoDoPlano
{
    /// <summary>O que eu quero que aconteca nesta conversa.</summary>
    Objetivo = 0,

    /// <summary>O que preciso descobrir antes de propor.</summary>
    PrecisoDescobrir = 1,

    /// <summary>A resistencia que eu espero encontrar.</summary>
    ObjecaoProvavel = 2,

    /// <summary>Como esta conversa termina.</summary>
    ProximoPasso = 3,
}

/// <summary>
/// Um bloco: o que o vendedor escreveu, e — em campo SEPARADO — o que a IA
/// sugeriu.
///
/// Separados de proposito (#12). Sugestao que cai dentro do texto do vendedor
/// apaga o que ele pensou e transforma "aceitar, editar ou ignorar" em
/// "desfazer". O texto dele nunca e sobrescrito sem que ele mande.
/// </summary>
/// <param name="InvocacaoId">
/// A chamada que gerou a sugestao pendente (#51). E o fio que o "por que essa
/// sugestao?" segue para achar contexto, modelo, custo e versao do prompt — sem
/// ele, o botao teria de adivinhar qual chamada produziu qual frase, e erraria
/// assim que houvesse duas no mesmo negocio.
/// </param>
public record Bloco(string Texto = "", string? Sugestao = null, Guid? InvocacaoId = null)
{
    public bool TemSugestaoPendente => !string.IsNullOrWhiteSpace(Sugestao);
}

/// <summary>
/// O plano de abordagem daquele negocio (#12).
///
/// E a tela onde o vendedor e o protagonista, e o criterio que prova isso e o
/// ultimo da issue: FUNCIONA SEM NUNCA CLICAR EM SUGERIR. A IA e insumo, e
/// insumo que nao chega nao impede o trabalho — e por isso nada aqui exige
/// sugestao para salvar, versionar ou ler.
/// </summary>
public class PlanoDeAbordagem
{
    private readonly Dictionary<BlocoDoPlano, Bloco> _blocos;

    public PlanoDeAbordagem(Guid id, Guid dealId, DateTimeOffset criadoEm)
    {
        if (id == Guid.Empty) throw new ArgumentException("Plano sem id.", nameof(id));
        if (dealId == Guid.Empty)
            throw new ArgumentException(
                "Plano sem negocio nao tem a que se referir.", nameof(dealId));

        Id = id;
        DealId = dealId;
        CriadoEm = criadoEm;
        AtualizadoEm = criadoEm;

        _blocos = Enum.GetValues<BlocoDoPlano>().ToDictionary(b => b, _ => new Bloco());
    }

    public Guid Id { get; }
    public Guid DealId { get; }
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    /// <summary>
    /// Quantas vezes o VENDEDOR mexeu. Comeca em 1: o plano em branco ja e uma
    /// versao, e nao um plano inexistente.
    ///
    /// Sugestao da IA nao incrementa. A versao existe para responder "o que ele
    /// tinha escrito quando falou com o cliente", e uma sugestao que ele ignorou
    /// nunca fez parte do plano.
    /// </summary>
    public int Versao { get; private set; } = 1;

    public Bloco this[BlocoDoPlano qual] => _blocos[qual];

    public IReadOnlyDictionary<BlocoDoPlano, Bloco> Blocos => _blocos;

    /// <summary>Plano ainda em branco, em qualquer bloco.</summary>
    public bool EstaVazio => _blocos.Values.All(b => string.IsNullOrWhiteSpace(b.Texto));

    /// <summary>
    /// O vendedor escreve. Texto igual ao que ja estava NAO gera versao: salvar
    /// automatico a cada tecla criaria um historico de centenas de versoes
    /// identicas, e a versao deixaria de significar "ele mudou de ideia".
    /// </summary>
    public void Escrever(BlocoDoPlano qual, string? texto, DateTimeOffset quando)
    {
        var limpo = (texto ?? "").Trim();
        var atual = _blocos[qual];

        if (string.Equals(atual.Texto, limpo, StringComparison.Ordinal)) return;

        _blocos[qual] = atual with { Texto = limpo };
        Versao++;
        AtualizadoEm = quando;
    }

    /// <summary>
    /// A IA propoe, em campo separado. NAO mexe no que o vendedor escreveu e
    /// NAO conta versao.
    /// </summary>
    public void Sugerir(BlocoDoPlano qual, string? sugestao, DateTimeOffset quando)
    {
        var limpa = (sugestao ?? "").Trim();

        _blocos[qual] = _blocos[qual] with
        {
            Sugestao = string.IsNullOrWhiteSpace(limpa) ? null : limpa,

            // Sugestao nova zera o vinculo: ele so volta quando quem chamou
            // disser de qual invocacao ela veio.
            InvocacaoId = null,
        };

        AtualizadoEm = quando;
    }

    /// <summary>Liga a sugestao pendente a chamada que a produziu (#51).</summary>
    public void Vincular(BlocoDoPlano qual, Guid invocacaoId)
    {
        if (!_blocos[qual].TemSugestaoPendente) return;

        _blocos[qual] = _blocos[qual] with { InvocacaoId = invocacaoId };
    }

    /// <summary>
    /// O vendedor aceita a sugestao: ela vira o texto DELE e some do campo de
    /// sugestao. A partir daqui o plano nao lembra de onde a frase veio — e
    /// isso e decisao: o plano e dele, e marcar "isto foi a IA" criaria duas
    /// categorias de frase numa tela que existe para ter uma.
    /// </summary>
    public void Aceitar(BlocoDoPlano qual, DateTimeOffset quando)
    {
        var bloco = _blocos[qual];
        if (!bloco.TemSugestaoPendente) return;

        // A procedencia sai junto: a frase virou TEXTO DELE. Guardar "isto veio
        // da IA" criaria duas categorias de frase numa tela que existe para ter
        // uma — e o plano e dele.
        _blocos[qual] = new Bloco(bloco.Sugestao!);
        Versao++;
        AtualizadoEm = quando;
    }

    /// <summary>
    /// Ignorar. A sugestao some sem tocar no texto — e sem virar versao, porque
    /// nada do plano mudou.
    /// </summary>
    public void Descartar(BlocoDoPlano qual, DateTimeOffset quando)
    {
        if (!_blocos[qual].TemSugestaoPendente) return;

        _blocos[qual] = _blocos[qual] with { Sugestao = null, InvocacaoId = null };
        AtualizadoEm = quando;
    }
}
