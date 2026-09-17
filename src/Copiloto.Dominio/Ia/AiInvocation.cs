namespace Copiloto.Dominio.Ia;

/// <summary>
/// O que se mediu na chamada (#1).
///
/// Vem agrupado, e nao como seis parametros soltos: o gatilho do CLAUDE.md e
/// quatro, e — mais que isso — estes campos so fazem sentido JUNTOS. Tokens sem
/// modelo nao viram custo, e custo sem tentativas nao diz se a cascata desceu.
/// </summary>
/// <param name="Tentativas">
/// Quantos degraus foram tentados, contando o que respondeu. Retentativa NAO
/// gera linha nova: uma chamada logica e uma linha, e quantas vezes ela precisou
/// insistir e um campo dela. Duas linhas por uma chamada fariam o total de
/// invocacoes medir a instabilidade do provedor em vez do uso do produto.
/// </param>
/// <param name="Sucesso">
/// Falha TAMBEM vira linha. Contar so o que deu certo esconde o custo do que
/// nao deu — e o provedor cobra pelo token que gastou antes de falhar.
/// </param>
public record MedicaoDaChamada(
    string Modelo,
    int TokensEntrada,
    int TokensSaida,
    int LatenciaMs,
    int Tentativas,
    bool Sucesso,
    decimal CustoEmReais)
{
    public int TokensTotais => TokensEntrada + TokensSaida;
}

/// <summary>
/// O que sustentou a resposta, para o "por que essa sugestao?" (#51).
///
/// Separado da medicao porque responde a outra pergunta. A medicao responde
/// QUANTO custou; isto responde COM BASE EM QUE — e e a segunda que constroi
/// confianca no vendedor, e a que responde "como voce sabe que a IA nao esta
/// inventando?".
/// </summary>
/// <param name="ContextoEnviado">
/// O texto que REALMENTE foi ao modelo, ja mascarado pelo escudo (#43). Guardar
/// o que foi montado depois nao serviria: a pergunta e o que ele viu naquele
/// momento, e nao o que veria hoje.
/// </param>
/// <param name="VersaoDoPrompt">
/// Hash curto do conteudo das instrucoes. Nao e um numero que alguem incrementa
/// e esquece: ele muda exatamente quando o prompt muda, e responde "esta
/// sugestao saiu do mesmo prompt daquela?" sem depender de disciplina. A #36
/// pode trocar isto por versao em arquivo sem quebrar nada.
/// </param>
public record ProcedenciaDaChamada(string? ContextoEnviado, string? VersaoDoPrompt);

/// <summary>
/// Uma chamada a um modelo: qual, quanto custou, e quanto demorou.
///
/// Guardar o NOME do modelo junto do custo e o que permite a comparacao entre
/// provedores depois — sem ele, o total responde "gastamos X" e nao "gastamos X
/// com este e Y com aquele", que e a pergunta que decide a troca.
///
/// Imutavel no que foi MEDIDO — e um registro do que ja aconteceu. A unica
/// coisa que muda depois e o aceite: a sugestao sai, o vendedor decide minutos
/// depois, e essa decisao tambem faz parte do que aconteceu com esta chamada.
/// Guarda-la em outro lugar obrigaria um join para responder "qual modelo
/// acerta mais", que e a pergunta que ela existe para responder (#3, #11).
/// </summary>
public class AiInvocation
{
    /// <param name="dealId">
    /// O negocio em que a invocacao aconteceu. Nulo so quando nao ha negocio no
    /// contexto — uma chamada de diagnostico, um teste de provedor. Havendo
    /// Deal, ele e obrigatorio, e quem cobra isso e o proprio Deal ao registrar.
    /// </param>
    public AiInvocation(
        Guid id, Tarefa agente, MedicaoDaChamada medicao, DateTimeOffset quando,
        Guid? dealId = null, string? correlationId = null,
        ProcedenciaDaChamada? procedencia = null)
    {
        ArgumentNullException.ThrowIfNull(medicao);

        if (id == Guid.Empty) throw new ArgumentException("Invocacao sem id.", nameof(id));
        if (string.IsNullOrWhiteSpace(medicao.Modelo))
            throw new ArgumentException(
                "Invocacao sem modelo: o custo sem o nome do modelo nao responde "
                + "se vale trocar de provedor.", nameof(medicao));
        if (medicao.CustoEmReais < 0)
            throw new ArgumentOutOfRangeException(nameof(medicao),
                "Custo negativo nao existe, e somado ao acumulado ele o faria DIMINUIR.");
        if (medicao.Tentativas < 1)
            throw new ArgumentOutOfRangeException(nameof(medicao),
                "Toda invocacao teve ao menos uma tentativa — inclusive a que falhou.");

        if (dealId == Guid.Empty)
            throw new ArgumentException(
                "Guid.Empty nao e 'sem negocio': use null. Empty passaria por "
                + "preenchido e o custo seria somado a um Deal que nao existe.",
                nameof(dealId));

        Id = id;
        DealId = dealId;
        Agente = agente;
        Modelo = medicao.Modelo.Trim();
        TokensEntrada = medicao.TokensEntrada;
        TokensSaida = medicao.TokensSaida;
        LatenciaMs = medicao.LatenciaMs;
        Tentativas = medicao.Tentativas;
        Sucesso = medicao.Sucesso;
        CustoEmReais = medicao.CustoEmReais;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        ContextoEnviado = procedencia?.ContextoEnviado;
        VersaoDoPrompt = procedencia?.VersaoDoPrompt;
        Quando = quando;
    }

