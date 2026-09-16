using Copiloto.Api.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Api.Infra;

/// <summary>Como uma dependencia esta agora.</summary>
public record EstadoDaDependencia(string Nome, bool Ok, string Detalhe, bool Essencial);

/// <summary>
/// O retrato das dependencias (#72).
///
/// Cada uma reportada SEPARADAMENTE, e nao um "ok" agregado: health check que
/// responde so verde ou vermelho manda o plantonista procurar do zero. O que
/// ele precisa saber e qual peca caiu.
/// </summary>
public record RelatorioDeSaude(IReadOnlyList<EstadoDaDependencia> Dependencias)
{
    /// <summary>
    /// A aplicacao esta apta a receber trafego?
    ///
    /// So o que e ESSENCIAL derruba: banco fora e fila fora impedem atender.
    /// Estado compartilhado fora degrada — a aplicacao continua util, com
    /// garantias menores, e devolver 503 nesse caso tiraria do ar um sistema
    /// que ainda atende.
    /// </summary>
    public bool Apta => Dependencias.Where(d => d.Essencial).All(d => d.Ok);

    /// <summary>Alguma coisa esta pior do que deveria, mesmo que apta.</summary>
    public bool Degradada => Dependencias.Any(d => !d.Ok);
}

/// <summary>Monta o relatorio consultando cada dependencia de verdade.</summary>
public class Saude
{
    private readonly CopilotoDbContext _ctx;
    private readonly IQueue<Ingestao.MensagemRecebida> _fila;
    private readonly IDistributedState _estado;
    private readonly string _backendDeEstado;
    private readonly string _backendDeFila;

    public Saude(
        CopilotoDbContext ctx,
        IQueue<Ingestao.MensagemRecebida> fila,
        IDistributedState estado,
        IConfiguration configuracao)
    {
        _ctx = ctx;
        _fila = fila;
        _estado = estado;
        _backendDeEstado = configuracao["STATE_BACKEND"] ?? Backends.Padrao;
        _backendDeFila = configuracao["QUEUE_BACKEND"] ?? Backends.Padrao;
    }

    public async Task<RelatorioDeSaude> Agora(CancellationToken ct)
    {
        return new RelatorioDeSaude(
        [
            await Postgres(ct),
            Fila(),
            Estado(),
        ]);
    }

    private async Task<EstadoDaDependencia> Postgres(CancellationToken ct)
    {
        try
        {
            var conecta = await _ctx.Database.CanConnectAsync(ct);
            return new EstadoDaDependencia("postgres", conecta,
                conecta ? "conectado" : "sem conexao", Essencial: true);
        }
        catch (Exception erro)
        {
            // A mensagem do provedor entra no detalhe, e nao so "falhou": e a
            // diferenca entre o plantonista saber que e senha errada e ficar
            // adivinhando.
            return new EstadoDaDependencia("postgres", false, erro.Message, Essencial: true);
        }
    }

    /// <summary>
    /// Fila fora e ESSENCIAL, e este e o ponto contraintuitivo da issue: com a
    /// fila fora, recusar a mensagem e melhor que aceitar. Aceitar e perder e a
    /// falha silenciosa que a fila existe para eliminar — o WhatsApp reentrega
    /// o que deu erro, e nao reentrega o que recebeu 202.
    /// </summary>
    private EstadoDaDependencia Fila() =>
        new("fila", _fila.Aceitando,
            _fila.Aceitando
                ? $"{_backendDeFila}, {_fila.Aguardando} aguardando"
                : $"{_backendDeFila}, nao aceita trabalho novo",
            Essencial: true);

    private EstadoDaDependencia Estado()
    {
        if (_estado is not EstadoComDegradacao comDegradacao)
            return new EstadoDaDependencia("estado", true, _backendDeEstado, Essencial: false);

        return new EstadoDaDependencia("estado", !comDegradacao.Degradado,
            comDegradacao.Degradado
                ? $"degradado desde {comDegradacao.DegradadoDesde:O} — {comDegradacao.RiscoAtual}"
                : _backendDeEstado,
            Essencial: false);
    }
}
