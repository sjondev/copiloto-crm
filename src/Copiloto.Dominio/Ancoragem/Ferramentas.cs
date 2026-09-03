namespace Copiloto.Dominio.Ancoragem;

/// <summary>
/// O que uma ferramenta devolveu. `Achou = false` NAO e erro: e a resposta
/// legitima de "nao ha dado para sustentar isso", e e ela que faz a sugestao
/// virar pergunta em vez de afirmacao.
/// </summary>
public record Achado(bool Achou, string? Valor = null)
{
    public static readonly Achado Nada = new(false);
    public static Achado De(string valor) => new(true, valor);
}

/// <summary>
/// As ferramentas que o agente chama ANTES de afirmar (#57).
///
/// Este e o encaixe que justifica MCP no projeto, e ele e maior do que parece:
/// a ancoragem deixa de ser instrucao no prompt — que o modelo pode ignorar — e
/// vira PROPRIEDADE DA ARQUITETURA. Se o dado nao esta no contexto porque a
/// ferramenta nao devolveu nada, nao ha o que inventar.
///
/// E a diferenca entre pedir ao modelo que se comporte e tornar o mau
/// comportamento impossivel.
/// </summary>
public interface IFerramentasDeAncoragem
{
    /// <summary>Escassez real. Devolve o numero quando ele sustenta a fala.</summary>
    Achado ConsultarEstoque(string produto);

    /// <summary>Preco vigente e faixa autorizada para a quantidade.</summary>
    Achado PrecoVigente(string produto, int quantidade);

    /// <summary>O que o vendedor PODE oferecer para este perfil.</summary>
    Achado PoliticaDesconto(string perfilCliente);

    /// <summary>Prova social real, agregada — nunca cliente nominal.</summary>
    Achado ClientesSemelhantesQueCompraram(string perfil);

    /// <summary>Prazo real para o CEP.</summary>
    Achado PrazoEntrega(string cep, string produto);
}

/// <summary>
/// Uma chamada de ferramenta, para o ledger: qual, com que argumento, quanto
/// demorou e se achou.
///
/// A latencia entra porque ancoragem que demora demais deixa de ser usada —
/// alguem "desliga para testar" e nunca religa. Medir e o que permite descobrir
/// isso antes de virar decisao silenciosa.
/// </summary>
public record ChamadaDeFerramenta(
    string Ferramenta,
    string Argumento,
    bool Achou,
    int LatenciaMs);
