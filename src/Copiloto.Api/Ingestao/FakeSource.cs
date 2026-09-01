namespace Copiloto.Api.Ingestao;

/// <summary>
/// Reproduz conversas gravadas, sem tocar em rede (#20).
///
/// E o padrao quando `CONVERSATION_SOURCE` nao esta definido, e isso e decisao:
/// a suite roda offline e de graca, e a demo da entrevista nao depende do wi-fi
/// da sala nem de cota de provedor. Fonte que exige credencial para o `dotnet
/// test` passar e fonte que quebra no primeiro clone.
/// </summary>
public class FakeSource
{
    private readonly IReadOnlyList<ConversaGravada> _conversas;

    public FakeSource(IReadOnlyList<ConversaGravada> conversas) => _conversas = conversas;

    /// <summary>Carrega o que estiver em `seed/conversas/*.json`.</summary>
    public static FakeSource DaPasta(string pasta)
    {
        if (!Directory.Exists(pasta))
            throw new DirectoryNotFoundException(
                $"Pasta de conversas nao encontrada: {pasta}. O FakeSource e o padrao, "
                + "entao a ausencia dela quebraria o projeto no primeiro clone — e o erro "
                + "precisa dizer isso, nao 'sequencia vazia'.");

        var conversas = Directory.GetFiles(pasta, "*.json")
            .OrderBy(c => c)
            .Select(c => ConversaGravada.Ler(File.ReadAllText(c)))
            .ToList();

        return new FakeSource(conversas);
    }

    public IReadOnlyList<ConversaGravada> Conversas => _conversas;

    /// <summary>
    /// As mensagens de uma conversa, no formato que o webhook entregaria.
    ///
    /// O `inicio` ancora os offsets do roteiro: o JSON guarda segundos
    /// relativos, e nao data absoluta, porque conversa gravada em marco nao
    /// pode chegar ao dossie como "esfriou ha seis meses".
    /// </summary>
    public IEnumerable<MensagemRecebida> Reproduzir(string conversaId, DateTimeOffset inicio)
    {
        var conversa = _conversas.FirstOrDefault(c => c.Id == conversaId)
            ?? throw new ArgumentException($"conversa '{conversaId}' nao esta no seed", nameof(conversaId));

        foreach (var (m, i) in conversa.Mensagens.Select((m, i) => (m, i)))
        {
            var de = m.DoCliente ? conversa.Cliente.Telefone : conversa.Empresa.Telefone;
            var para = m.DoCliente ? conversa.Empresa.Telefone : conversa.Cliente.Telefone;

            yield return new MensagemRecebida(
                $"seed.{conversa.Id}.{i}", de, para, m.Texto,
                inicio.AddSeconds(m.OffsetSegundos));
        }
    }

    /// <summary>
    /// O atraso entre duas mensagens no modo demo.
    ///
    /// Modo instantaneo (fator 0) para teste; acelerado para a demo parecer ao
    /// vivo sem a plateia esperar os quatro minutos reais entre a pergunta e a
    /// resposta do vendedor.
    /// </summary>
    public static TimeSpan Atraso(MensagemGravada anterior, MensagemGravada atual, double fator)
    {
        if (fator <= 0) return TimeSpan.Zero;
        var real = atual.OffsetSegundos - anterior.OffsetSegundos;
        return TimeSpan.FromSeconds(Math.Max(0, real) * fator);
    }
}
