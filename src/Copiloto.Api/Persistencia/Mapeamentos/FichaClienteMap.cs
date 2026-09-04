using System.Text.Json;
using Copiloto.Dominio.Fichas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class FichaClienteMap : IEntityTypeConfiguration<FichaCliente>
{
    private static readonly JsonSerializerOptions Json =
        new() { Converters = { new AnotacaoJson() } };

    public void Configure(EntityTypeBuilder<FichaCliente> e)
    {
        e.ToTable("fichas_cliente");
        e.HasKey(f => f.Id);
        e.Property(f => f.LeadId).IsRequired();
        e.Property(f => f.CriadaEm).IsRequired();
        e.Property(f => f.AtualizadaEm).IsRequired();

        // Uma ficha por lead: a ficha E o que se sabe daquele cliente, e duas
        // seriam duas versoes da verdade sem criterio de desempate.
        e.HasIndex(f => f.LeadId).IsUnique().HasDatabaseName("ux_fichas_lead");

        // Os tres blocos e o historico vao como JSON, por conversor.
        //
        // Os blocos ERAM `OwnsOne` com quatro colunas de texto cada, e a #88
        // derrubou isso: cada campo virou `Anotacao` (valor + natureza + fonte +
        // quando), e o EF nao consegue ligar parametro de construtor a um owned
        // aninhado — "No suitable constructor was found for entity type
        // 'SobreAEmpresa'".
        //
        // As duas saidas eram achatar `Anotacao` em quatro colunas por campo
        // (48 colunas na ficha) ou serializar o bloco. A primeira e o dominio se
        // curvando ao ORM, e o dominio e' quem tem razao para existir. O
        // conversor paga o preco no mapeamento, que e' o lugar certo para pagar
        // — foi o mesmo raciocinio que o historico ja tinha feito aqui.
        //
        // O que isso custa: nao da para consultar "fichas cujo ramo e cafeteria"
        // por indice. Ninguem consulta — a ficha e sempre lida pelo lead, e a
        // busca por conteudo de ficha nao existe em nenhuma issue aberta.
        var (deEmpresa, comparaEmpresa) = ComoJson<SobreAEmpresa>();
        var (dePessoa, comparaPessoa) = ComoJson<SobreAPessoa>();
        var (deNegocio, comparaNegocio) = ComoJson<SobreONegocio>();
        var (deHistorico, comparaHistorico) = ComoJson<List<VersaoDaFicha>>();

        e.Property(f => f.Empresa).HasColumnName("empresa")
            .HasConversion(deEmpresa, comparaEmpresa);
        e.Property(f => f.Pessoa).HasColumnName("pessoa")
            .HasConversion(dePessoa, comparaPessoa);
        e.Property(f => f.Negocio).HasColumnName("negocio")
            .HasConversion(deNegocio, comparaNegocio);

        e.Property<List<VersaoDaFicha>>("_historico")
            .HasColumnName("historico")
            .HasConversion(deHistorico, comparaHistorico);

        e.Ignore(f => f.Historico);
        e.Ignore(f => f.Preenchidos);
        e.Ignore(f => f.Fatos);
        e.Ignore(f => f.Impressoes);
    }

    /// <summary>
    /// Conversor e comparador de um tipo do dominio para JSON.
    ///
    /// O comparador NAO e opcional, e o modo de falhar e o pior possivel: sem
    /// ele o EF compara por REFERENCIA, nunca percebe que o bloco mudou, e a
    /// gravacao simplesmente nao acontece — sem excecao, sem log, com o
    /// SaveChanges devolvendo sucesso.
    /// </summary>
    private static (ValueConverter<T, string>, ValueComparer<T>) ComoJson<T>() => (
        new ValueConverter<T, string>(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<T>(s, Json)!),
        new ValueComparer<T>(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(),
            v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Json), Json)!));
}
