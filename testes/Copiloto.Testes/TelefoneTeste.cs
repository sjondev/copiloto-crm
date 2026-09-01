using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// A normalizacao de telefone brasileiro (#22).
///
/// O estrago que ela evita e especifico: o mesmo cliente vira DOIS leads e o
/// historico se parte no meio — o vendedor abre a conversa e nao ve o que foi
/// combinado semana passada.
/// </summary>
public class TelefoneTeste
{
    [Theory]
    [InlineData("+55 11 98765-4321")]
    [InlineData("5511987654321")]
    [InlineData("11987654321")]
    [InlineData("(11) 98765-4321")]
    [InlineData("11 9 8765 4321")]
    [InlineData("  +55(11)98765.4321  ")]
    public void Formatos_diferentes_do_mesmo_numero_convergem(string bruto)
    {
        Assert.Equal("+5511987654321", Telefone.Normalizar(bruto)!.E164);
    }

    [Fact]
    public void Celular_sem_o_nono_digito_e_o_mesmo_cliente()
    {
        // O caso que parte o historico: agenda velha e cadastro antigo ainda
        // entregam o numero de oito digitos.
        var antigo = Telefone.Normalizar("11 8765-4321");
        var novo = Telefone.Normalizar("11 98765-4321");

        Assert.Equal(novo, antigo);
        Assert.Equal("+5511987654321", antigo!.E164);
    }

    [Theory]
    [InlineData("11 3123-4567")]   // fixo Sao Paulo
    [InlineData("21 2222-3333")]   // fixo Rio
    public void Fixo_nao_ganha_nono_digito(string bruto)
    {
        // Enfiar um 9 num fixo criaria um numero que nao existe.
        var t = Telefone.Normalizar(bruto)!;

        Assert.False(t.EhCelular);
        Assert.Equal(8, t.Assinante.Length);
    }

    [Fact]
    public void DDD_55_nao_e_confundido_com_o_DDI()
    {
        // Rio Grande do Sul tem DDD 55. `5555XXXXXXXX` e DDI+DDD; `55XXXXXXXXX`
        // sozinho e o DDD gaucho.
        var comDdi = Telefone.Normalizar("55 55 99999-8888")!;
        var semDdi = Telefone.Normalizar("55 99999-8888")!;

        Assert.Equal("55", comDdi.Ddd);
        Assert.Equal("55", semDdi.Ddd);
        Assert.Equal(comDdi, semDdi);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("11 1234-5678")]      // assinante comecando com 1
    [InlineData("09 98765-4321")]     // DDD invalido
    [InlineData("11 12345-6789")]     // nove digitos sem comecar com 9
    public void O_que_nao_e_telefone_brasileiro_devolve_null(string? bruto)
    {
        // Null e nao um Telefone com dado torto: devolver o torto so adiaria o
        // erro para uma camada onde ninguem sabe mais de onde ele veio.
        Assert.Null(Telefone.Normalizar(bruto));
    }

    [Fact]
    public void Telefones_iguais_sao_iguais_como_valor()
    {
        // E o que permite resolver o Lead por telefone sem comparar string crua.
        var a = Telefone.Normalizar("(11) 98765-4321")!;
        var b = Telefone.Normalizar("5511987654321")!;

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
