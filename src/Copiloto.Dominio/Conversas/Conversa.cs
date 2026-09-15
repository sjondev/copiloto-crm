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

    /// <summary>
    /// As falas em ordem cronologica, ordenadas na LEITURA e nao so na escrita.
    ///
    /// <see cref="Registrar"/> nao e a unica porta de entrada: o EF Core
    /// materializa a entidade escrevendo direto no campo, na ordem que a
    /// consulta devolveu — e a chave e <c>Guid</c>, entao nao ha ordem
    /// cronologica nenhuma a esperar do banco.
    /// </summary>
    public IReadOnlyList<Mensagem> Mensagens => _mensagens.OrderBy(m => m.EnviadaEm).ToList();

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

    /// <summary>
    /// A fala mais recente do cliente, pela data e nao pela posicao na lista:
    /// posicao e o que o banco decidir, data e o que aconteceu.
    /// </summary>
    public Mensagem? UltimaDoCliente =>
        _mensagens.Where(m => m.Autor == Autor.Cliente).MaxBy(m => m.EnviadaEm);

    /// <summary>Silencio desde a ultima fala do cliente — o sinal de esfriamento.</summary>
    public TimeSpan? SilencioDoCliente(DateTimeOffset agora) =>
        UltimaDoCliente is { } ultima ? agora - ultima.EnviadaEm : null;
}
