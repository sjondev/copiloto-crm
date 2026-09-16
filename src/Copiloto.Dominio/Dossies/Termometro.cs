namespace Copiloto.Dominio.Dossies;

/// <summary>Quao perto de comprar o cliente parece estar.</summary>
public enum Temperatura { Fria = 0, Morna = 1, Quente = 2 }

/// <summary>Para onde ela esta indo.</summary>
public enum Direcao { Esfriando = -1, Estavel = 0, Esquentando = 1 }

/// <summary>De onde veio o sinal: o cliente se aproximando, ou se afastando.</summary>
public enum TipoDeSinal
{
    /// <summary>Preco, prazo, frete, forma de pagamento — pergunta de quem esta decidindo.</summary>
    Compra = 0,

    /// <summary>Silencio, resposta monossilabica, adiamento.</summary>
    Fuga = 1,
}

/// <summary>
/// A temperatura da venda, com direcao (#13).
///
/// Valor sozinho nao serve: "morno" descreve tanto o cliente que estava frio e
/// esquentou quanto o que estava quente e esfriou — e os dois pedem coisas
/// opostas do vendedor. O primeiro quer que ele avance; o segundo quer que ele
/// descubra o que mudou, e avancar ali termina a conversa.
/// </summary>
public record Termometro(Temperatura Valor, Direcao Para)
{
    /// <summary>O que aparece na tela: "mornando", "morno estavel", "esfriando".</summary>
    public string Resumo => Para switch
    {
        Direcao.Esquentando => $"{Valor} e esquentando".ToLowerInvariant(),
        Direcao.Esfriando => $"{Valor} e esfriando".ToLowerInvariant(),
        _ => $"{Valor} estavel".ToLowerInvariant(),
    };
}
