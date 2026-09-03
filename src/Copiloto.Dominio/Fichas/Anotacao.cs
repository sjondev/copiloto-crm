namespace Copiloto.Dominio.Fichas;

/// <summary>
/// De onde a informacao veio, e quanto peso ela aguenta (#88).
///
/// "E gerente de compras" e "parece desconfiado" sao coisas de naturezas
/// diferentes, e entrar no contexto com o mesmo peso e o risco mais silencioso
/// da ficha: o palpite do vendedor volta para ele reembalado como analise do
/// sistema, o que CONFIRMA o vies original em vez de corrigi-lo.
/// </summary>
public enum NaturezaDaInformacao
{
    /// <summary>Apurado. Sustenta afirmacao.</summary>
    Fato = 0,

    /// <summary>Percebido por alguem. Sustenta, no maximo, hipotese.</summary>
    Impressao = 1,
}

/// <summary>
/// Uma linha da ficha: o que se sabe, se e fato ou impressao, e de onde saiu.
///
/// Nao ha conversao implicita de string, e isso e decisao: `Ramo = "cafeteria"`
/// compilando viraria fato por omissao, e "parece apressado" entraria como dado
/// apurado sem ninguem escolher isso. Quem anota diz qual das duas coisas esta
/// anotando — e o custo de digitar `Anotacao.Fato(...)` e exatamente o momento
/// de pensar na diferenca.
/// </summary>
public record Anotacao
{
    private Anotacao(string valor, NaturezaDaInformacao natureza, string? fonte, DateTimeOffset? quando)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("Anotacao sem conteudo.", nameof(valor));

        Valor = valor.Trim();
        Natureza = natureza;
        Fonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
        Quando = quando;
    }

    public string Valor { get; init; }
    public NaturezaDaInformacao Natureza { get; init; }

    /// <summary>
    /// Site, LinkedIn, o proprio cliente disse, um terceiro contou. Opcional:
    /// exigir fonte faria o vendedor inventar uma para salvar o formulario, e
    /// fonte inventada e pior que fonte ausente — ela parece verificavel.
    /// </summary>
    public string? Fonte { get; init; }

    /// <summary>
    /// Quando foi anotado. Serve a impressao mais que ao fato: "me pareceu
    /// apressado" de tres semanas atras e outra coisa de "me pareceu apressado"
    /// ontem, e e' isso que da ao vendedor a chance de discordar.
    /// </summary>
    public DateTimeOffset? Quando { get; init; }

    public static Anotacao Fato(string valor, string? fonte = null, DateTimeOffset? quando = null) =>
        new(valor, NaturezaDaInformacao.Fato, fonte, quando);

    /// <summary>
    /// Impressao nao aceita fonte: a fonte E quem anotou, e escrever "site" ao
    /// lado de um palpite e o disfarce que a issue existe para impedir.
    /// </summary>
    public static Anotacao Impressao(string valor, DateTimeOffset? quando = null) =>
        new(valor, NaturezaDaInformacao.Impressao, fonte: null, quando);

    public bool EhFato => Natureza == NaturezaDaInformacao.Fato;

    /// <summary>
    /// O que a ficha manda para o contexto e para a tela. A procedencia vai
    /// JUNTO do valor, e nao numa coluna ao lado: o modelo le uma linha de
    /// texto, e separar valor de rotulo e' como a impressao vira fato no
    /// caminho.
    /// </summary>
    public string Rotulado()
    {
        var procedencia = (EhFato, Fonte, Quando) switch
        {
            (true, null, _) => "fato",
            (true, var f, _) => $"fato, {f}",
            (false, _, null) => "impressão do vendedor",
            (false, _, var q) => $"impressão do vendedor em {q:dd/MM/yyyy}",
        };

        return $"{Valor} [{procedencia}]";
    }
}
