namespace Copiloto.Dominio.Titulares;

/// <summary>O que o titular pediu (art. 18).</summary>
public enum TipoDePedido
{
    /// <summary>"Voces tratam dado meu?"</summary>
    Confirmacao = 0,

    /// <summary>"Me mostrem tudo que voces tem sobre mim."</summary>
    Acesso = 1,

    Correcao = 2,

    /// <summary>Em formato estruturado, para levar embora.</summary>
    Portabilidade = 3,

    /// <summary>"Com quem voces compartilharam?"</summary>
    Compartilhamento = 4,

    /// <summary>"Parem de me analisar" — sem apagar o historico comercial.</summary>
    Oposicao = 5,

    Exclusao = 6,
}

/// <summary>
/// Um pedido de titular, com o relogio correndo (#81).
///
/// O prazo mora aqui, e nao numa planilha, porque prazo que ninguem mede so
/// aparece depois de vencido — e o que chega depois disso nao e um lembrete, e
/// uma notificacao da ANPD.
/// </summary>
public record PedidoDoTitular(
    Guid Id, Guid LeadId, TipoDePedido Tipo, DateTimeOffset RecebidoEm,
    DateTimeOffset? AtendidoEm = null)
{
    /// <summary>
    /// Quinze dias, o prazo do art. 19 para a declaracao clara e completa.
    ///
    /// A lei tambem preve resposta em formato SIMPLIFICADO de imediato — e o
    /// caso da confirmacao, que e um sim ou nao. O prazo aqui e o da resposta
    /// completa, que e a que exige trabalho e portanto a que atrasa.
    /// </summary>
    public static readonly TimeSpan Prazo = TimeSpan.FromDays(15);

    /// <summary>Quando este pedido passa a estar atrasado.</summary>
    public DateTimeOffset VenceEm => RecebidoEm + Prazo;

    public bool Atendido => AtendidoEm is not null;

    /// <summary>
    /// Passou do prazo e ninguem atendeu. Pedido ATENDIDO nao vence depois —
    /// contar atraso de quem ja respondeu encheria o painel de alarme falso e
    /// esconderia os que ainda importam.
    /// </summary>
    public bool Vencido(DateTimeOffset agora) => !Atendido && agora > VenceEm;

    /// <summary>
    /// Falta pouco. Serve para o alerta chegar ANTES, que e a unica hora em que
    /// ele muda alguma coisa.
    /// </summary>
    public bool VencendoEm(DateTimeOffset agora, TimeSpan antecedencia) =>
        !Atendido && !Vencido(agora) && agora >= VenceEm - antecedencia;

    public PedidoDoTitular Atender(DateTimeOffset quando) => this with { AtendidoEm = quando };

    public TimeSpan? TempoDeResposta => AtendidoEm - RecebidoEm;
}
