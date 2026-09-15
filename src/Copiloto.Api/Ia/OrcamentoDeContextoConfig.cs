using Copiloto.Dominio.Ia;

namespace Copiloto.Api.Ia;

/// <summary>
/// Carrega o orcamento de contexto do appsettings (#31).
///
/// Vem de fora pelo mesmo motivo da tabela de modelos: o teto muda quando o
/// modelo muda, e quem opera precisa poder apertar o gasto sem esperar deploy.
/// </summary>
public static class OrcamentoDeContextoConfig
{
    public const string Secao = "Contexto";

    /// <summary>
    /// Secao ausente devolve o padrao do dominio, e isso e deliberado: o
    /// projeto sobe no primeiro clone sem exigir configuracao, e os numeros
    /// padrao sao os da propria issue.
    /// </summary>
    public static OrcamentoDeContexto Carregar(IConfiguration config) =>
        config.GetSection(Secao).Get<OrcamentoDeContexto>() ?? new OrcamentoDeContexto();
}
