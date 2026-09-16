using Copiloto.Api.Ingestao;
using Copiloto.Api.Persistencia;
using Copiloto.Dominio.Conversas;
using Copiloto.Dominio.Ia;
using Copiloto.Dominio.Vendas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Fichas = Copiloto.Dominio.Fichas;

namespace Copiloto.Testes;

/// <summary>
/// O mapeamento (#103), conferido em SQLite na memoria.
///
/// Sem Postgres de pe: a suite roda offline e de graca por decisao do
/// CLAUDE.md, e teste que precisa de container para passar e teste que quebra
/// no primeiro clone. O que se prova aqui e o MAPEAMENTO — que o modelo fecha,
/// que as chaves e os indices existem e que o dominio sobrevive a ida e volta.
/// </summary>
public class PersistenciaTeste : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public PersistenciaTeste()
    {
        // Conexao aberta segurando o banco: fechada, o SQLite em memoria some.
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();

        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>()
            .UseSqlite(_conexao)
            .Options;

        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    private CopilotoDbContext Novo() => new(_opcoes);

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

/// <summary>
/// O resolvedor sobre o banco (#103): a resolucao de Lead da #22 atravessando
/// a persistencia, que e onde ela passa a valer entre reinicios.
/// </summary>
public class ResolvedorSobreBancoTeste : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Empresa = "+55 11 3333-4444";

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public ResolvedorSobreBancoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    [Fact]
    public void O_lead_criado_hoje_e_encontrado_depois_do_restart()
    {
        // O que faltava para ser CRM: sem isto, o vendedor abre a tela e a
        // conversa de ontem nao esta la.
        Guid id;
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            id = r.Resolver(Telefone.Normalizar("11 98765-4321")!, Agora).Id;
        }

        // Contexto novo = processo novo, para o efeito deste teste.
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));
            var denovo = r.Resolver(Telefone.Normalizar("(11) 98765-4321")!, Agora);

            Assert.Equal(id, denovo.Id);
            Assert.Equal(1, r.LeadsConhecidos);
        }
    }

    [Fact]
    public void O_numero_sem_o_nono_digito_acha_o_lead_que_ja_esta_no_banco()
    {
        // A #22 atravessando o banco: normalizar no codigo so serve se a busca
        // tambem for pelo normalizado.
        using var ctx = new CopilotoDbContext(_opcoes);
        var r = new ResolvedorDeLead(Empresa, new LeadsNoBanco(ctx));

        var a = r.Resolver(Telefone.Normalizar("11 98765-4321")!, Agora);
        var b = r.Resolver(Telefone.Normalizar("11 8765-4321")!, Agora);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, ctx.Leads.Count());
    }
}

/// <summary>A Ficha do Cliente atravessando o banco (#86).</summary>
public class FichaNoBancoTeste : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _conexao;
    private readonly DbContextOptions<CopilotoDbContext> _opcoes;

    public FichaNoBancoTeste()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        _opcoes = new DbContextOptionsBuilder<CopilotoDbContext>().UseSqlite(_conexao).Options;
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    [Fact]
    public void A_ficha_volta_com_os_campos_preenchidos()
    {
        var id = Guid.NewGuid();
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0,
                empresa: new Fichas.SobreAEmpresa(Ramo: Fichas.Anotacao.Fato("cafeteria"), Porte: Fichas.Anotacao.Fato("3 lojas")),
                pessoa: new Fichas.SobreAPessoa(Cargo: Fichas.Anotacao.Fato("sócio")));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);
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
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0, pessoa: new Fichas.SobreAPessoa(PapelNaDecisao: Fichas.Anotacao.Fato("decisor")));
            ficha.Atualizar(T0.AddDays(2), pessoa: new Fichas.SobreAPessoa(PapelNaDecisao: Fichas.Anotacao.Fato("influenciador")));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);
        var lida = leitura.Fichas.Single(f => f.Id == id);

        Assert.Equal(2, lida.Historico.Count);
        Assert.Equal("decisor", lida.Historico[0].Pessoa.PapelNaDecisao!.Valor);
    }

    [Fact]
    public void Ficha_vazia_e_gravavel()
    {
        // O sistema funciona sem ela, e "funciona" inclui salvar.
        using var ctx = new CopilotoDbContext(_opcoes);
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
        using (var ctx = new CopilotoDbContext(_opcoes))
        {
            var ficha = new Fichas.FichaCliente(id, Guid.NewGuid(), T0);
            ficha.Atualizar(T0, pessoa: new Fichas.SobreAPessoa(
                Cargo: Fichas.Anotacao.Fato("sócio", "LinkedIn"),
                EstiloObservado: Fichas.Anotacao.Impressao("parece desconfiado", T0)));
            ctx.Fichas.Add(ficha);
            ctx.SaveChanges();
        }

        using var leitura = new CopilotoDbContext(_opcoes);
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
        using var ctx = new CopilotoDbContext(_opcoes);
        ctx.Fichas.Add(new Fichas.FichaCliente(Guid.NewGuid(), lead, T0));
        ctx.SaveChanges();

        ctx.Fichas.Add(new Fichas.FichaCliente(Guid.NewGuid(), lead, T0));

        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }
}
