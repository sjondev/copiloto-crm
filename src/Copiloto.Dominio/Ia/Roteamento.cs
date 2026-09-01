namespace Copiloto.Dominio.Ia;

/// <summary>
/// O que se esta pedindo ao modelo. A tarefa e o que decide o gasto.
/// </summary>
public enum Tarefa
{
    /// <summary>A0: vale a pena olhar esta fala? Precisa ser barato e rapido.</summary>
    Triagem = 0,

    /// <summary>A1, A3, A4: ler a conversa, detectar sinal, apontar lacuna.</summary>
    Leitura = 1,

    /// <summary>A5: montar conselho de plano. Pode gastar segundos.</summary>
    Conselho = 2,
}

/// <summary>
/// Um modelo que o router pode escolher. Vem de CONFIGURACAO, nunca de codigo.
/// </summary>
/// <param name="Nome">Como o provedor o chama.</param>
/// <param name="Provedor">Quem hospeda — e a unidade do circuito.</param>
/// <param name="CustoPorMilTokens">Em reais.</param>
/// <param name="LatenciaTipicaMs">Medida, nao prometida pelo fornecedor.</param>
/// <param name="Atende">As tarefas para as quais ele serve.</param>
public record ModeloDisponivel(
    string Nome,
    string Provedor,
    decimal CustoPorMilTokens,
    int LatenciaTipicaMs,
    IReadOnlyList<Tarefa> Atende);

/// <summary>Qual modelo, e POR QUE — o registro que vai ao ledger.</summary>
public record DecisaoDeRoteamento(
    string Modelo,
    string Provedor,
    string Motivo,
    IReadOnlyList<string> Descartados);

/// <summary>
/// Escolhe o modelo por tarefa, custo, latencia e estado do circuito (#29).
///
/// A tabela vem de fora por decisao: trocar de modelo e a operacao mais
/// frequente da vida deste sistema — preco muda, modelo novo sai, provedor cai.
/// Se ela morasse em codigo, cada troca seria deploy, e o efeito pratico e que
/// ninguem troca e o sistema fica no modelo de dois anos atras.
///
/// A ORDEM DOS CRITERIOS E A DECISAO INTEIRA:
///
///   1. atende a tarefa?          filtro, nao criterio — quem nao atende sai
///   2. circuito fechado?         filtro — provedor caido nao e escolhido,
///                                mesmo sendo o ideal
///   3. custo                     criterio principal
///   4. latencia                  desempate
///
/// Custo antes de latencia porque, entre dois modelos que ATENDEM a tarefa, o
/// mais lento ja foi considerado bom o bastante por quem escreveu a tabela. Se
/// a latencia importasse mais que o preco para uma tarefa, o jeito de dizer
/// isso e nao listar o modelo lento nela — e nao inverter o criterio aqui e
/// deixar a triagem escolher o modelo forte "porque respondeu rapido".
/// </summary>
public class RoteadorDeModelo
{
    private readonly IReadOnlyList<ModeloDisponivel> _tabela;
    private readonly Func<string, bool> _circuitoAberto;

    /// <param name="tabela">A configuracao carregada de fora.</param>
    /// <param name="circuitoAberto">
    /// Dado o provedor, ele esta fora do ar? Injetado como funcao porque o
    /// estado do circuito e' da BORDA (mora em Redis, #66) e o router e regra —
    /// ele decide, nao consulta infraestrutura.
    /// </param>
    public RoteadorDeModelo(
        IReadOnlyList<ModeloDisponivel> tabela, Func<string, bool>? circuitoAberto = null)
    {
        ArgumentNullException.ThrowIfNull(tabela);
        if (tabela.Count == 0)
            throw new ArgumentException(
                "Tabela de modelos vazia: o router nao tem o que escolher, e "
                + "subir assim adiaria o erro para a primeira conversa real.",
                nameof(tabela));

        _tabela = tabela;
        _circuitoAberto = circuitoAberto ?? (_ => false);
    }

    /// <summary>
    /// Escolhe. Devolve null quando nao ha candidato — e null e a resposta
    /// honesta: cair no "melhor disponivel" mandaria a triagem para o modelo
    /// caro justamente quando o barato caiu, que e o pior momento para gastar.
    /// </summary>
    public DecisaoDeRoteamento? Escolher(Tarefa tarefa)
    {
        var atendem = _tabela.Where(m => m.Atende.Contains(tarefa)).ToList();
        if (atendem.Count == 0) return null;

        var descartados = new List<string>();
        var disponiveis = new List<ModeloDisponivel>();

        foreach (var m in atendem)
        {
            if (_circuitoAberto(m.Provedor))
                descartados.Add($"{m.Nome}: circuito aberto em {m.Provedor}");
            else
                disponiveis.Add(m);
        }

        if (disponiveis.Count == 0) return null;

        var escolhido = disponiveis
            .OrderBy(m => m.CustoPorMilTokens)
            .ThenBy(m => m.LatenciaTipicaMs)
            .First();

        foreach (var m in disponiveis.Where(m => m != escolhido))
            descartados.Add($"{m.Nome}: mais caro (R$ {m.CustoPorMilTokens}/1k)");

        var motivo = descartados.Any(d => d.Contains("circuito aberto"))
            ? $"mais barato entre os que atendem {tarefa} e estao de pe"
            : $"mais barato entre os que atendem {tarefa}";

        return new DecisaoDeRoteamento(escolhido.Nome, escolhido.Provedor, motivo, descartados);
    }
}
