using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Copiloto.Api.Infra;

namespace Copiloto.Api.Ia;

/// <summary>
/// O que foi analisado e ainda vale, compartilhado entre instancias (#34, #71).
///
/// Com cache local, a taxa de acerto cai proporcionalmente ao numero de
/// replicas: a segunda instancia nao sabe que a primeira ja pagou por aquela
/// analise, e paga de novo.
///
/// O risco desta classe nao e perder acerto, e VAZAR: cache mal chaveado serve
/// o dossie de um cliente para outro, e faz isso em silencio — sem erro, sem
/// log, com a tela mostrando um texto plausivel sobre a pessoa errada. Por isso
/// o dono e gravado DENTRO do valor e conferido na leitura, e nao so embutido
/// na chave.
/// </summary>
public class CacheDeAnalise
{
    private record Guardado(Guid LeadId, string Conteudo);

    private static readonly JsonSerializerOptions Json = new();

    private readonly IDistributedState _estado;
    private readonly TimeSpan _validade;

    public CacheDeAnalise(IDistributedState estado, TimeSpan? validade = null)
    {
        ArgumentNullException.ThrowIfNull(estado);

        _estado = estado;
        _validade = validade ?? TimeSpan.FromHours(6);
    }

    /// <summary>Acertos e erros, para a metrica do painel.</summary>
    public int Acertos { get; private set; }

    public int Erros { get; private set; }

    public double TaxaDeAcerto => Acertos + Erros == 0 ? 0 : (double)Acertos / (Acertos + Erros);

    /// <summary>
    /// A chave e o ESTADO da conversa, nao o texto dela.
    ///
    /// Guardar por texto faria a mesma conversa com uma mensagem a mais virar
    /// outra entrada inteira; guardar so por lead serviria analise velha depois
    /// de o cliente falar de novo — que e o erro pior, porque a resposta parece
    /// certa. O id da ultima mensagem resolve os dois: ele muda exatamente
    /// quando a conversa anda.
    /// </summary>
    public static string Chave(Guid leadId, Guid ultimaMensagemId, string versaoDoPrompt)
    {
        var cru = $"{leadId}|{ultimaMensagemId}|{versaoDoPrompt}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cru)))[..16];

        // O lead vai no prefixo E entra no hash: o prefixo deixa a chave
        // legivel no Redis para depurar e expurgar por titular (#46), e o hash
        // e o que de fato identifica o estado.
        return $"analise:{leadId}:{hash}";
    }

    /// <summary>
    /// Le a analise guardada. Devolve null quando nao ha — e tambem quando o
    /// que ha pertence a OUTRO lead, que e a defesa contra o vazamento
    /// silencioso.
    /// </summary>
    public async Task<string?> Ler(Guid leadId, string chave, CancellationToken ct)
    {
        var cru = await _estado.Ler(chave, ct);
        if (cru is null)
        {
            Erros++;
            return null;
        }

        var guardado = JsonSerializer.Deserialize<Guardado>(cru, Json);
        if (guardado is null || guardado.LeadId != leadId)
        {
            // Conta como erro e nao serve nada. Chegar aqui significa colisao
            // de chave ou chave montada errado — e nos dois casos entregar o
            // conteudo seria mostrar o dossie de um cliente para outro.
            Erros++;
            return null;
        }

        Acertos++;
        return guardado.Conteudo;
    }

    public Task Guardar(Guid leadId, string chave, string conteudo, CancellationToken ct) =>
        _estado.Gravar(chave, JsonSerializer.Serialize(new Guardado(leadId, conteudo), Json),
                       _validade, ct);
}
