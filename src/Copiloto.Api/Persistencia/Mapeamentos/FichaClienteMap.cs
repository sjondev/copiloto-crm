using System.Text.Json;
using Copiloto.Dominio.Fichas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class FichaClienteMap : IEntityTypeConfiguration<FichaCliente>
{
    private static readonly JsonSerializerOptions Json = new();

    public void Configure(EntityTypeBuilder<FichaCliente> builder)
    {
        builder.ToTable("fichas_cliente");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.LeadId).IsRequired();
        builder.Property(f => f.CriadaEm).IsRequired();
        builder.Property(f => f.AtualizadaEm).IsRequired();

        // Uma ficha por lead: a ficha E o que se sabe daquele cliente, e duas
        // seriam duas versoes da verdade sem criterio de desempate.
        builder.HasIndex(f => f.LeadId).IsUnique().HasDatabaseName("ux_fichas_lead");

        // Os tres blocos viram colunas na mesma tabela, e nao tabelas proprias:
        // eles nao existem sem a ficha e nunca sao consultados sozinhos.
        builder.OwnsOne(f => f.Empresa);
        builder.OwnsOne(f => f.Pessoa);
        builder.OwnsOne(f => f.Negocio);

        // O historico vai como JSON numa coluna so, por conversor.
        //
        // A primeira tentativa foi `OwnsMany(...).ToJson()`, que e o caminho
        // idiomatico, e ele NAO fecha aqui: `VersaoDaFicha` tem tres records
        // aninhados, e o EF nao consegue ligar esses parametros de construtor
        // dentro de um owned em JSON.
        //
        // A saida obvia seria achatar `VersaoDaFicha` em doze campos soltos —
        // e ela esta errada. Seria o dominio se curvando ao ORM, quando o
        // dominio e' quem tem razao para existir. O conversor deixa o modelo
        // como esta e paga o preco no mapeamento, que e' o lugar certo para
        // pagar.
        //
        // O historico e lista de LEITURA (a tela mostra "o que mudou e quando"),
        // nunca alvo de consulta por campo, entao JSON nao custa nada aqui.
        var conversor = new ValueConverter<List<VersaoDaFicha>, string>(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<List<VersaoDaFicha>>(s, Json) ?? new());

        // Sem o comparador, o EF compara por REFERENCIA e nunca percebe que a
        // lista mudou — a versao nova simplesmente nao seria gravada, sem erro.
        var comparador = new ValueComparer<List<VersaoDaFicha>>(
            (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
            v => JsonSerializer.Serialize(v, Json).GetHashCode(),
            v => JsonSerializer.Deserialize<List<VersaoDaFicha>>(
                     JsonSerializer.Serialize(v, Json), Json)!);

        builder.Property<List<VersaoDaFicha>>("_historico")
            .HasColumnName("historico")
            .HasConversion(conversor, comparador);

        builder.Ignore(f => f.Historico);
    }
}
