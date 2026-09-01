namespace Copiloto.Dominio.Conversas;

/// <summary>
/// Junta baloes seguidos do mesmo falante numa fala so (#19).
///
/// Reduz custo de IA em multiplo direto: quatro baloes viravam quatro analises.
/// E melhora o contexto, porque "o bourbon" sozinho nao diz nada — junto de
/// "vi o cafe de voces" e "qual o valor do kg?", diz tudo.
/// </summary>
public static class AgrupadorDeFalas
{
    /// <summary>
    /// O silencio que fecha uma fala. Dez segundos porque a pausa entre baloes
    /// de uma mesma ideia e de um a tres segundos — quem digita a proxima frase
    /// nao some por dez. Configuravel porque conversa de suporte tem outro
    /// ritmo que conversa de venda.
    /// </summary>
    public static readonly TimeSpan JanelaPadrao = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Quebra a conversa em falas. Baloes do mesmo autor separados por menos
    /// que a janela ficam juntos; troca de falante quebra sempre, mesmo dentro
    /// da janela — o vendedor respondendo no meio encerra a fala do cliente.
    /// </summary>
    public static IReadOnlyList<Fala> Agrupar(
        IEnumerable<Mensagem> mensagens, TimeSpan? janela = null)
    {
        ArgumentNullException.ThrowIfNull(mensagens);
        var limite = janela ?? JanelaPadrao;

        var falas = new List<Fala>();
        var atual = new List<Mensagem>();

        foreach (var m in mensagens.OrderBy(x => x.EnviadaEm))
        {
            var quebrou = atual.Count > 0
                && (atual[^1].Autor != m.Autor
                    || m.EnviadaEm - atual[^1].EnviadaEm > limite);

            if (quebrou)
            {
                falas.Add(new Fala(atual[0].Autor, atual));
                atual = new List<Mensagem>();
            }

            atual.Add(m);
        }

        if (atual.Count > 0) falas.Add(new Fala(atual[0].Autor, atual));
        return falas;
    }
}
