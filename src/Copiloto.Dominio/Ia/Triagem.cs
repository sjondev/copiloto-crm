using Copiloto.Dominio.Conversas;

namespace Copiloto.Dominio.Ia;

public enum Veredito
{
    /// <summary>Vale acordar o modelo de leitura.</summary>
    Analisar = 0,

    /// <summary>Ruido social: nao muda o dossie.</summary>
    Descartar = 1,
}

/// <param name="PorHeuristica">
/// Decidido sem gastar modelo nenhum. E o que separa "economizamos o modelo
/// caro" de "economizamos tudo" no calculo do ledger.
/// </param>
public record Triagem(Veredito Veredito, string Motivo, bool PorHeuristica)
{
    public bool Analisa => Veredito == Veredito.Analisar;
}

/// <summary>
/// O A0 sem modelo nenhum (#37).
///
/// Em conversa de WhatsApp boa parte do trafego e ruido social, e filtrar antes
/// do modelo caro e economia de multiplo direto. Mas o filtro erra caro: fala
/// descartada nao entra no dossie, e o vendedor nunca fica sabendo que ela
/// existiu.
///
/// A REGRA QUE TORNA ISSO SEGURO: so descarta o que e ruido E nao responde a
/// nada. "ok" isolado no meio do dia e ruido; "ok" logo depois de o vendedor
/// mandar uma proposta e a OBJECAO VELADA que a #13 existe para captar —
/// resposta monossilabica depois de mensagem longa e sinal de fuga, e joga-la
/// fora seria economizar centavos destruindo o produto.
/// </summary>
public static class TriagemPorHeuristica
{
    /// <summary>
    /// Falas que nao carregam informacao sozinhas. Comparadas ja sem acento,
    /// sem pontuacao e em minuscula.
    /// </summary>
    private static readonly HashSet<string> Ruido = new(StringComparer.Ordinal)
    {
        "ok", "okay", "blz", "beleza", "certo", "ta", "tah", "ta bom", "tudo bem",
        "obrigado", "obrigada", "obg", "vlw", "valeu", "agradeco",
        "bom dia", "boa tarde", "boa noite", "oi", "ola", "opa", "eai", "e ai",
        "boa", "legal", "otimo", "perfeito", "show", "massa",
        "de nada", "imagina", "disponha", "ate mais", "ate logo", "tchau", "abraco",
    };

    /// <summary>
    /// Quanto tempo depois de uma fala do vendedor uma resposta curta ainda
    /// CONTA como resposta.
    ///
    /// Passado isso, "ok" deixa de responder a proposta e vira ruido solto. O
    /// numero e generoso de proposito: errar para o lado de analisar custa uma
    /// chamada; errar para o lado de descartar custa um sinal de fuga.
    /// </summary>
    public static readonly TimeSpan JanelaDeResposta = TimeSpan.FromHours(6);

    /// <summary>
    /// O veredito quando ele e OBVIO, ou <c>null</c> quando so o modelo decide.
    ///
    /// Devolver null e a resposta honesta: forcar um veredito aqui faria a
    /// heuristica opinar sobre a fala que ela nao entende, que e justamente
    /// onde o triador barato existe para entrar.
    /// </summary>
    public static Triagem? Avaliar(Mensagem fala, Mensagem? anterior)
    {
        ArgumentNullException.ThrowIfNull(fala);

        // Midia nunca e descartada, mesmo sem texto. O dossie precisa declarar
        // a lacuna (#23), e para declarar ele precisa ser regerado.
        if (fala.NaoInterpretada)
            return new Triagem(Veredito.Analisar, "midia nao interpretada abre lacuna", true);

        if (!EhRuido(fala.Texto)) return null;

        // Ruido do VENDEDOR e sempre ruido: "bom dia" dito por quem vende nao
        // conta nada sobre o cliente, que e o unico assunto do dossie.
        if (fala.Autor == Autor.Vendedor)
            return new Triagem(Veredito.Descartar, "cortesia do vendedor", true);

        // Ruido do CLIENTE logo depois de o vendedor falar nao e ruido: e
        // resposta curta a algo, e resposta curta e sinal de fuga (#13).
        if (RespondeAoVendedor(fala, anterior))
            return new Triagem(
                Veredito.Analisar, "resposta curta ao vendedor pode ser objecao velada", true);

        return new Triagem(Veredito.Descartar, "cortesia solta do cliente", true);
    }

    private static bool RespondeAoVendedor(Mensagem fala, Mensagem? anterior) =>
        anterior is { Autor: Autor.Vendedor }
        && fala.EnviadaEm - anterior.EnviadaEm <= JanelaDeResposta;

    private static bool EhRuido(string texto)
    {
        var limpo = Achatar(texto);
        if (limpo.Length == 0) return true;

        // So emoji e figurinha: sobra nada depois de tirar letra e numero.
        if (!limpo.Any(char.IsLetterOrDigit)) return true;

        return Ruido.Contains(limpo);
    }

    /// <summary>Sem acento, sem pontuacao, sem caixa — "Obrigado!!" e "obrigado".</summary>
    private static string Achatar(string texto)
    {
        var normalizado = texto.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);

        var letras = normalizado
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c));

        return string.Join(' ', new string(letras.ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
