using System.Reflection;
using System.Text.Json;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Logging.Abstractions;

namespace Copiloto.Testes;

/// <summary>
/// O agente A1 (#13): le a conversa e diz o que ela mostra.
///
/// O que estes testes protegem nao e a leitura em si — e a CITACAO. Pedir o
/// trecho no prompt e pedido, e modelo atende pedido quando lhe convem. O que
/// vale e a conferencia: o trecho e procurado na conversa de verdade, e sinal
/// que nao for encontrado nao chega a existir.
///
/// Sem isso, "o cliente demonstrou interesse" chega a tela e ninguem consegue
/// dizer se e verdade — que e exatamente o "a IA ta ruim" impossivel de
/// verificar que este projeto existe para nao produzir.
/// </summary>
public class AgenteDeLeituraTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ModeloDisponivel Mini =
        new("fake-mini", "provedor-a", 0.5m, 200, [Tarefa.Leitura]);

    private const string Identidade = "Voce le a conversa. NUNCA escreve para o cliente.";

    /// <summary>A conversa que o seed cita, montada aqui para a conferencia ter o que achar.</summary>
    private static Conversa ConversaDoCafe()
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor do kg?", Agora));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Vendedor, "o bourbon sai a 78", Agora.AddMinutes(2)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar melhor e te falo", Agora.AddMinutes(9)));
        return conversa;
    }

    private static FakeProvider ProvedorDoSeed()
    {
        var raiz = typeof(AgenteDeLeituraTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        return FakeProvider.DaPasta(Path.Combine(raiz, "seed", "respostas"));
    }

    private static FakeProvider ProvedorQueResponde(string json) =>
        new(new Dictionary<Tarefa, RespostaGravada>
        {
            [Tarefa.Leitura] = new(Tarefa.Leitura, JsonDocument.Parse(json).RootElement.Clone(), 100, 40),
        });

    private static AgenteDeLeitura Agente(IModelProvider provedor) =>
        new(new CascataDeModelos(
                new RoteadorDeModelo([Mini]), provedor, NullLogger<CascataDeModelos>.Instance),
            new MontadorDeContexto(),
            new PrecoDoModelo([Mini]),
            Identidade,
            NullLogger<AgenteDeLeitura>.Instance);

    /// <summary>
    /// So o dossie. A medicao que a #1 acrescentou tem teste proprio; aqui o
    /// que importa e o que o agente LEU.
    /// </summary>
    private static async Task<Dossie?> Ler(IModelProvider provedor, Conversa? conversa = null) =>
        (await Agente(provedor).Ler(
            conversa ?? ConversaDoCafe(), Guid.NewGuid(), "", "", CancellationToken.None)).Dossie;

    [Fact]
    public async Task A_leitura_do_seed_vira_dossie_com_os_dois_sinais()
    {
        var dossie = await Ler(ProvedorDoSeed());

        Assert.NotNull(dossie);
        Assert.Equal(2, dossie!.Sinais.Count);
        Assert.Single(dossie.SinaisDe(TipoDeSinal.Compra));
        Assert.Single(dossie.SinaisDe(TipoDeSinal.Fuga));
    }

    [Fact]
    public async Task Todo_sinal_aponta_para_uma_fala_que_existe()
    {
        var conversa = ConversaDoCafe();

        var dossie = await Ler(ProvedorDoSeed(), conversa);

        Assert.All(dossie!.Sinais, s =>
            Assert.Contains(conversa.Mensagens, m => m.Id == s.MensagemId));
    }

    [Fact]
    public async Task Sinal_com_citacao_inventada_nao_chega_ao_dossie()
    {
        // O caso que a issue existe para impedir. O modelo devolve uma frase que
        // soa plausivel e nao foi dita por ninguem.
        var dossie = await Ler(ProvedorQueResponde("""
            {"estagio":"consideracao","temperatura":"quente","direcao":"esquentando",
             "sinais":[
               {"tipo":"compra","descricao":"cliente pediu para fechar hoje",
                "trecho_citado":"pode fechar, quero 10 quilos agora"},
               {"tipo":"compra","descricao":"preco perguntado",
                "trecho_citado":"qual o valor do kg?"}]}
            """));

        var sinal = Assert.Single(dossie!.Sinais);
        Assert.Equal("qual o valor do kg?", sinal.TrechoCitado);
    }

    [Fact]
    public async Task Sinal_sem_citacao_nenhuma_nao_e_exibido()
    {
        // "Sinal sem citacao nao e exibido", criterio literal da issue.
        var dossie = await Ler(ProvedorQueResponde("""
            {"estagio":"consideracao","temperatura":"morna","direcao":"estavel",
             "sinais":[{"tipo":"compra","descricao":"o cliente demonstrou interesse"}]}
            """));

        Assert.Empty(dossie!.Sinais);
    }

    [Fact]
    public async Task Parafrase_nao_conta_como_citacao()
    {
        // Trocar PALAVRA e o que distingue citar de inventar. Caixa e espaco
        // podem variar; o texto, nao.
        var dossie = await Ler(ProvedorQueResponde("""
            {"estagio":"consideracao","temperatura":"morna","direcao":"estavel",
             "sinais":[{"tipo":"compra","descricao":"perguntou o preco",
                        "trecho_citado":"quanto custa o quilo?"}]}
            """));

        Assert.Empty(dossie!.Sinais);
    }

    [Fact]
    public async Task Citacao_com_caixa_e_espaco_diferentes_ainda_vale()
    {
        // O modelo troca maiuscula e quebra de linha sem querer, e recusar isso
        // faria a regra descartar citacao honesta.
        var dossie = await Ler(ProvedorQueResponde("""
            {"estagio":"consideracao","temperatura":"morna","direcao":"estavel",
             "sinais":[{"tipo":"compra","descricao":"perguntou o preco",
                        "trecho_citado":"Qual  o VALOR   do kg?"}]}
            """));

        Assert.Single(dossie!.Sinais);
    }

    [Fact]
    public async Task A_temperatura_tem_direcao_e_nao_so_valor()
    {
        // Morno subindo e morno caindo pedem coisas opostas do vendedor.
        var dossie = await Ler(ProvedorDoSeed());

        Assert.Equal(Temperatura.Morna, dossie!.Termometro!.Valor);
        Assert.Equal(Direcao.Esfriando, dossie.Termometro.Para);
        Assert.Equal("morna e esfriando", dossie.Termometro.Resumo);
    }

    [Fact]
    public async Task Temperatura_ausente_nao_vira_leitura_inventada()
    {
        // Sem direcao declarada, "estavel" e a leitura que nao afirma movimento
        // nenhum — inventar uma direcao seria pOr no dossie algo que o modelo
        // nao disse.
        var dossie = await Ler(ProvedorQueResponde("""
            {"estagio":"descoberta","sinais":[]}
            """));

        Assert.Equal(Direcao.Estavel, dossie!.Termometro!.Para);
    }

    [Fact]
    public async Task Cascata_esgotada_nao_produz_dossie_vazio()
    {
        // Dossie vazio na tela pareceria leitura feita que nao achou nada. Null
        // deixa a tela manter o dossie anterior, como a #30 decidiu.
        var provedor = ProvedorDoSeed();
        provedor.CombinarFalha(FalhaSimulada.ProvedorForaDoAr);

        Assert.Null(await Ler(provedor));
    }

    [Fact]
    public async Task Resposta_ilegivel_nao_produz_dossie()
    {
        var provedor = ProvedorDoSeed();
        provedor.CombinarFalha(FalhaSimulada.JsonInvalido);

        Assert.Null(await Ler(provedor));
    }

    [Fact]
    public async Task Conversa_vazia_nao_e_lida()
    {
        var vazia = new Conversa(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(await Ler(ProvedorDoSeed(), vazia));
    }

    [Fact]
    public async Task O_audio_da_conversa_entra_como_lacuna_no_dossie()
    {
        // A leitura precisa dizer que leu metade, quando leu metade (#23).
        var conversa = ConversaDoCafe();
        conversa.Registrar(new Mensagem(
            Guid.NewGuid(), Autor.Cliente, "", Agora.AddMinutes(12),
            new Midia(TipoDeMidia.Audio, TimeSpan.FromSeconds(14))));

        var dossie = await Ler(ProvedorDoSeed(), conversa);

        Assert.Contains(dossie!.Lacunas, l => l.StartsWith("1 audio nao foi transcrito", StringComparison.Ordinal));
    }

    [Fact]
    public void Agente_sem_a_camada_C0_nao_sobe()
    {
        // Sem C0 o agente nao sabe que nao pode inventar.
        var erro = Assert.Throws<ArgumentException>(() => new AgenteDeLeitura(
            new CascataDeModelos(
                new RoteadorDeModelo([Mini]), new FakeProvider(), NullLogger<CascataDeModelos>.Instance),
            new MontadorDeContexto(), new PrecoDoModelo([Mini]), "  ",
            NullLogger<AgenteDeLeitura>.Instance));

        Assert.Contains("nao pode inventar", erro.Message);
    }

    [Fact]
    public void O_prompt_do_A1_existe_e_carrega_a_regra_de_citacao()
    {
        // O arquivo e a camada C0 de verdade. Se alguem apagar a regra dali, a
        // conferencia continua barrando — mas o modelo passa a errar de
        // proposito, e o dossie fica vazio sem ninguem entender por que.
        var raiz = typeof(AgenteDeLeituraTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        var prompt = File.ReadAllText(Path.Combine(raiz, "prompts", "a1-leitura.md"));

        Assert.Contains("copia LITERAL", prompt);
        Assert.Contains("NUNCA escreve para o cliente", prompt);
    }
}
