using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;

namespace Copiloto.Testes;

/// <summary>
/// O mapeamento (#103), conferido em SQLite na memoria.
///
/// Sem Postgres de pe: a suite roda offline e de graca por decisao do
/// CLAUDE.md, e teste que precisa de container para passar e teste que quebra
/// no primeiro clone. O que se prova aqui e o MAPEAMENTO — que o modelo fecha,
/// que as chaves e os indices existem e que o dominio sobrevive a ida e volta.
/// </summary>
public class PersistenciaTeste : BancoEmMemoria
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private CopilotoDbContext Novo() => NovoContexto();

    [Fact]
    public void O_modelo_fecha_e_o_esquema_e_criado()
    {
        // Se um mapeamento estiver errado, isto falha aqui e nao em producao.
        using var ctx = Novo();
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Lead)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Deal)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(AiInvocation)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Conversa)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Mensagem)));
    }

    [Fact]
    public void Lead_sobrevive_a_ida_e_volta()
    {
        var id = Guid.NewGuid();
        using (var ctx = Novo())
        {
            ctx.Leads.Add(new Lead(id, "+5511987654321", Agora, "Marina"));
            ctx.SaveChanges();
        }

        using (var ctx = Novo())
        {
            var lido = ctx.Leads.Single(l => l.Id == id);
            Assert.Equal("+5511987654321", lido.Telefone);
            Assert.Equal("Marina", lido.Nome);
        }
    }

    [Fact]
    public void Dois_leads_com_o_mesmo_telefone_nao_entram()
    {
        // O ponto da issue: a garantia nao pode viver so no codigo. Duas
        // instancias processando a mesma conversa criam dois leads antes de
        // qualquer `if` perceber.
        using var ctx = Novo();
        ctx.Leads.Add(new Lead(Guid.NewGuid(), "+5511987654321", Agora));
        ctx.SaveChanges();

        ctx.Leads.Add(new Lead(Guid.NewGuid(), "+5511987654321", Agora));

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void O_estagio_e_gravado_como_texto_e_nao_como_numero()
    {
        // Enum como int deixa o banco ilegivel e quebra em silencio se alguem
        // reordenar o enum — e reordenar enum e o tipo de mudanca que parece
        // inofensiva.
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Agora);
        deal.MoverPara(Estagio.Qualificacao, Agora);

        using (var ctx = Novo()) { ctx.Deals.Add(deal); ctx.SaveChanges(); }

        // Lido como TEXTO cru do banco, e nao pela entidade: pela entidade o
        // converter desfaria a gravacao e o teste passaria mesmo com int.
        // A tabela tem um registro so, entao dispensa WHERE — e dispensar e
        // melhor aqui, porque o SQLite guarda Guid como BLOB e comparar com
        // string nao casaria.
        using var leitura = Novo();
        var texto = leitura.Database
            .SqlQueryRaw<string>("SELECT Estagio AS Value FROM deals")
            .Single();

        Assert.Equal("Qualificacao", texto);
    }

    [Fact]
    public void O_custo_acumulado_e_as_invocacoes_voltam_juntos()
    {
        // O vinculo da #2 atravessando o banco: sem isso, "quanto custou fechar
        // este negocio?" viraria uma consulta que alguem escreve errado depois.
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Agora);
        deal.RegistrarInvocacao(new AiInvocation(Guid.NewGuid(), "fake", 0.15m, Agora, deal.Id));
        deal.RegistrarInvocacao(new AiInvocation(Guid.NewGuid(), "fake", 0.004m, Agora, deal.Id));

        using (var ctx = Novo()) { ctx.Deals.Add(deal); ctx.SaveChanges(); }

        using var leitura = Novo();
        var lido = leitura.Deals.Include(d => d.Invocacoes).Single(d => d.Id == deal.Id);

        Assert.Equal(2, lido.Invocacoes.Count);
        Assert.Equal(0.154m, lido.CustoIaAcumulado);
        Assert.Equal(lido.Invocacoes.Sum(i => i.CustoEmReais), lido.CustoIaAcumulado);
    }

    [Fact]
    public void Centavo_fracionado_nao_e_arredondado_no_caminho()
    {
        // Uma invocacao custa fracao de centavo. Arredondar cada uma para duas
        // casas faria o acumulado divergir da soma, que e o que o teste da #2
        // confere — e a divergencia so apareceria depois de milhares delas.
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Agora);
        deal.RegistrarInvocacao(new AiInvocation(Guid.NewGuid(), "fake", 0.000125m, Agora, deal.Id));

        using (var ctx = Novo()) { ctx.Deals.Add(deal); ctx.SaveChanges(); }

        using var leitura = Novo();
        Assert.Equal(0.000125m, leitura.Deals.Single(d => d.Id == deal.Id).CustoIaAcumulado);
    }

    [Fact]
    public void A_conversa_volta_com_as_mensagens_em_ordem()
    {
        var conversa = new Conversa(Guid.NewGuid(), Guid.NewGuid());
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "vou pensar", Agora.AddMinutes(5)));
        conversa.Registrar(new Mensagem(Guid.NewGuid(), Autor.Cliente, "qual o valor?", Agora));

        using (var ctx = Novo()) { ctx.Conversas.Add(conversa); ctx.SaveChanges(); }

        using var leitura = Novo();
        var lida = leitura.Conversas.Include(c => c.Mensagens).Single(c => c.Id == conversa.Id);

        Assert.Equal(2, lida.Mensagens.Count);
        Assert.Equal("qual o valor?", lida.Mensagens.OrderBy(m => m.EnviadaEm).First().Texto);
    }

    [Fact]
    public void O_indice_unico_do_telefone_existe_com_nome()
    {
        // Pelo nome, para que renomear seja decisao consciente: indice sem nome
        // ganha um gerado, e o proximo diff de migration fica ilegivel.
        using var ctx = Novo();
        var indice = ctx.Model.FindEntityType(typeof(Lead))!
            .GetIndexes().Single(i => i.IsUnique);

        Assert.Equal("ux_leads_telefone", indice.GetDatabaseName());
    }
}
