namespace Copiloto.Dominio.Conversas;

/// <summary>O que veio na fala alem de texto, e que a Fase 1 nao interpreta.</summary>
public enum TipoDeMidia
{
    Audio = 0,
    Imagem = 1,
    Documento = 2,

    /// <summary>
    /// Video, figurinha, localizacao, contato — o que o provedor mandar e nao
    /// for um dos tres acima.
    ///
    /// Existe para que midia desconhecida vire lacuna em vez de sumir: o
    /// default de um enum e o primeiro valor, e cair em <c>Audio</c> por
    /// omissao diria ao vendedor que existe audio onde nao existe.
    /// </summary>
    Outro = 99,
}

/// <summary>
/// A midia anexada a uma fala (#23).
///
/// Na Fase 1 nada disso e transcrito nem interpretado, e a decisao e economica
/// antes de ser tecnica: registrar a lacuna e mais honesto e muito mais barato
/// que transcrever. O que nao pode e sumir — dossie que le uma conversa pela
/// metade sem saber que era pela metade tira conclusao errada com confianca
/// inteira.
/// </summary>
public record Midia(TipoDeMidia Tipo, TimeSpan? Duracao = null)
{
    /// <summary>
    /// O que aparece na conversa no lugar do conteudo, quando o provedor nao
    /// mandou texto nenhum.
    ///
    /// Diz o que NAO foi feito, e nao so o que chegou: "[audio]" pareceria um
    /// audio que o sistema ouviu. A duracao entra porque muda a decisao do
    /// vendedor — audio de 4 segundos e um "ok", audio de dois minutos e a
    /// objecao inteira.
    /// </summary>
    public string Marcador => Tipo switch
    {
        TipoDeMidia.Audio when Duracao is { } d => $"[audio nao transcrito, {(int)d.TotalSeconds}s]",
        TipoDeMidia.Audio => "[audio nao transcrito]",
        TipoDeMidia.Imagem => "[imagem nao interpretada]",
        TipoDeMidia.Documento => "[documento nao lido]",
        _ => "[midia nao interpretada]",
    };

    /// <summary>
    /// A midia que o nome do provedor descreve, ou <c>null</c> quando a fala e
    /// texto puro.
    ///
    /// Nome desconhecido vira <see cref="TipoDeMidia.Outro"/> e NAO null: o
    /// provedor inventa tipo novo sem avisar, e tratar o que nao reconhecemos
    /// como texto faria a fala entrar no dossie como se tivesse sido lida.
    /// </summary>
    public static Midia? PeloNome(string? nome, TimeSpan? duracao = null) =>
        nome?.Trim().ToLowerInvariant() switch
        {
            null or "" or "texto" or "text" => null,
            "audio" => new Midia(TipoDeMidia.Audio, duracao),
            "imagem" or "image" => new Midia(TipoDeMidia.Imagem, duracao),
            "documento" or "document" => new Midia(TipoDeMidia.Documento, duracao),
            _ => new Midia(TipoDeMidia.Outro, duracao),
        };

    /// <summary>
    /// A lacuna que este tipo de midia abre no dossie, no plural certo.
    ///
    /// E escrita como o que NAO sabemos, e nao como sinal: o dossie afirma
    /// apenas o que leu, e isto e justamente o que ele nao leu.
    /// </summary>
    public static string Lacuna(TipoDeMidia tipo, int quantas)
    {
        var (uma, varias) = tipo switch
        {
            TipoDeMidia.Audio => ("1 audio nao foi transcrito", $"{quantas} audios nao foram transcritos"),
            TipoDeMidia.Imagem => ("1 imagem nao foi interpretada", $"{quantas} imagens nao foram interpretadas"),
            TipoDeMidia.Documento => ("1 documento nao foi lido", $"{quantas} documentos nao foram lidos"),
            _ => ("1 midia nao foi interpretada", $"{quantas} midias nao foram interpretadas"),
        };

        return $"{(quantas == 1 ? uma : varias)}: o que foi dito ali nao entrou nesta leitura.";
    }
}
