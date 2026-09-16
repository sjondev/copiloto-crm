using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Vendas;

namespace Copiloto.Testes;

/// <summary>
/// O resolvedor sobre o banco (#103): a resolucao de Lead da #22 atravessando
/// a persistencia, que e onde ela passa a valer entre reinicios.
/// </summary>
public class ResolvedorSobreBancoTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Empresa = "+55 11 3333-4444";

    [Fact]
    public void O_lead_criado_hoje_e_encontrado_depois_do_restart()
    {
        // O que faltava para ser CRM: sem isto, o vendedor abre a tela e a
        // conversa de ontem nao esta la.
        Guid id;
        using (var ctx = NovoContexto())
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            id = r.Resolver(Telefone.Normalizar("11 98765-4321")!, Agora).Id;
        }

        // Contexto novo = processo novo, para o efeito deste teste.
        using (var ctx = NovoContexto())
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            var denovo = r.Resolver(Telefone.Normalizar("(11) 98765-4321")!, Agora);

            Assert.Equal(id, denovo.Id);
            Assert.Equal(1, r.LeadsConhecidos);
        }
    }

    /// <summary>
    /// O cliente que troca de chip (#177), atravessando o banco.
    ///
    /// A busca por numero adicional le uma COLECAO, e nao uma coluna comum — se
    /// a traducao do EF nao funcionar, o teste em memoria passa e este reprova.
    /// E' por isso que ele existe separado.
    /// </summary>
    [Fact]
    public void O_cliente_que_trocou_de_numero_continua_sendo_o_mesmo_lead()
    {
        Guid id;
        using (var ctx = NovoContexto())
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            var lead = r.Resolver(Telefone.Normalizar("11 98765-4321")!, Agora);
            lead.TambemFalaPor("11 97777-1111");

            ctx.SaveChanges();
            id = lead.Id;
        }

        using (var ctx = NovoContexto())
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            var pelaLinhaNova = r.Resolver(Telefone.Normalizar("11 97777-1111")!, Agora);

            Assert.Equal(id, pelaLinhaNova.Id);
            Assert.Equal(1, ctx.Leads.Count());
        }
    }

    [Fact]
    public void O_numero_sem_o_nono_digito_acha_o_lead_que_ja_esta_no_banco()
    {
        // A #22 atravessando o banco: normalizar no codigo so serve se a busca
        // tambem for pelo normalizado.
        using var ctx = NovoContexto();
        var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));

        var a = r.Resolver(Telefone.Normalizar("11 98765-4321")!, Agora);
        var b = r.Resolver(Telefone.Normalizar("11 8765-4321")!, Agora);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, ctx.Leads.Count());
    }
}

