namespace Copiloto.Dominio.Fichas;

/// <summary>
/// O que ainda nao se sabe, com a pergunta que descobre.
///
/// O rotulo sozinho nao e acionavel: "Papel na decisao" descreve um campo de
/// formulario, e o vendedor esta no meio de uma conversa. A pergunta e o que ele
/// pode usar sem traduzir nada.
/// </summary>
/// <param name="ApenasImpressao">
/// O slot tem conteudo, mas so impressao. Continua aberto para efeito de
/// conselho: impressao nao ancora (#88), entao "acho que quem decide e ele"
/// ainda nao sustenta uma fala. A pergunta muda de DESCOBRIR para CONFIRMAR.
/// </param>
public record Lacuna(string Rotulo, string Pergunta, bool ApenasImpressao);

/// <summary>
/// O quadrante mais util do dossie (#8), e o unico que nao precisa de modelo.
///
/// Ausencia de informacao e comparacao de conjunto, nao inferencia: o slot esta
/// preenchido ou nao esta. Pagar token para um modelo responder isso seria
/// pagar para que ele erre onde o `if` acerta — e um modelo pode inventar que
/// falta o que esta la, ou calar sobre o que falta. E' o mesmo argumento que o
/// `Dossie.DetectarObjecoes` ja usa: roda sem modelo, e por isso nao alucina.
///
/// A lacuna livre que o agente de leitura escreve continua existindo. Esta aqui
/// nao a substitui: cobre o que a ficha sabe cobrar, e o modelo cobre o que so
/// aparece lendo a conversa.
/// </summary>
public static class Lacunas
{
    /// <summary>
    /// Quantas cabem antes de a lista virar ruido.
    ///
    /// O Storybook da #170 mostrou o efeito no estado `CheioDemais`: passado
    /// certo ponto, as lacunas caem abaixo da dobra — e sao justamente a parte
    /// mais util do dossie. Doze perguntas de uma vez tambem nao e uma conversa,
    /// e um interrogatorio.
    /// </summary>
    public const int Maximo = 5;

    /// <summary>
    /// A pergunta de cada slot, na ORDEM em que mudam a venda.
    ///
    /// Quem decide vem primeiro porque e o unico que invalida todo o resto:
    /// descobrir orcamento, prazo e necessidade com quem nao assina e trabalho
    /// que recomeca do zero quando o decisor aparece.
    ///
    /// Orcamento antes de necessidade porque a necessidade quase sempre ja
    /// apareceu na conversa — quem escreve para uma torrefacao quer cafe —,
    /// enquanto o quanto ele pode gastar raramente aparece sem alguem perguntar.
    ///
    /// O TERMO de cada slot identifica o assunto, para nao repetir uma pergunta
    /// que o agente de leitura ja fez com outras palavras.
    ///
    /// E heuristica, e o limite esta declarado: casa por texto, entao erra em
    /// dois sentidos. Cala uma pergunta legitima quando a palavra aparece noutro
    /// contexto ("decidimos fechar"), e deixa passar a duplicata escrita sem o
    /// radical. Preferir o primeiro erro e escolha: lacuna a menos e informacao
    /// perdida; lacuna repetida e o vendedor aprendendo que a lista tem enchimento.
    ///
    /// A alternativa honesta seria o agente devolver o SLOT junto da lacuna, e
    /// nao texto livre. Isso muda o contrato dele, e fica para quando houver
    /// motivo alem deste.
    /// </summary>
    private static readonly (string Rotulo, string Pergunta, string Termo)[] PorPrioridade =
    [
        ("Papel na decisão", "Ele decide a compra, ou precisa levar para alguém?", "decid"),
        ("Quem mais decide", "Quem mais participa dessa decisão?", "decid"),
        ("Orçamento estimado", "Quanto ele pretende investir por mês?", "orçament"),
        ("Usa hoje", "O que ele usa hoje, e de quem compra?", "usa hoje"),
        ("Provável necessidade", "Para que ele quer o produto — revenda, consumo, presente?", "para que"),
        ("Porte", "Qual o tamanho da operação dele?", "tamanho"),
        ("Momento", "O que mudou agora para ele ir atrás disso?", "mudou"),
        ("Risco conhecido", "O que pode fazer essa venda não acontecer?", "risco"),
        ("Ramo", "Em que ramo ele atua?", "ramo"),
        ("Cargo", "Qual o cargo dele?", "cargo"),
        ("Como chegou", "Como ele chegou até a empresa?", "chegou"),
        ("Estilo observado", "Como ele prefere ser atendido?", "atendid"),
    ];

    /// <summary>
    /// As lacunas daquela ficha, da mais util para a menos, cortadas no
    /// <see cref="Maximo"/>.
    ///
    /// Ficha inexistente devolve as primeiras perguntas, e nao lista vazia: o
    /// lead sem ficha nenhuma e o caso em que MAIS se desconhece, e devolver
    /// nada ali diria ao vendedor que nao falta nada.
    /// </summary>
    /// <param name="jaDitas">
    /// As lacunas que o agente de leitura ja escreveu. Sem isto o dossie mostra
    /// "Quem decide a compra e ele mesmo?" do agente e "Ele decide a compra, ou
    /// precisa levar para alguem?" da ficha, uma embaixo da outra — observado
    /// rodando, e nao previsto.
    /// </param>
    public static IReadOnlyList<Lacuna> De(
        FichaCliente? ficha, IEnumerable<string>? jaDitas = null)
    {
        var fatos = ficha?.Fatos;
        var impressoes = ficha?.Impressoes;
        var ditas = (jaDitas ?? []).Select(d => d.ToLowerInvariant()).ToList();

        return PorPrioridade
            .Where(slot => fatos is null || !fatos.ContainsKey(slot.Rotulo))
            .Where(slot => !ditas.Any(d => d.Contains(slot.Termo, StringComparison.Ordinal)))
            .Select(slot =>
            {
                var soImpressao = impressoes?.ContainsKey(slot.Rotulo) == true;

                return new Lacuna(
                    slot.Rotulo,
                    soImpressao ? Confirmar(slot.Rotulo, impressoes!) : slot.Pergunta,
                    soImpressao);
            })
            .Take(Maximo)
            .ToList();
    }

    /// <summary>
    /// A pergunta de quem ja tem um palpite: ela cita o palpite.
    ///
    /// "Confirmar se ele decide" e vago; "voce anotou que ele parece decidir
    /// sozinho — confere?" e uma pergunta que o vendedor faz sem pensar. E a
    /// mesma razao da citacao no sinal do dossie: o vendedor discorda de algo
    /// concreto, em vez de aceitar ou ignorar.
    /// </summary>
    private static string Confirmar(
        string rotulo, IReadOnlyDictionary<string, Anotacao> impressoes) =>
        $"Você anotou \"{impressoes[rotulo].Valor}\" como impressão. Dá para confirmar?";
}
