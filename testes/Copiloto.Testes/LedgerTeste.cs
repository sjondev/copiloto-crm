using System.Reflection;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O ledger de invocacoes (#1).
///
/// Sem ele o painel de ROI e chute: a pergunta central do projeto — a IA se
/// paga? — depende de saber quanto cada chamada custou, e a QUAL negocio.
///
/// A entidade existia desde o comeco e nada em producao gravava nela. Isto aqui
/// cobre o que ela precisa registrar; que os caminhos de verdade gravam esta em
/// `SugestaoDoPlanoTeste` e `LeituraNaTelaTeste`.
/// </summary>
public class LedgerTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    private static readonly ModeloDisponivel Barato =
        new("fake-mini", "fake", 0.5m, 10, [Tarefa.Leitura]);

    private static readonly ModeloDisponivel Caro =
        new("fake-forte", "fake", 2m, 50, [Tarefa.Leitura]);

    private static string Raiz => typeof(LedgerTeste).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "RaizDoRepositorio").Value!;

    private static AgenteDeLeitura Agente(
        IModelProvider provedor, params ModeloDisponivel[] modelos) =>
        new(new CascataDeModelos(
                new RoteadorDeModelo(modelos), provedor, NullLogger<CascataDeModelos>.Instance),
            new MontadorDeContexto(),
            new PrecoDoModelo(modelos),
            "Voce le a conversa. NUNCA escreve para o cliente.",
            NullLogger<AgenteDeLeitura>.Instance);

    private static FakeProvider ProvedorDoSeed() =>
        FakeProvider.DaPasta(Path.Combine(Raiz, "seed", "respostas"));

    private static Dominio.Conversas.Conversa Conversa()
    {
        var conversa = new Dominio.Conversas.Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Dominio.Conversas.Mensagem(
            Guid.NewGuid(), Dominio.Conversas.Autor.Cliente, "qual o valor do kg?", T0));
        return conversa;
    }

    // --- Toda invocacao vira linha ---

    [Fact]
    public async Task A_chamada_que_deu_certo_vira_medicao_com_custo()
    {
        var leitura = await Agente(ProvedorDoSeed(), Barato).Ler(
            Conversa(), Guid.NewGuid(), "", "", CancellationToken.None);

        var medicao = Assert.IsType<MedicaoDaChamada>(leitura.Medicao);

        Assert.True(medicao.Sucesso);
        Assert.Equal("fake-mini", medicao.Modelo);
        Assert.True(medicao.TokensTotais > 0, "a chamada precisa registrar tokens");
        Assert.True(medicao.CustoEmReais > 0, "token gasto tem custo");
    }

    /// <summary>
    /// Contar so o que deu certo esconde o custo do que nao deu — e o provedor
    /// cobra pelo token gasto antes de falhar.
    /// </summary>
    [Fact]
    public async Task A_chamada_que_DEGRADOU_tambem_vira_linha()
    {
        var provedor = ProvedorDoSeed();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr, vezes: 5);

        var leitura = await Agente(provedor, Barato).Ler(
            Conversa(), Guid.NewGuid(), "", "", CancellationToken.None);

        Assert.Null(leitura.Dossie);

        var medicao = Assert.IsType<MedicaoDaChamada>(leitura.Medicao);
        Assert.False(medicao.Sucesso);
        Assert.Equal("fake-mini", medicao.Modelo);
    }

    [Fact]
    public async Task Conversa_vazia_nao_gera_linha_porque_nao_houve_chamada()
    {
        var vazia = new Dominio.Conversas.Conversa(Guid.NewGuid(), Guid.NewGuid());

        var leitura = await Agente(ProvedorDoSeed(), Barato).Ler(
            vazia, Guid.NewGuid(), "", "", CancellationToken.None);

        // Nao e falha: e ausencia de chamada. Linha aqui inflaria a contagem de
        // invocacoes com trabalho que nunca saiu do processo.
        Assert.Null(leitura.Medicao);
    }

    // --- Retentativa e um campo, nao uma linha ---

    /// <summary>
    /// Duas linhas por uma chamada fariam o total de invocacoes medir a
    /// instabilidade do provedor em vez do uso do produto.
    /// </summary>
    [Fact]
    public async Task Descer_a_cascata_conta_TENTATIVAS_e_nao_linhas()
    {
        var provedor = ProvedorDoSeed();

        // O primeiro degrau cai; o segundo responde.
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        var leitura = await Agente(provedor, Barato, Caro).Ler(
            Conversa(), Guid.NewGuid(), "", "", CancellationToken.None);

        var medicao = Assert.IsType<MedicaoDaChamada>(leitura.Medicao);

        Assert.True(medicao.Sucesso);
        Assert.Equal(2, medicao.Tentativas);

        // O custo e de quem RESPONDEU: e dele o token que o provedor cobrou.
        Assert.Equal("fake-forte", medicao.Modelo);
    }

    // --- O preco vem da configuracao ---

    [Fact]
    public void O_preco_sai_da_tabela_de_modelos_e_nao_de_numero_em_codigo()
    {
        var preco = new PrecoDoModelo([Barato, Caro]);

        // 2 reais por mil tokens, mil tokens.
        Assert.Equal(2m, preco.De("fake-forte", 1000));
        Assert.Equal(0.5m, preco.De("fake-mini", 1000));
    }

    /// <summary>
    /// Derrubar a sugestao do vendedor porque o preco nao foi cadastrado
    /// trocaria um numero errado no relatorio por trabalho perdido na frente do
    /// cliente.
    /// </summary>
    [Fact]
    public void Modelo_fora_da_tabela_custa_zero_e_nao_levanta_excecao()
    {
        Assert.Equal(0m, new PrecoDoModelo([Barato]).De("modelo-que-ninguem-cadastrou", 5000));
    }

    // --- A soma ---

    [Fact]
    public void Tres_invocacoes_com_custo_conhecido_somam_o_total_esperado()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), T0);

        foreach (var custo in new[] { 0.15m, 0.25m, 0.60m })
        {
            deal.RegistrarInvocacao(new AiInvocation(
                Guid.NewGuid(), Tarefa.Leitura,
                new MedicaoDaChamada("fake-mini", 100, 40, 12, 1, true, custo),
                T0, deal.Id));
        }

        Assert.Equal(1.00m, deal.CustoIaAcumulado);
    }

    [Fact]
    public void A_invocacao_que_falhou_tambem_entra_no_acumulado()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), T0);

        deal.RegistrarInvocacao(new AiInvocation(
            Guid.NewGuid(), Tarefa.Leitura,
            new MedicaoDaChamada("fake-mini", 900, 0, 30, 3, Sucesso: false, 0.45m),
            T0, deal.Id));

        // O token gasto antes de falhar foi cobrado, e o acumulado do negocio
        // precisa mostrar isso — senao o ROI fica otimista por construcao.
        Assert.Equal(0.45m, deal.CustoIaAcumulado);
    }

    // --- Bordas do registro ---

    [Fact]
    public void Invocacao_sem_tentativa_nenhuma_e_recusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AiInvocation(
            Guid.NewGuid(), Tarefa.Leitura,
            new MedicaoDaChamada("fake-mini", 0, 0, 0, Tentativas: 0, true, 0m), T0));
    }

    [Fact]
    public void O_correlation_id_e_opcional_enquanto_a_42_nao_existe()
    {
        var invocacao = new AiInvocation(
            Guid.NewGuid(), Tarefa.Leitura,
            new MedicaoDaChamada("fake-mini", 10, 5, 1, 1, true, 0m), T0);

        Assert.Null(invocacao.CorrelationId);
    }
}
