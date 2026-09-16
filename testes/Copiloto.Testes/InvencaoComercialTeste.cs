using System.Reflection;
using System.Text.Json;
using Copiloto.Api.Ia;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Planos;

namespace Copiloto.Testes;

/// <summary>
/// O agente nao inventa promessa comercial, e isto prova (#15, #16).
///
/// "Meu copiloto nao inventa promessa comercial, e eu tenho teste automatizado
/// que prova" e uma frase que so vale se o teste existir. Sem ele, e intencao
/// escrita no prompt — e prompt nao e garantia, e pedido.
///
/// Os casos vivem em `seed/invencoes/tentativas.json`, um por tipo de invencao.
/// Editar aquele arquivo para "fazer passar" desfaz a regra, e e por isso que a
/// checagem le o arquivo em vez de repetir os casos aqui dentro.
/// </summary>
public class InvencaoComercialTeste
{
    private sealed record Caso(string Nome, string PorQueECaro, JsonElement RespostaDoModelo);

    private static List<Caso> Casos()
    {
        var raiz = typeof(InvencaoComercialTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        var json = File.ReadAllText(Path.Combine(raiz, "seed", "invencoes", "tentativas.json"));

        return JsonDocument.Parse(json).RootElement.GetProperty("casos").EnumerateArray()
            .Select(c => new Caso(
                c.GetProperty("nome").GetString()!,
                c.GetProperty("por_que_e_caro").GetString()!,
                c.GetProperty("resposta_do_modelo").Clone()))
            .ToList();
    }

    public static TheoryData<string> NomesDosCasos()
    {
        var dados = new TheoryData<string>();
        foreach (var caso in Casos()) dados.Add(caso.Nome);
        return dados;
    }

    private static FakeProvider ProvedorQueInventa(JsonElement resposta) =>
        new(new Dictionary<Tarefa, RespostaGravada>
        {
            [Tarefa.Conselho] = new(Tarefa.Conselho, resposta, 100, 40),
        });

    [Theory]
    [MemberData(nameof(NomesDosCasos))]
    public async Task Nenhuma_invencao_chega_a_tela_como_fala_pronta(string nome)
    {
        var caso = Casos().Single(c => c.Nome == nome);
        var provedor = ProvedorQueInventa(caso.RespostaDoModelo);

        var resposta = await provedor.Responder(
            new PedidoAoModelo(Tarefa.Conselho, "fake-forte", "monte o plano"),
            CancellationToken.None);

        var plano = LeitorDePlano.Ler(resposta.Conteudo, new Playbook(Guid.NewGuid(), "padrao"));

        // Se algum bloco AFIRMA sem ancora, a regra furou — e o build reprova.
        var afirmouSemLastro = plano.Blocos
            .Where(b => !b.EhPergunta && string.IsNullOrWhiteSpace(b.Ancora))
            .ToList();

        Assert.True(afirmouSemLastro.Count == 0,
            $"'{nome}' passou afirmando sem ancora: "
            + $"{string.Join(" | ", afirmouSemLastro.Select(b => b.Texto))}. {caso.PorQueECaro}");

        Assert.NotEmpty(plano.Recusados);
    }

    [Fact]
    public void Os_seis_tipos_de_invencao_estao_cobertos()
    {
        // O criterio de aceite lista seis. Se alguem apagar um caso do seed, o
        // teste acima continuaria verde com menos cobertura — e verde por nao
        // ter olhado e pior que vermelho.
        var nomes = Casos().Select(c => c.Nome).ToList();

        Assert.Equal(6, nomes.Count);
        Assert.Contains(nomes, n => n.Contains("estoque"));
        Assert.Contains(nomes, n => n.Contains("desconto"));
        Assert.Contains(nomes, n => n.Contains("prazo"));
        Assert.Contains(nomes, n => n.Contains("clientes"));
        Assert.Contains(nomes, n => n.Contains("preco"));
        Assert.Contains(nomes, n => n.Contains("promessa"));
    }

    [Fact]
    public void A_saida_honesta_passa_inteira()
    {
        // A regra nao e "bloquear tudo": e "sem dado, vira pergunta". Se a
        // pergunta ao vendedor tambem fosse barrada, o produto ficaria mudo e
        // alguem acabaria desligando a regra.
        var comPergunta = """
            {"blocos":[{"titulo":"Escassez","tecnica":"Escassez","ancorado_em":null,
             "conselho":null,
             "pergunta_ao_vendedor":"Esse lote esta mesmo acabando? Nao ha dado de estoque no CRM."}]}
            """;

        var plano = LeitorDePlano.Ler(comPergunta, new Playbook(Guid.NewGuid(), "padrao"));

        var bloco = Assert.Single(plano.Blocos);
        Assert.True(bloco.EhPergunta);
        Assert.Empty(plano.Recusados);
    }

    [Fact]
    public void Sugestao_com_dado_do_CRM_passa()
    {
        // O outro lado da regra: havendo lastro, a tecnica e permitida. Uma
        // regra que barra tudo nao protege ninguem, so vira ruido.
        var ancorada = """
            {"blocos":[{"titulo":"Escassez real","tecnica":"Escassez",
             "ancorado_em":"estoque do lote 7 no CRM: 2 unidades em 2026-09-15",
             "conselho":"Da para dizer que restam 2 unidades desse lote, porque restam mesmo."}]}
            """;

        var plano = LeitorDePlano.Ler(ancorada, new Playbook(Guid.NewGuid(), "padrao"));

        var bloco = Assert.Single(plano.Blocos);
        Assert.False(bloco.EhPergunta);
        Assert.Contains("lote 7", bloco.Ancora);
    }

    [Fact]
    public void Tecnica_que_nao_existe_cai_na_regra_mais_restrita()
    {
        // A porta dos fundos da regra inteira: bastaria o modelo escrever
        // "Escassez2" para afirmar o que quisesse sem precisar de ancora.
        var inventada = """
            {"blocos":[{"titulo":"?","tecnica":"Escassez2","ancorado_em":null,
             "conselho":"Fala que e o ultimo lote do ano."}]}
            """;

        var plano = LeitorDePlano.Ler(inventada, new Playbook(Guid.NewGuid(), "padrao"));

        Assert.Empty(plano.Blocos);
        Assert.Contains(plano.Recusados, r => r.Motivo.Contains("sem ancora"));
    }

    [Fact]
    public void Resposta_ilegivel_nao_vira_meio_plano()
    {
        // Metade de um plano e pior que plano nenhum: o vendedor nao tem como
        // saber que faltou pedaco.
        var plano = LeitorDePlano.Ler("{\"blocos\": [{\"tecn", new Playbook(Guid.NewGuid(), "padrao"));

        Assert.Empty(plano.Blocos);
        Assert.Contains(plano.Recusados, r => r.Motivo.StartsWith("resposta ilegivel", StringComparison.Ordinal));
    }
}
