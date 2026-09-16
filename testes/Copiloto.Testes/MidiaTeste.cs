using System.Reflection;
using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Dossies;
using Copiloto.Dominio.Vendas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// Midia na Fase 1 (#23): nao se transcreve nada, e nada some.
///
/// O erro que estes testes existem para impedir e o mais caro do produto — o
/// dossie ler metade da conversa e afirmar com confianca inteira, porque o
/// audio de dois minutos do cliente nao deixou rastro nenhum.
/// </summary>
public class MidiaTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Mensagem Audio(TimeSpan? duracao = null, string texto = "") =>
        new(Guid.NewGuid(), Autor.Cliente, texto, Agora, new Midia(TipoDeMidia.Audio, duracao));

    [Fact]
    public void Audio_sem_texto_entra_dizendo_que_nao_foi_transcrito()
    {
        // "[audio]" pareceria um audio que o sistema ouviu. O marcador diz o
        // que NAO foi feito.
        Assert.Equal("[audio nao transcrito]", Audio().Texto);
    }

    [Fact]
    public void Marcador_leva_a_duracao_quando_ela_e_conhecida()
    {
        // Muda a decisao do vendedor: 4 segundos e um "ok", dois minutos e a
        // objecao inteira.
        Assert.Equal("[audio nao transcrito, 14s]", Audio(TimeSpan.FromSeconds(14)).Texto);
    }

    [Fact]
    public void Texto_do_provedor_ganha_do_marcador()
    {
        // Se o provedor mandou legenda ou transcricao propria, ela vale mais
        // que o nosso marcador — mas a midia continua registrada como lacuna.
        var comLegenda = Audio(texto: "vou pensar melhor");

        Assert.Equal("vou pensar melhor", comLegenda.Texto);
        Assert.True(comLegenda.NaoInterpretada);
    }

    [Fact]
    public void Fala_sem_texto_e_sem_midia_continua_nao_existindo()
    {
        Assert.Throws<ArgumentException>(
            () => new Mensagem(Guid.NewGuid(), Autor.Cliente, "  ", Agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("texto")]
    public void Texto_puro_nao_tem_midia(string? tipo) => Assert.Null(Midia.PeloNome(tipo));

    [Theory]
    [InlineData("audio", TipoDeMidia.Audio)]
    [InlineData("imagem", TipoDeMidia.Imagem)]
    [InlineData("document", TipoDeMidia.Documento)]
    public void Tipo_do_provedor_vira_tipo_de_casa(string nome, TipoDeMidia esperado) =>
        Assert.Equal(esperado, Midia.PeloNome(nome)!.Tipo);

    [Fact]
    public void Tipo_que_nao_conhecemos_vira_lacuna_e_nao_texto()
    {
        // O provedor inventa tipo novo sem avisar. Tratar o desconhecido como
        // texto faria a fala entrar no dossie como se tivesse sido lida.
        var figurinha = Midia.PeloNome("sticker");

        Assert.Equal(TipoDeMidia.Outro, figurinha!.Tipo);
        Assert.Equal("[midia nao interpretada]", figurinha.Marcador);
    }

    [Fact]
    public void Dossie_declara_uma_lacuna_por_tipo_e_nao_uma_por_mensagem()
    {
        // Sete audios viram uma linha dizendo sete. Sete linhas iguais
        // empurrariam as lacunas de verdade, sobre o cliente, para fora da tela.
        var dossie = new Dossie(Guid.NewGuid(), Guid.NewGuid(), Agora);

        dossie.DeclararMidiaNaoInterpretada([
            Audio(), Audio(), Audio(),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "", Agora, new Midia(TipoDeMidia.Imagem)),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", Agora),
        ]);

        Assert.Equal(2, dossie.Lacunas.Count);
        Assert.Contains(dossie.Lacunas, l => l.StartsWith("3 audios nao foram transcritos"));
        Assert.Contains(dossie.Lacunas, l => l.StartsWith("1 imagem nao foi interpretada"));
    }

    [Fact]
    public void Dossie_sem_midia_nao_inventa_lacuna()
    {
        var dossie = new Dossie(Guid.NewGuid(), Guid.NewGuid(), Agora);

        dossie.DeclararMidiaNaoInterpretada([
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", Agora)]);

        Assert.Empty(dossie.Lacunas);
    }

    [Fact]
    public void Midia_no_meio_da_conversa_nao_quebra_a_montagem_de_contexto()
    {
        // O criterio explicito da #23. O agrupador junta balao com marcador
        // igual junta balao com texto — a fala continua uma so.
        var baloes = new[]
        {
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "olha so", Agora),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "", Agora.AddSeconds(2),
                new Midia(TipoDeMidia.Audio, TimeSpan.FromSeconds(14))),
            new Mensagem(Guid.NewGuid(), Autor.Cliente, "deu pra entender?", Agora.AddSeconds(5)),
        };

        var fala = Assert.Single(AgrupadorDeFalas.Agrupar(baloes));

        Assert.Equal(3, fala.Baloes.Count);
        Assert.Contains("[audio nao transcrito, 14s]", fala.Texto);
    }

    [Fact]
    public void Replay_do_seed_nao_perde_mais_o_tipo_da_midia()
    {
        // O seed ja marcava "tipo": "audio" e o FakeSource descartava — que era
        // exatamente o "fingir que a midia nao existe" desta issue.
        var raiz = typeof(MidiaTeste).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RaizDoRepositorio").Value!;

        var falas = FakeSource.DaPasta(Path.Combine(raiz, "seed", "conversas"))
            .Reproduzir("esfria-e-some", Agora)
            .ToList();

        var comAudio = Assert.Single(falas, f => f.Midia is not null);
        Assert.Equal(TipoDeMidia.Audio, comAudio.Midia!.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(14), comAudio.Midia.Duracao);
    }

    [Fact]
    public void Payload_sem_texto_e_sem_midia_nao_entra_na_fila()
    {
        var vazia = new MensagemRecebida("wamid.1", "+5511988887777", "+551133334444", " ", Agora);

        Assert.Contains("nao ha fala nenhuma", vazia.PorQueNaoEntra());
    }

    [Fact]
    public void Payload_so_com_midia_entra()
    {
        // Audio sem legenda e o caso comum, e recusa-lo perderia a fala inteira.
        var soAudio = new MensagemRecebida(
            "wamid.1", "+5511988887777", "+551133334444", "", Agora, new Midia(TipoDeMidia.Audio));

        Assert.Null(soAudio.PorQueNaoEntra());
    }

    [Fact]
    public void A_midia_sobrevive_a_ida_e_volta_do_banco()
    {
        // O objeto de valor entra decomposto em duas colunas e volta montado. E
        // a parte que so o banco prova: o construtor que o EF usa nao e o mesmo
        // que o dominio usa, e um erro ali some da suite inteira e aparece em
        // producao como audio que virou texto puro.
        using var conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();

        var opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(conexao).Options;
        using (var criacao = new CopilotoDbContext(opcoes)) criacao.Database.EnsureCreated();

        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "olha so", Agora));
        conversa.Registrar(Audio(TimeSpan.FromSeconds(14)));

        using (var escrita = new CopilotoDbContext(opcoes))
        {
            escrita.Add(new Lead(conversa.LeadId, "+5511988887777", Agora));
            escrita.Add(conversa);
            escrita.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(opcoes);
        var lida = leitura.Conversas.Include(c => c.Mensagens).Single(c => c.Id == conversa.Id);

        var audio = Assert.Single(lida.Mensagens, m => m.NaoInterpretada);
        Assert.Equal(TipoDeMidia.Audio, audio.Midia!.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(14), audio.Midia.Duracao);
        Assert.Equal("[audio nao transcrito, 14s]", audio.Texto);
    }
}
