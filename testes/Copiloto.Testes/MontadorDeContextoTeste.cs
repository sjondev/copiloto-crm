using Copiloto.Api.Ia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Ia;
using Microsoft.Extensions.Configuration;

namespace Copiloto.Testes;

/// <summary>
/// O montador de contexto (#31).
///
/// Conversa de tres meses nao cabe em janela nenhuma, e a pergunta nao e "como
/// mandar tudo" — e "o que sacrificar primeiro". A ordem de sacrificio e a
/// decisao de produto inteira.
/// </summary>
public class MontadorDeContextoTeste
{
    private static readonly DateTimeOffset Inicio = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private const string Identidade =
        "Voce le a conversa e entrega contexto ao vendedor. NUNCA escreve para o cliente. "
        + "Escassez, prova social, desconto e prazo so viram sugestao com dado no CRM.";

    private static Fala FalaDe(Autor autor, string texto, int minuto) =>
        new(autor, [new Mensagem(Guid.NewGuid(), autor, texto, Inicio.AddMinutes(minuto))]);

    /// <summary>Uma conversa longa como as de verdade: meses de troca.</summary>
    private static List<Fala> ConversaDeTresMeses(int falas = 900)
    {
        var conversa = new List<Fala>();
        for (var i = 0; i < falas; i++)
        {
            var autor = i % 2 == 0 ? Autor.Cliente : Autor.Vendedor;
            conversa.Add(FalaDe(autor,
                $"fala numero {i} com texto suficiente para ocupar espaco de verdade no contexto", i));
        }

        return conversa;
    }

    [Fact]
    public void Conversa_de_tres_meses_cabe_no_orcamento_sem_estourar()
    {
        var orcamento = new OrcamentoDeContexto(Total: 8000);
        var montador = new MontadorDeContexto(orcamento);

        var contexto = montador.Montar(Identidade, "playbook", "ficha", ConversaDeTresMeses());

        Assert.True(contexto.TokensEstimados <= orcamento.Total,
            $"estourou: {contexto.TokensEstimados} de {orcamento.Total}");
        Assert.True(contexto.Cortou);
    }

    [Fact]
    public void A_identidade_sobrevive_a_qualquer_nivel_de_corte()
    {
        // C0 carrega a regra de ancoragem. Cortada ali, o agente para de saber
        // que nao pode inventar escassez — contexto cortado na C0 e pior que
        // contexto nenhum.
        var apertadissimo = new OrcamentoDeContexto(Total: 260, C0: 200, C1: 10, C2: 10);

        var contexto = new MontadorDeContexto(apertadissimo)
            .Montar(Identidade, "playbook enorme", "ficha enorme", ConversaDeTresMeses());

        Assert.Contains("NUNCA escreve para o cliente", contexto.Texto);
        Assert.Contains("so viram sugestao com dado no CRM", contexto.Texto);
    }

    [Fact]
    public void As_falas_recentes_ficam_literais_mesmo_sem_orcamento()
    {
        // O criterio menos obvio e mais importante: tom, ironia e objecao velada
        // so sobrevivem no texto cru. Resumir o recente destroi exatamente o
        // sinal que o dossie existe para captar.
        var conversa = ConversaDeTresMeses();
        conversa.Add(FalaDe(Autor.Cliente, "vou pensar melhor e te falo", 10_000));

        var semEspaco = new OrcamentoDeContexto(Total: 300, C0: 200, C1: 10, C2: 10, FalasSempreLiterais: 3);

        var contexto = new MontadorDeContexto(semEspaco)
            .Montar(Identidade, "", "", conversa);

        Assert.Contains("vou pensar melhor e te falo", contexto.Texto);
    }

    [Fact]
    public void Corta_do_mais_antigo_e_mantem_o_mais_novo()
    {
        // Falas longas de proposito: com texto curto as tres caberiam no
        // orcamento e o teste nao provaria corte nenhum.
        var enchimento = new string('x', 400);
        var conversa = new List<Fala>
        {
            FalaDe(Autor.Cliente, $"PRIMEIRA fala, la do comeco de tudo {enchimento}", 0),
            FalaDe(Autor.Vendedor, $"resposta do meio da conversa {enchimento}", 1),
            FalaDe(Autor.Cliente, $"ULTIMA fala, a que importa agora {enchimento}", 2),
        };

        var apertado = new OrcamentoDeContexto(Total: 400, C0: 200, C1: 5, C2: 5, FalasSempreLiterais: 1);

        var contexto = new MontadorDeContexto(apertado).Montar(Identidade, "", "", conversa);

        Assert.Contains("ULTIMA fala", contexto.Texto);
        Assert.DoesNotContain("PRIMEIRA fala", contexto.Texto);
    }

