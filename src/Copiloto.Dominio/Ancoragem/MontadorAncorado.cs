using System.Diagnostics;
using Copiloto.Dominio.Planos;

namespace Copiloto.Dominio.Ancoragem;

/// <summary>
/// Monta blocos do plano consultando a ferramenta ANTES de afirmar (#57).
///
/// Nenhum metodo daqui aceita o numero QUE VAI SER AFIRMADO. Quem quiser
/// sugerir escassez informa o PRODUTO, e o valor vem da ferramenta — nao ha
/// assinatura por onde um numero inventado entre. A regra deixa de depender de
/// o modelo obedecer e passa a depender do formato da chamada.
///
/// A quantidade em <see cref="Preco"/> e a excecao que confirma a regra: ela e
/// o que o CLIENTE pediu, dito na conversa, e nao um dado da empresa que o
/// modelo poderia inventar — o preco dessa quantidade continua vindo da tabela.
/// </summary>
public class MontadorAncorado
{
    /// <summary>
    /// Acima disto, escassez e' mentira. Fica aqui e nao na ferramenta porque e'
    /// julgamento de VENDA ("dois e pouco, duzentos nao e"), nao de estoque —
    /// a ferramenta responde quanto tem; quem decide se isso e pouco e o
    /// dominio.
    /// </summary>
    public const int EstoqueQueJaNaoEEscasso = 10;

    private readonly IFerramentasDeAncoragem _ferramentas;
    private readonly List<ChamadaDeFerramenta> _chamadas = new();

    public MontadorAncorado(IFerramentasDeAncoragem ferramentas)
    {
        ArgumentNullException.ThrowIfNull(ferramentas);
        _ferramentas = ferramentas;
    }

    /// <summary>O que foi consultado, para o ledger.</summary>
    public IReadOnlyList<ChamadaDeFerramenta> Chamadas => _chamadas;

    public BlocoSugerido Escassez(string produto)
    {
        var achado = Medir("consultar_estoque", produto,
                           () => _ferramentas.ConsultarEstoque(produto));

        if (!achado.Achou)
            return BlocoSugerido.Perguntar(Tatica.Escassez,
                $"Temos pouco estoque de {produto}? Não consegui confirmar.");

        // Estoque FARTO tambem impede a sugestao, e este e o ponto da issue:
        // a ferramenta respondeu, o dado existe, e ele NAO sustenta a fala.
        // Sugerir "restam poucas" com duzentas em estoque e publicidade
        // enganosa — e o erro nao seria do modelo, seria de quem so conferiu
        // se veio resposta.
        if (int.TryParse(achado.Valor, out var quantidade)
            && quantidade > EstoqueQueJaNaoEEscasso)
        {
            return BlocoSugerido.Perguntar(Tatica.Escassez,
                $"Estoque de {produto} está em {quantidade} — não dá para falar em "
                + "escassez. Vale destacar outra coisa?");
        }

        return BlocoSugerido.Ancorado(Tatica.Escassez,
            $"Restam {achado.Valor} de {produto}",
            $"consultar_estoque({produto})={achado.Valor}");
    }

    /// <summary>
    /// Preco vigente para a quantidade. A quantidade entra como parametro e o
    /// PRECO nao: quem chama diz quantos quilos o cliente quer, e o valor sai da
    /// tabela — preco combinado na conversa e divida que a empresa honra depois.
    /// </summary>
    public BlocoSugerido Preco(string produto, int quantidade)
    {
        if (quantidade <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantidade),
                "Quantidade nao positiva nao tem preco: a faixa de desconto e' "
                + "escolhida por ela, e zero cairia na primeira faixa por acidente.");

        var achado = Medir("preco_vigente", $"{produto}/{quantidade}",
                           () => _ferramentas.PrecoVigente(produto, quantidade));

        return achado.Achou
            ? BlocoSugerido.Ancorado(Tatica.Preco,
                $"{quantidade} de {produto}: {achado.Valor}",
                $"preco_vigente({produto},{quantidade})={achado.Valor}")
            : BlocoSugerido.Perguntar(Tatica.Preco,
                $"Qual o preço vigente de {produto} para {quantidade}? "
                + "Não achei na tabela.");
    }

    public BlocoSugerido Desconto(string perfilCliente)
    {
        var achado = Medir("politica_desconto", perfilCliente,
                           () => _ferramentas.PoliticaDesconto(perfilCliente));

        return achado.Achou
            ? BlocoSugerido.Ancorado(Tatica.Desconto,
                $"Condição autorizada: {achado.Valor}",
                $"politica_desconto({perfilCliente})={achado.Valor}")
            : BlocoSugerido.Perguntar(Tatica.Desconto,
                "Existe política de desconto para este perfil? Não achei registro.");
    }

    public BlocoSugerido ProvaSocial(string perfil)
    {
        var achado = Medir("clientes_semelhantes", perfil,
                           () => _ferramentas.ClientesSemelhantesQueCompraram(perfil));

        return achado.Achou
            ? BlocoSugerido.Ancorado(Tatica.ProvaSocial,
                $"Outros clientes parecidos já compraram: {achado.Valor}",
                $"clientes_semelhantes({perfil})={achado.Valor}")
            : BlocoSugerido.Perguntar(Tatica.ProvaSocial,
                "Temos caso parecido para citar? Não encontrei na base.");
    }

    public BlocoSugerido Prazo(string cep, string produto)
    {
        var achado = Medir("prazo_entrega", $"{cep}/{produto}",
                           () => _ferramentas.PrazoEntrega(cep, produto));

        return achado.Achou
            ? BlocoSugerido.Ancorado(Tatica.Prazo,
                $"Prazo de entrega: {achado.Valor}",
                $"prazo_entrega({cep})={achado.Valor}")
            : BlocoSugerido.Perguntar(Tatica.Prazo,
                $"Qual o prazo para o CEP {cep}? A consulta não retornou.");
    }

    private Achado Medir(string ferramenta, string argumento, Func<Achado> chamar)
    {
        var relogio = Stopwatch.StartNew();
        var achado = chamar();
        relogio.Stop();

        _chamadas.Add(new ChamadaDeFerramenta(
            ferramenta, argumento, achado.Achou, (int)relogio.ElapsedMilliseconds));

        return achado;
    }
}
