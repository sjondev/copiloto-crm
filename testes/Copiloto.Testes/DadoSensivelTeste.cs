using Copiloto.Api.Seguranca;
using Copiloto.Dominio.Seguranca;

namespace Copiloto.Testes;

/// <summary>
/// Dado sensivel que chega sozinho na conversa (#82).
///
/// As falas plantadas aqui sao do cenario de cafe, porque e ali que o problema
/// nasce: ninguem PERGUNTA sobre saude — o cliente conta, porque quer saber se
/// pode tomar o produto.
/// </summary>
public class DadoSensivelTeste
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("to com refluxo, posso tomar cafe?", CategoriaSensivel.Saude)]
    [InlineData("minha esposa está grávida, o descafeinado serve?", CategoriaSensivel.Saude)]
    [InlineData("não bebo por questão de fé", CategoriaSensivel.ConviccaoReligiosa)]
    [InlineData("é para o jejum da igreja", CategoriaSensivel.ConviccaoReligiosa)]
    [InlineData("compro do sindicato dos professores", CategoriaSensivel.FiliacaoSindical)]
    [InlineData("meu médico proibiu cafeína, tenho gastrite", CategoriaSensivel.Saude)]
    public void Detecta_o_indicio_na_fala(string fala, CategoriaSensivel esperada)
    {
        var indicios = DadoSensivel.Detectar(fala);

        Assert.NotEmpty(indicios);
        Assert.Contains(indicios, i => i.Categoria == esperada);
    }

    [Theory]
    [InlineData("me vê um café preto, por favor")]
    [InlineData("o pote da direita, o de torra escura")]
    [InlineData("vi voces no marketing digital de voces")]
    [InlineData("tenho fé em vocês, mandem logo")]
    [InlineData("qual o valor do kg do bourbon amarelo?")]
    public void Conversa_comum_de_cafeteria_nao_dispara(string fala)
    {
        // Detector que marca metade das conversas nao protege ninguem: ele e
        // desligado na primeira semana, e ai nao protege mais nada.
        Assert.Empty(DadoSensivel.Detectar(fala));
    }

    [Fact]
    public void Dado_sensivel_nunca_entra_no_indice()
    {
        // Indice e o pior destino: o dado deixa de estar numa conversa e passa
        // a ser RECUPERAVEL por semelhanca, aparecendo na analise de outro
        // cliente sem ninguem ter pedido (#62).
        Assert.False(DadoSensivel.PodeIndexar("to com refluxo, posso tomar cafe?"));
        Assert.True(DadoSensivel.PodeIndexar("me vê 2kg do bourbon amarelo"));
    }

    [Fact]
    public void O_trecho_sensivel_nao_chega_ao_modelo_mas_o_pedido_chega()
    {
        // Descartar a fala inteira seria perder a venda junto com o dado: o
        // pedido esta na segunda metade da frase.
        var paraOModelo = DadoSensivel.ForaDoContextoDeSugestao(
            "to com refluxo, posso tomar cafe?");

        Assert.DoesNotContain("refluxo", paraOModelo);
        Assert.Contains("posso tomar cafe?", paraOModelo);
        Assert.Contains("[SENSIVEL:Saude]", paraOModelo);
    }

    [Fact]
    public void A_conviccao_religiosa_nao_calibra_a_sugestao()
    {
        // O criterio que mais importa eticamente: usar "nao bebo por questao de
        // fe" para escolher o angulo da venda e o uso que a lei trata com rigor
        // — e a garantia nao e o prompt pedir, e o trecho nao estar la.
        var paraOModelo = DadoSensivel.ForaDoContextoDeSugestao(
            "não bebo por questão de fé, mas quero presentear meu sócio");

        Assert.DoesNotContain("questão de fé", paraOModelo);
        Assert.Contains("quero presentear meu sócio", paraOModelo);
    }

    [Fact]
    public void Fala_sem_nada_sensivel_atravessa_intacta()
    {
        const string fala = "me vê 3kg do bourbon, pra durar";

        Assert.Equal(fala, DadoSensivel.ForaDoContextoDeSugestao(fala));
    }

    [Fact]
    public void A_fala_ja_entra_no_contexto_sem_o_trecho_sensivel()
    {
        // A moldura e o unico caminho da fala do cliente ate o modelo, entao a
        // limpeza mora la — nao em quem chama.
        var (bloco, _) = MolduraDeContexto.Montar("to com refluxo, posso tomar cafe?");

        Assert.DoesNotContain("refluxo", bloco);
        Assert.Contains("[SENSIVEL:Saude]", bloco);
        Assert.Contains("posso tomar cafe?", bloco);
    }

    [Fact]
    public void A_retencao_e_mais_curta_que_a_da_conversa()
    {
        // Dado que ninguem pediu, e que a empresa nao tem finalidade para usar,
        // nao pode ficar pelo prazo do que ela pediu.
        Assert.True(DadoSensivel.Retencao < TimeSpan.FromDays(90));
        Assert.False(DadoSensivel.DeveExpurgar(T0, T0.AddDays(29)));
        Assert.True(DadoSensivel.DeveExpurgar(T0, T0.AddDays(30)));
    }

    [Fact]
    public void Cliente_falante_na_mesma_conversa_nao_vira_alerta()
    {
        // Cinco mencoes de uma pessoa so e uma pessoa so.
        var incidencia = new IncidenciaDeDadoSensivel();
        var conversa = Guid.NewGuid();

        for (var i = 0; i < 5; i++) incidencia.Registrar(conversa, T0.AddMinutes(i));

        Assert.False(incidencia.DeveAlertarGestor(T0));
    }

    [Fact]
    public void Cinco_conversas_na_semana_alertam_o_gestor()
    {
        // Aqui nao e coincidencia: e formulario, roteiro ou campanha pedindo
        // informacao que a empresa nao precisa.
        var incidencia = new IncidenciaDeDadoSensivel();
        for (var i = 0; i < 5; i++) incidencia.Registrar(Guid.NewGuid(), T0.AddHours(i));

        Assert.True(incidencia.DeveAlertarGestor(T0.AddHours(5)));
        Assert.Contains("revisar por onde essa pergunta", incidencia.Alerta(T0.AddHours(5)));
    }

    [Fact]
    public void Fora_da_janela_a_contagem_nao_acumula_para_sempre()
    {
        var incidencia = new IncidenciaDeDadoSensivel();
        for (var i = 0; i < 5; i++) incidencia.Registrar(Guid.NewGuid(), T0);

        Assert.False(incidencia.DeveAlertarGestor(T0.AddDays(8)));
    }
}
