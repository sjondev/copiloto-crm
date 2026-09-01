namespace Copiloto.Dominio.Conversas;

/// <summary>
/// O fio de mensagens com um lead, em ordem de chegada.
/// </summary>
public class Conversa
{
    private readonly List<Mensagem> _mensagens = new();

    public Conversa(Guid id, Guid leadId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Conversa sem id.", nameof(id));
        if (leadId == Guid.Empty) throw new ArgumentException("Conversa sem lead.", nameof(leadId));

        Id = id;
        LeadId = leadId;
    }

    public Guid Id { get; }
    public Guid LeadId { get; }
    public IReadOnlyList<Mensagem> Mensagens => _mensagens;

    /// <summary>
    /// Guarda a fala, mantendo a ordem cronologica.
    ///
    /// A ordenacao e por data de ENVIO e nao de chegada: mensagem de WhatsApp
    /// chega fora de ordem quando o celular estava sem sinal, e o dossie que le
    /// "vou pensar" antes de "qual o valor?" entende a conversa ao contrario.
    /// </summary>
    public void Registrar(Mensagem mensagem)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        if (_mensagens.Any(m => m.Id == mensagem.Id)) return;   // reentrega do webhook

        _mensagens.Add(mensagem);
        _mensagens.Sort((a, b) => a.EnviadaEm.CompareTo(b.EnviadaEm));
    }

    public Mensagem? UltimaDoCliente =>
        _mensagens.LastOrDefault(m => m.Autor == Autor.Cliente);

    /// <summary>Silencio desde a ultima fala do cliente — o sinal de esfriamento.</summary>
    public TimeSpan? SilencioDoCliente(DateTimeOffset agora) =>
        UltimaDoCliente is null ? null : agora - UltimaDoCliente.EnviadaEm;
}
