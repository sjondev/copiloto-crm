using System.Reflection;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Seguranca;

namespace Copiloto.Testes;

/// <summary>
/// O PII Shield (#43), e o teste que FALHA O BUILD se dado pessoal vazar.
///
/// Conversa com cliente e dado pessoal, e mandar para modelo de terceiro e
/// transferencia sob a LGPD. O teste que quebra o build e o que transforma a
/// intencao em garantia.
/// </summary>
public class EscudoDePiiTeste
{
    [Fact]
    public void Mascara_email_no_meio_da_frase()
    {
        // "Cobre PII no meio de frase, nao so em campo estruturado": em conversa
        // de WhatsApp nao existe campo, existe frase.
        var (texto, mapa) = EscudoDePii.Mascarar("manda pro meu email joao.silva@empresa.com.br pfv");

        Assert.DoesNotContain("joao.silva@empresa.com.br", texto);
        Assert.Contains("[EMAIL_1]", texto);
        Assert.Equal("joao.silva@empresa.com.br", mapa["[EMAIL_1]"]);
    }

    [Theory]
    [InlineData("meu cpf e 529.982.247-25")]
    [InlineData("cpf 52998224725 pode faturar")]
    public void Mascara_CPF_formatado_e_sem_formatacao(string frase)
    {
        var (texto, _) = EscudoDePii.Mascarar(frase);

        Assert.Contains("[CPF_1]", texto);
        Assert.DoesNotContain("52998224725", texto.Replace(".", "").Replace("-", ""));
    }

    [Fact]
    public void Numero_de_onze_digitos_que_nao_e_CPF_valido_nao_vira_CPF()
    {
        // CPF e celular tem onze digitos, e so o digito verificador desempata.
        // Errar aqui vaza o CPF ou apaga o telefone.
        var (texto, _) = EscudoDePii.Mascarar("meu zap e 11987654321");

        Assert.DoesNotContain("[CPF_", texto);
        Assert.Contains("[TEL_1]", texto);
    }

    [Theory]
    [InlineData("liga no (11) 98765-4321")]
    [InlineData("meu numero: +55 11 98765-4321")]
    [InlineData("chama no 11987654321")]
    public void Mascara_telefone_em_varios_formatos(string frase)
    {
        var (texto, _) = EscudoDePii.Mascarar(frase);
        Assert.Contains("[TEL_1]", texto);
    }

    [Fact]
    public void Mascara_CEP_e_endereco()
    {
        var (texto, _) = EscudoDePii.Mascarar("entrega na Rua das Flores, 123 - CEP 01310-100");

        Assert.Contains("[END_1]", texto);
        Assert.Contains("[CEP_1]", texto);
        Assert.DoesNotContain("Flores", texto);
    }

    [Fact]
    public void O_mesmo_valor_repetido_recebe_o_mesmo_marcador()
    {
        // O cliente que repete o telefone em tres falas precisa aparecer como
        // UMA pessoa: com marcadores diferentes, o modelo le tres pessoas e o
        // dossie inventa relacao entre elas.
        var (texto, mapa) = EscudoDePii.Mascarar(
            "liga no 11987654321, se nao atender tenta 11987654321 de novo");

        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(texto, @"\[TEL_1\]").Count);
        Assert.Single(mapa);
    }

    [Fact]
    public void Remontar_devolve_o_texto_original()
    {
        var original = "manda pro joao@empresa.com ou liga no (11) 98765-4321";
        var (mascarado, mapa) = EscudoDePii.Mascarar(original);

        Assert.Equal(original, EscudoDePii.Remontar(mascarado, mapa));
    }

    [Fact]
    public void Marcador_inventado_pelo_modelo_nao_vira_dado_de_outra_pessoa()
    {
        // O certo e sair com `[TEL_9]` visivel, que denuncia o problema, e nao
        // com o telefone de outro cliente no lugar.
        var (_, mapa) = EscudoDePii.Mascarar("liga no 11987654321");

        var resposta = EscudoDePii.Remontar("confirmar com [TEL_9]", mapa);

        Assert.Contains("[TEL_9]", resposta);
    }

    // ---------------------------------------------------------------------
    // O teste que FALHA O BUILD. E o criterio de aceite da issue.
    // ---------------------------------------------------------------------

    [Fact]
    public void Nada_do_seed_sai_com_dado_pessoal_apos_o_escudo()
    {
        // Roda sobre as conversas REAIS do seed, e nao sobre exemplo inventado
        // no proprio teste: exemplo escrito para passar passa sempre.
        var raiz = typeof(EscudoDePiiTeste).Assembly
            .GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;
        var fonte = FakeSource.DaPasta(Path.Combine(raiz, "seed", "conversas"));

        var vazamentos = new List<string>();
        foreach (var conversa in fonte.Conversas)
        {
            foreach (var m in fonte.Reproduzir(conversa.Id, DateTimeOffset.UtcNow))
            {
                // O que sairia para o modelo: o texto da fala E os telefones da
                // troca, que sao PII tanto quanto o corpo da mensagem.
                var carga = $"{m.De} {m.Para} {m.Texto}";
                var (mascarado, _) = EscudoDePii.Mascarar(carga);

                vazamentos.AddRange(DetectorDePii.Suspeito(mascarado)
                    .Select(v => $"{conversa.Id}/{m.ProviderMessageId}: {v}"));
            }
        }

        Assert.True(vazamentos.Count == 0,
            "PII saiu da rede sem mascara — isto e transferencia de dado pessoal "
            + "sob a LGPD, e por isso reprova o build:\n  "
            + string.Join("\n  ", vazamentos));
    }

    [Fact]
    public void O_detector_enxerga_PII_de_verdade()
    {
        // Sem isto, um erro no regex faria o teste acima passar sempre — verde
        // por nao ter olhado, que e o pior tipo de verde num gate de seguranca.
        var sujo = "joao@empresa.com, 11987654321, 529.982.247-25, 01310-100";

        var achados = DetectorDePii.Suspeito(sujo);

        Assert.Contains(achados, a => a.Contains("e-mail"));
        Assert.Contains(achados, a => a.Contains("sequencia numerica"));
    }

    [Fact]
    public void O_detector_e_independente_do_escudo()
    {
        // A razao de existir do `DetectorDePii`, e ela foi paga: enquanto o
        // gate reusava os padroes do escudo, desligar um padrao la deixava o
        // gate VERDE — ele parava de procurar exatamente o que o escudo parava
        // de mascarar. Gate que so acha o que o proprio codigo ja sabe procurar
        // nao e gate, e eco.
        //
        // Aqui a prova: um telefone escrito num formato que o ESCUDO nao pega
        // continua sendo acusado pelo DETECTOR.
        const string formatoExotico = "meu contato e 11 9 8 7 6 5 4 3 2 1";

        var (mascarado, _) = EscudoDePii.Mascarar(formatoExotico);

        Assert.NotEmpty(DetectorDePii.Suspeito(mascarado));
    }

    [Fact]
    public void O_marcador_do_proprio_escudo_nao_e_acusado()
    {
        // Senao o gate reprovaria o conserto.
        Assert.Empty(DetectorDePii.Suspeito("confirmar [TEL_1] e [EMAIL_2] com o cliente"));
    }
}