    [Fact]
    public void O_que_foi_omitido_e_dito_e_nao_resumido()
    {
        // Marca de omissao, nao resumo. Resumir de verdade exige ler, e ler
        // custa uma chamada de modelo (#35). Inventar "o cliente demonstrou
        // interesse" seria pOr na boca do agente uma leitura que ninguem fez.
        var apertado = new OrcamentoDeContexto(Total: 400, C0: 200, C1: 5, C2: 5, FalasSempreLiterais: 2);

        var contexto = new MontadorDeContexto(apertado)
            .Montar(Identidade, "", "", ConversaDeTresMeses(50));

        Assert.Contains("omitidas por orcamento de contexto", contexto.Texto);
        Assert.Contains(contexto.Cortes, c => c.StartsWith("C3 conversa cortada", StringComparison.Ordinal));
    }

    [Fact]
    public void Conversa_curta_entra_inteira_e_sem_marca_de_corte()
    {
        var conversa = new List<Fala>
        {
            FalaDe(Autor.Cliente, "qual o valor do kg?", 0),
            FalaDe(Autor.Vendedor, "o bourbon sai a 78", 1),
        };

        var contexto = new MontadorDeContexto().Montar(Identidade, "playbook", "ficha", conversa);

        Assert.False(contexto.Cortou);
        Assert.Contains("qual o valor do kg?", contexto.Texto);
        Assert.DoesNotContain("omitidas", contexto.Texto);
    }

    [Fact]
    public void O_playbook_entra_inteiro_ou_nao_entra()
    {
        // Meia regra e pior que nenhuma: o agente seguiria a metade que sobrou
        // sem saber que havia mais.
        var playbook = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"regra {i} da empresa"));

        var contexto = new MontadorDeContexto(new OrcamentoDeContexto(C1: 50))
            .Montar(Identidade, playbook, "", []);

        Assert.DoesNotContain("regra 0 da empresa", contexto.Texto);
        Assert.Contains(contexto.Cortes, c => c.StartsWith("C1 playbook fora", StringComparison.Ordinal));
    }

    [Fact]
    public void A_ficha_corta_pelo_fim_e_mantem_o_comeco()
    {
        // O comeco identifica o negocio; o fim traz detalhe acessorio, e e o que
        // se perde com menos dano.
        var ficha = "Lead: padaria do centro\nEstagio: proposta\n"
                    + string.Join("\n", Enumerable.Range(0, 300).Select(i => $"observacao acessoria {i}"));

        var contexto = new MontadorDeContexto(new OrcamentoDeContexto(C2: 60))
            .Montar(Identidade, "", ficha, []);

        Assert.Contains("Lead: padaria do centro", contexto.Texto);
        Assert.DoesNotContain("observacao acessoria 299", contexto.Texto);
        Assert.Contains(contexto.Cortes, c => c.StartsWith("C2 ficha cortada", StringComparison.Ordinal));
    }

    [Fact]
    public void A_contagem_acompanha_o_texto_que_sai()
    {
        var contexto = new MontadorDeContexto().Montar(Identidade, "playbook", "ficha", []);

        Assert.Equal(MontadorDeContexto.EstimarTokens(contexto.Texto), contexto.TokensEstimados);
    }

    [Fact]
    public void A_estimativa_erra_para_mais_e_nao_para_menos()
    {
        // Estourar a janela custa a chamada inteira; sobrar espaco nao custa
        // nada. Por isso a heuristica e mais pessimista que os ~4 caracteres
        // por token usuais.
        var texto = new string('a', 350);

        Assert.True(MontadorDeContexto.EstimarTokens(texto) >= 350 / 4);
    }

    [Fact]
    public void Identidade_maior_que_o_orcamento_inteiro_nao_sobe()
    {
        // Sobraria zero para a conversa, e o agente responderia sem ter lido
        // nada — falhar na subida e melhor que descobrir isso em producao.
        var erro = Assert.Throws<ArgumentException>(
            () => new MontadorDeContexto(new OrcamentoDeContexto(Total: 200, C0: 200)));

        Assert.Contains("sem ter lido nada", erro.Message);
    }

    [Fact]
    public void O_orcamento_vem_da_configuracao()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Contexto:Total"] = "3000",
                ["Contexto:FalasSempreLiterais"] = "2",
            })
            .Build();

        var orcamento = OrcamentoDeContextoConfig.Carregar(config);

        Assert.Equal(3000, orcamento.Total);
        Assert.Equal(2, orcamento.FalasSempreLiterais);
    }

    [Fact]
    public void Secao_ausente_usa_o_padrao_do_dominio()
    {
        // O projeto sobe no primeiro clone sem exigir configuracao.
        var vazia = new ConfigurationBuilder().Build();

        Assert.Equal(new OrcamentoDeContexto(), OrcamentoDeContextoConfig.Carregar(vazia));
    }
}
