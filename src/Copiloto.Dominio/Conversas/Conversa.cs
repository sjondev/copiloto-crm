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
    /// As falas em ordem cronologica, sempre.
    ///
    /// A ordenacao acontece na LEITURA, e nao so no <see cref="Registrar"/>,
    /// porque a lista tem duas origens. Quando o webhook chama `Registrar`, ela
    /// ja nasce ordenada; quando o ORM materializa a conversa do banco, ela vem
    /// na ordem que o provedor devolveu — e a chave primaria e Guid, entao nao
    /// ha ordem cronologica nenhuma garantida ali (#136).
    ///
    /// Confiar na ordem da lista sem isto e o tipo de defeito que nao levanta
    /// excecao: o numero sai errado e plausivel na tela do vendedor.
    /// </summary>
    public IReadOnlyList<Mensagem> Mensagens =>
        _mensagens.OrderBy(m => m.EnviadaEm).ToList();

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
    /// A ultima fala do cliente, por DATA e nao por posicao.
    ///
    /// Era `LastOrDefault`, que dependia de a lista estar ordenada — verdade
    /// quando ela vem do webhook, mentira quando vem do banco. O silencio saiu
    /// errado num teste (9 dias onde eram 8) porque a primeira fala do cliente
    /// ficou por ultimo na materializacao.
    /// </summary>
    public Mensagem? UltimaDoCliente =>
        _mensagens.Where(m => m.Autor == Autor.Cliente).MaxBy(m => m.EnviadaEm);

    /// <summary>Silencio desde a ultima fala do cliente — o sinal de esfriamento.</summary>
    public TimeSpan? SilencioDoCliente(DateTimeOffset agora) =>
        UltimaDoCliente is null ? null : agora - UltimaDoCliente.EnviadaEm;
}
