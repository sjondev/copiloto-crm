using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// A mesma pessoa falando por mais de um numero (#177).
///
/// O WhatsApp nao tem cadastro, entao a identidade pratica e o telefone — e o
/// cliente que troca de chip vira, hoje, um lead novo com o historico partido no
/// meio. A ficha fica no lead antigo, o dossie no novo, e o vendedor abre os
/// dois sem saber que sao a mesma pessoa.
/// </summary>
public class NumerosDoLeadTeste
{
    // Escritos como o vendedor digitaria; o Lead canoniza, e as asercoes
    // comparam com a forma canonica.
    private const string Antigo = "+55 11 98765-4321";
    private const string Novo = "+55 11 97777-1111";
    private const string AntigoE164 = "+5511987654321";
    private const string NovoE164 = "+5511977771111";
    private static readonly DateTimeOffset Agora = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static Lead NovoLead() => new(Guid.NewGuid(), Antigo, Agora);

    [Fact]
    public void O_lead_nasce_falando_por_um_numero_so()
    {
        var lead = NovoLead();

        Assert.Equal(AntigoE164, lead.Telefone);
        Assert.Equal([AntigoE164], lead.Numeros);
    }

    [Fact]
    public void O_numero_novo_entra_sem_tirar_o_antigo()
    {
        var lead = NovoLead();

        lead.TambemFalaPor(Novo);

        // O antigo FICA: a conversa que ja existe esta amarrada nele, e some-lo
        // trocaria dois leads partidos por um lead com historico faltando.
        Assert.Equal([AntigoE164, NovoE164], lead.Numeros);
    }

    [Fact]
    public void O_principal_nao_muda_quando_o_cliente_troca_de_chip()
    {
        var lead = NovoLead();

        lead.TambemFalaPor(Novo);

        Assert.Equal(AntigoE164, lead.Telefone);
    }

    [Fact]
    public void O_mesmo_numero_escrito_de_outro_jeito_nao_duplica()
    {
        var lead = NovoLead();

        lead.TambemFalaPor("5511987654321");

        Assert.Single(lead.Numeros);
    }

    [Fact]
    public void Repetir_o_numero_adicional_nao_duplica()
    {
        var lead = NovoLead();

        lead.TambemFalaPor(Novo);
        lead.TambemFalaPor(Novo);

        Assert.Equal(2, lead.Numeros.Count);
    }

    [Fact]
    public void Numero_invalido_e_recusado()
    {
        var lead = NovoLead();

        Assert.Throws<ArgumentException>(() => lead.TambemFalaPor("nao e telefone"));
    }

    [Fact]
    public void Reconhece_a_pessoa_por_qualquer_um_dos_numeros()
    {
        var lead = NovoLead();
        lead.TambemFalaPor(Novo);

        Assert.True(lead.FalaPor(Antigo));
        Assert.True(lead.FalaPor(Novo));
        Assert.True(lead.FalaPor("5511977771111"));
        Assert.False(lead.FalaPor("+55 11 96666-0000"));
    }

    // --- De onde veio o nome ---

    /// <summary>
    /// O nome que chega do WhatsApp foi digitado pelo PROPRIO cliente no perfil
    /// dele, e nao apurado pelo vendedor. A #88 separou fato de impressao na
    /// ficha pelo mesmo motivo: informacao sem procedencia entra no contexto com
    /// o mesmo peso de informacao conferida.
    /// </summary>
    [Fact]
    public void O_nome_guarda_de_onde_veio()
    {
        var lead = NovoLead();

        lead.Identificar("Marina", Lead.FontePerfilDoWhatsApp);

        Assert.Equal("Marina", lead.Nome);
        Assert.Equal(Lead.FontePerfilDoWhatsApp, lead.NomeFonte);
    }

    [Fact]
    public void Nome_dito_pelo_vendedor_nao_se_passa_por_perfil()
    {
        var lead = NovoLead();

        lead.Identificar("Marina Souza", "o vendedor anotou");

        Assert.Equal("o vendedor anotou", lead.NomeFonte);
    }

    [Fact]
    public void Nome_vazio_nao_apaga_o_que_ja_se_sabia()
    {
        var lead = NovoLead();
        lead.Identificar("Marina", Lead.FontePerfilDoWhatsApp);

        lead.Identificar("   ", "seja la o que for");

        Assert.Equal("Marina", lead.Nome);
        Assert.Equal(Lead.FontePerfilDoWhatsApp, lead.NomeFonte);
    }
}
