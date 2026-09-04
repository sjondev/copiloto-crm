using Copiloto.Api.Persistencia;
using Fichas = Copiloto.Dominio.Fichas;

namespace Copiloto.Testes;

/// <summary>A Ficha do Cliente atravessando o banco (#86).</summary>
public class FichaNoBancoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_ficha_volta_com_os_campos_preenchidos()
    {
        var id = Guid.NewGuid();
        using (var ctx = NovoContexto())
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0,
                empresa: new Fichas.SobreAEmpresa(Ramo: Fichas.Anotacao.Fato("cafeteria"), Porte: Fichas.Anotacao.Fato("3 lojas")),
                pessoa: new Fichas.SobreAPessoa(Cargo: Fichas.Anotacao.Fato("sócio")));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = NovoContexto();
        var lida = leitura.Fichas.Single(f => f.Id == id);

        Assert.Equal("cafeteria", lida.Empresa.Ramo!.Valor);
        Assert.Equal("3 lojas", lida.Empresa.Porte!.Valor);
        Assert.Equal("sócio", lida.Pessoa.Cargo!.Valor);
        Assert.False(lida.EstaVazia);
    }

    [Fact]
    public void O_historico_sobrevive_ao_banco()
    {
        // "Ele era o decisor e agora nao e" so vale se durar mais que a sessao.
        var id = Guid.NewGuid();
        using (var ctx = NovoContexto())
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0, pessoa: new Fichas.SobreAPessoa(PapelNaDecisao: Fichas.Anotacao.Fato("decisor")));
            ficha.Atualizar(T0.AddDays(2), pessoa: new Fichas.SobreAPessoa(PapelNaDecisao: Fichas.Anotacao.Fato("influenciador")));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = NovoContexto();
        var lida = leitura.Fichas.Single(f => f.Id == id);

        Assert.Equal(2, lida.Historico.Count);
        Assert.Equal("decisor", lida.Historico[0].Pessoa.PapelNaDecisao!.Valor);
    }

    [Fact]
    public void Ficha_vazia_e_gravavel()
    {
        // O sistema funciona sem ela, e "funciona" inclui salvar.
        using var ctx = NovoContexto();
        ctx.Fichas.Add(new Fichas.FichaCliente(Guid.NewGuid(), Guid.NewGuid(), T0));

        ctx.SaveChanges();

        Assert.True(ctx.Fichas.Single().EstaVazia);
    }

    [Fact]
    public void A_natureza_da_anotacao_sobrevive_ao_banco()
    {
        // O conversor JSON e' quem reconstroi a Anotacao na leitura, e se ele
        // errasse a natureza a impressao voltaria do banco como FATO — sem
        // erro, sem log, e ancorando preco na proxima analise.
        var id = Guid.NewGuid();
        using (var ctx = NovoContexto())
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0, pessoa: new Fichas.SobreAPessoa(
                Cargo: Fichas.Anotacao.Fato("sócio", "LinkedIn"),
                EstiloObservado: Fichas.Anotacao.Impressao("parece desconfiado", T0)));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = NovoContexto();
        var lida = leitura.Fichas.Single(f => f.Id == id);

        Assert.True(lida.Pessoa.Cargo!.EhFato);
        Assert.Equal("LinkedIn", lida.Pessoa.Cargo!.Fonte);
        Assert.False(lida.Pessoa.EstiloObservado!.EhFato);
        Assert.Equal(T0, lida.Pessoa.EstiloObservado!.Quando);
        Assert.Single(lida.Impressoes);
    }

    [Fact]
    public void Um_lead_nao_tem_duas_fichas()
    {
        // Duas seriam duas versoes da verdade sem criterio de desempate.
        var lead = Guid.NewGuid();
        using var ctx = NovoContexto();
        ctx.Fichas.Add(new Fichas.FichaCliente(Guid.NewGuid(), lead, T0));
        ctx.SaveChanges();

        ctx.Fichas.Add(new Fichas.FichaCliente(Guid.NewGuid(), lead, T0));

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }
}
