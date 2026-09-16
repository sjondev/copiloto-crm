using System.Text.RegularExpressions;

namespace Copiloto.Dominio.Seguranca;

/// <summary>As categorias do art. 11 que aparecem em conversa de venda.</summary>
public enum CategoriaSensivel
{
    Saude = 0,
    ConviccaoReligiosa = 1,
    OrigemRacialOuEtnica = 2,
    OpiniaoPolitica = 3,
    FiliacaoSindical = 4,
    VidaSexual = 5,

    /// <summary>Genetico ou biometrico.</summary>
    CorpoIdentificavel = 6,
}

/// <summary>Onde o indicio apareceu, e de que categoria ele e.</summary>
public record IndicioSensivel(CategoriaSensivel Categoria, string Trecho);

/// <summary>
/// Dado sensivel que chega sozinho na conversa, e o regime proprio dele (#82).
///
/// Ninguem planeja coletar dado sensivel: conversa de WhatsApp e livre, e o
/// cliente conta da vida dele. "To com refluxo, posso tomar cafe?" e dado de
/// saude — art. 11, regime mais rigoroso, consentimento especifico e destacado
/// como regra.
///
/// O que mais importa aqui e eticamente: mencao de saude ou de fe NAO pode
/// calibrar tecnica de persuasao. Usar "nao bebo por questao de fe" para
/// escolher o angulo da venda e exatamente o uso que a lei trata com rigor, e
/// que destruiria a confianca do cliente se viesse a publico.
///
/// A garantia nao e uma instrucao no prompt: o trecho sensivel nao chega ao
/// modelo (<see cref="ForaDoContextoDeSugestao"/>) e nao entra no indice
/// (<see cref="PodeIndexar"/>). O que nao esta la nao calibra nada.
///
/// Mora no DOMINIO, e nao na borda, desde a #89: o que faz um texto ser dado
/// sensivel e regra da LGPD, nao detalhe de infraestrutura — e a Ficha do
/// Cliente precisa dessa regra no proprio construtor para o bloqueio ser
/// estrutural, em vez de depender de alguem lembrar de validar antes de gravar.
/// </summary>
public static class DadoSensivel
{
    /// <summary>
    /// Retencao mais curta que a da conversa comum.
    ///
    /// O numero e conservador de proposito e vai virar configuracao quando a
    /// politica por finalidade existir (ARQUITETURA secao 7). Enquanto isso,
    /// vale o principio: dado que ninguem pediu, e que a empresa nao tem
    /// finalidade para usar, nao pode ficar pelo prazo do que ela pediu.
    /// </summary>
    public static readonly TimeSpan Retencao = TimeSpan.FromDays(30);

    // Os padroes sao amplos, pelo mesmo motivo do `DetectorDePii`: falso
    // positivo custa um trecho a menos no contexto; falso negativo custa dado
    // de saude do cliente indexado num banco vetorial.
    //
    // O que ficou DE FORA, e por que — e a parte que erra caro:
    //
    //   "preto"   — "cafe preto" e a frase mais provavel deste produto
    //   "direita" — "a prateleira da direita"
    //   "digital" — "marketing digital"
    //   "fe"      — dentro de "cafe" nao casa com \b, mas "fe" solto tambem
    //               aparece em "fe em voces"; so entra com "questao de fe"
    //
    // Um detector que marca metade das conversas de uma cafeteria como
    // sensiveis nao protege ninguem: ele e desligado na primeira semana.
    private static readonly (CategoriaSensivel Categoria, Regex Padrao)[] Indicios =
    [
        (CategoriaSensivel.Saude, new Regex(
            @"\b(refluxo|gastrite|[úu]lcera|diabet\w*|hipertens\w*|press[ãa]o alta|"
            + @"colesterol|ins[ôo]nia|ansiedade|depress[ãa]o|gr[áa]vid\w*|gesta[çc][ãa]o|"
            + @"amamenta\w*|rem[ée]dio|medicamento|al[ée]rgic\w*|alergia|intoler[âa]ncia|"
            + @"cirurgia|quimioterapia|doen[çc]a|diagn[óo]stico|laudo m[ée]dico)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.ConviccaoReligiosa, new Regex(
            @"\b(quest[ãa]o de f[ée]|religi[ãa]o|religios\w*|evang[ée]lic\w*|cat[óo]lic\w*|"
            + @"esp[íi]rita|umbanda|candombl[ée]|judeu|judaica|kosher|halal|"
            + @"mu[çc]ulman\w*|igreja|jejum|quaresma|ramad[ãa])\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.OrigemRacialOuEtnica, new Regex(
            @"\b(pessoa negra|pessoas negras|afrodescendente|afro-brasileir\w*|"
            + @"ind[íi]gena|quilombola|etnia)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.OpiniaoPolitica, new Regex(
            @"\b(votei|voto n\w+|candidat\w+|partido|elei[çc][ãa]o|elei[çc][õo]es)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.FiliacaoSindical, new Regex(
            @"\b(sindicato|sindicaliz\w*|sindical)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.VidaSexual, new Regex(
            @"\b(orienta[çc][ãa]o sexual|homossexual|bissexual|LGBT\w*|transexual|transg[êe]nero)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),

        (CategoriaSensivel.CorpoIdentificavel, new Regex(
            @"\b(exame de DNA|DNA|biometria|biom[ée]tric\w*|impress[ãa]o digital|"
            + @"reconhecimento facial)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    /// <summary>O que na fala indica dado sensivel. Vazio e o caso comum.</summary>
    public static IReadOnlyList<IndicioSensivel> Detectar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return [];

        var achados = new List<IndicioSensivel>();
        foreach (var (categoria, padrao) in Indicios)
            foreach (Match m in padrao.Matches(texto))
                achados.Add(new IndicioSensivel(categoria, m.Value));

        return achados;
    }

    /// <summary>
    /// Se a fala pode ir para o indice de embeddings (#62).
    ///
    /// Indice e o pior destino possivel para dado sensivel: ele deixa de estar
    /// numa conversa e passa a ser RECUPERAVEL por semelhanca, aparecendo em
    /// analise de outro cliente sem que ninguem tenha pedido.
    /// </summary>
    public static bool PodeIndexar(string? texto) => Detectar(texto).Count == 0;

    /// <summary>
    /// A fala como ela vai ao modelo: o trecho sensivel sai, o resto fica.
    ///
    /// Descartar a fala INTEIRA seria perder a venda junto com o dado — em "to
    /// com refluxo, posso tomar cafe?" o pedido esta na segunda metade. O
    /// marcador tambem diz ao modelo que ali havia algo que ele nao vai ver, o
    /// que evita a leitura de que a frase estava truncada.
    /// </summary>
    public static string ForaDoContextoDeSugestao(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? "";

        var saida = texto;
        foreach (var (categoria, padrao) in Indicios)
            saida = padrao.Replace(saida, $"[SENSIVEL:{categoria}]");

        return saida;
    }

    /// <summary>Passou do prazo proprio, mais curto que o da conversa.</summary>
    public static bool DeveExpurgar(DateTimeOffset registradaEm, DateTimeOffset agora) =>
        agora - registradaEm >= Retencao;
}