    /// <summary>Construtor do EF. Ele materializa por propriedade, nao por medicao.</summary>
    private AiInvocation()
    {
        Modelo = "";
    }

    public Guid Id { get; }

    /// <summary>
    /// O vinculo custo-negocio. E barato agora e caro depois: enxertar rastreio
    /// num modelo ja povoado exige backfill e adivinhacao, e a resposta ficaria
    /// sendo estimativa para sempre — justamente na conta que decide se o
    /// produto se paga.
    /// </summary>
    public Guid? DealId { get; }

    /// <summary>Qual agente gastou. Sem isto o total nao diz ONDE cortar.</summary>
    public Tarefa Agente { get; }

    public string Modelo { get; }
    public int TokensEntrada { get; }
    public int TokensSaida { get; }
    public int TokensTotais => TokensEntrada + TokensSaida;

    /// <summary>Quanto o vendedor esperou. E o que decide se vale trocar por um modelo pior e mais rapido.</summary>
    public int LatenciaMs { get; }

    /// <summary>Degraus tentados, contando o que respondeu.</summary>
    public int Tentativas { get; }

    /// <summary>
    /// Falsa quando a cascata inteira se esgotou. A linha existe do mesmo jeito:
    /// o provedor cobra pelo token gasto antes de falhar, e um ledger que so
    /// conta acerto esconde exatamente o custo que ninguem esperava ter.
    /// </summary>
    public bool Sucesso { get; }

    public decimal CustoEmReais { get; }

    /// <summary>
    /// O fio que liga esta chamada a fala que a originou (#42). Nulo enquanto o
    /// correlation-id ponta a ponta nao existir — e o campo nasce agora porque
    /// enxertar rastreio em tabela ja povoada exige backfill e adivinhacao.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// O contexto que foi ao modelo, ja mascarado (#51).
    ///
    /// Nulo nas chamadas antigas e nas que nao guardam — e ele E dado pessoal,
    /// mesmo pseudonimizado (#83). A retencao dele segue a da conversa (#45).
    /// </summary>
    public string? ContextoEnviado { get; }

    /// <summary>Hash curto do prompt que gerou a resposta.</summary>
    public string? VersaoDoPrompt { get; }

    /// <summary>
    /// Se a sugestao desta chamada foi usada pelo vendedor.
    ///
    /// Nulo quando ele ainda nao decidiu — e nulo tambem nas chamadas que nao
    /// produzem sugestao, como a leitura da conversa. Nulo NAO e "ignorou": a
    /// taxa de aceite que contasse indecisao como recusa mediria a velocidade do
    /// vendedor, e nao a qualidade do modelo.
    /// </summary>
    public bool? SugestaoAceita { get; private set; }

    /// <summary>O vendedor decidiu. Primeira decisao vale; ele nao desfaz para refazer a estatistica.</summary>
    public void RegistrarAceite(bool aceita)
    {
        SugestaoAceita ??= aceita;
    }

    public DateTimeOffset Quando { get; }
}
