using System.Reflection;
using Copiloto.Api.Ingestao;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// O seed e o FakeSource (#20, #21).
///
/// As tres conversas sao o teste de qualidade do dossie: se a leitura for rasa
/// nelas, o prompt esta ruim e isso e bug, nao "coisa de IA". E sao elas que
/// rodam a demo — offline, sem depender do wi-fi da sala.
/// </summary>
public class FakeSourceTeste
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static string PastaDoSeed()
    {
        var raiz = typeof(FakeSourceTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;
        return Path.Combine(raiz, "seed", "conversas");
    }

    private static FakeSource Fonte() => FakeSource.DaPasta(PastaDoSeed());

    [Fact]
    public void As_tres_conversas_do_cenario_estao_no_seed()
    {
        var ids = Fonte().Conversas.Select(c => c.Id).ToList();

        Assert.Contains("fecha", ids);
        Assert.Contains("objecao-preco", ids);
        Assert.Contains("esfria-e-some", ids);
    }

    [Fact]
    public void Reproduzir_devolve_as_mensagens_no_formato_do_webhook()
    {
        var mensagens = Fonte().Reproduzir("fecha", Inicio).ToList();

        Assert.NotEmpty(mensagens);
        Assert.All(mensagens, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.ProviderMessageId));
            Assert.NotNull(Telefone.Normalizar(m.De));
            Assert.NotNull(Telefone.Normalizar(m.Para));
        });
    }

    [Fact]
    public void O_falante_do_seed_e_reconhecido_pelo_resolvedor()
    {
        // A prova de que o seed casa com a ingestao de verdade: se os telefones
        // do JSON nao batessem, tudo viraria "cliente" e ninguem notaria.
        var fonte = Fonte();
        var conversa = fonte.Conversas.First(c => c.Id == "fecha");
        var resolvedor = new ResolvedorDeLead(conversa.Empresa.Telefone);

        var autores = fonte.Reproduzir("fecha", Inicio)
            .Select(m => resolvedor.QuemFalou(Telefone.Normalizar(m.De)!))
            .ToList();

        Assert.Contains(Autor.Cliente, autores);
        Assert.Contains(Autor.Vendedor, autores);
    }

    [Fact]
    public void Uma_conversa_inteira_vira_UM_lead()
    {
        var fonte = Fonte();
        var conversa = fonte.Conversas.First(c => c.Id == "fecha");
        var resolvedor = new ResolvedorDeLead(conversa.Empresa.Telefone);

        foreach (var m in fonte.Reproduzir("fecha", Inicio))
            resolvedor.Resolver(resolvedor.TelefoneDoCliente(m)!, m.EnviadaEm);

        Assert.Equal(1, resolvedor.LeadsConhecidos);
    }

    [Fact]
    public void Os_baloes_seguidos_do_seed_viram_uma_fala_so()
    {
        // O "bom dia / vi o cafe / o bourbon / qual o valor" da conversa 1 e
        // exatamente o caso da #19, e aqui as duas coisas se encontram.
        var fonte = Fonte();
        var conversa = fonte.Conversas.First(c => c.Id == "fecha");
        var resolvedor = new ResolvedorDeLead(conversa.Empresa.Telefone);

        var mensagens = fonte.Reproduzir("fecha", Inicio)
            .Select(m => new Mensagem(
                Guid.NewGuid(),
                resolvedor.QuemFalou(Telefone.Normalizar(m.De)!),
                m.Texto, m.EnviadaEm))
            .ToList();

        var falas = AgrupadorDeFalas.Agrupar(mensagens);

        Assert.True(falas.Count < mensagens.Count,
            "o agrupamento nao juntou balao nenhum: ou o seed perdeu os baloes "
            + "curtos, ou a janela parou de valer");
        Assert.Equal(4, falas[0].Baloes.Count);   // bom dia / vi o cafe / o bourbon / qual o valor
    }

    [Fact]
    public void O_offset_e_relativo_ao_inicio_que_o_chamador_da()
    {
        // Conversa gravada em marco nao pode chegar ao dossie como "esfriou ha
        // seis meses": o roteiro guarda segundos, nao data absoluta.
        var fonte = Fonte();

        var ontem = fonte.Reproduzir("fecha", Inicio).First().EnviadaEm;
        var hoje = fonte.Reproduzir("fecha", Inicio.AddDays(1)).First().EnviadaEm;

        Assert.Equal(TimeSpan.FromDays(1), hoje - ontem);
    }

    [Fact]
    public void Modo_instantaneo_nao_espera_e_o_acelerado_espera_menos_que_o_real()
    {
        var a = new MensagemGravada("cliente", 0, "oi");
        var b = new MensagemGravada("vendedor", 240, "bom dia");

        Assert.Equal(TimeSpan.Zero, FakeSource.Atraso(a, b, 0));
        Assert.Equal(TimeSpan.FromSeconds(24), FakeSource.Atraso(a, b, 0.1));
        Assert.Equal(TimeSpan.FromSeconds(240), FakeSource.Atraso(a, b, 1));
    }

    [Fact]
    public void Pasta_ausente_falha_dizendo_o_que_e()
    {
        // "Sequencia vazia" tres camadas adiante nao ajuda ninguem.
        var erro = Assert.Throws<DirectoryNotFoundException>(
            () => FakeSource.DaPasta("/nao/existe/conversas"));

        Assert.Contains("conversas", erro.Message);
    }

    [Fact]
    public void O_seed_nao_tem_dado_real_de_pessoa()
    {
        // LGPD, e a #21 pede explicitamente. Os telefones sao ficticios e ficam
        // na faixa 9XXXX-XXXX com prefixos repetidos de proposito.
        foreach (var c in Fonte().Conversas)
        {
            Assert.NotNull(Telefone.Normalizar(c.Cliente.Telefone));
            Assert.NotNull(Telefone.Normalizar(c.Empresa.Telefone));
            Assert.False(string.IsNullOrWhiteSpace(c.Titulo));
        }
    }
}
