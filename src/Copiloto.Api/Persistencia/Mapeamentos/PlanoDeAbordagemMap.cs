using System.Text.Json;
using Copiloto.Dominio.Planos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

/// <summary>
/// O plano de abordagem (#12).
///
/// Os quatro blocos vao como JSON numa coluna, e nao em tabela — mesma razao dos
/// sinais do dossie: eles nao existem fora do plano, nao tem id proprio e nunca
/// sao consultados sozinhos. A tela sempre pede o plano inteiro.
/// </summary>
public class PlanoDeAbordagemMap : IEntityTypeConfiguration<PlanoDeAbordagem>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<PlanoDeAbordagem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("planos_de_abordagem");
        builder.HasKey(p => p.Id);

        // Um plano por negocio: o plano e o que o vendedor vai fazer NESTA
        // negociacao, e duas versoes ativas do mesmo negocio seriam duas
        // intencoes competindo na mesma tela.
        builder.HasIndex(p => p.DealId).IsUnique().HasDatabaseName("ux_planos_deal");

        builder.Property(p => p.DealId).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();
        builder.Property(p => p.AtualizadoEm).IsRequired();
        builder.Property(p => p.Versao).IsRequired();

        // O dicionario e serializado inteiro. O comparador e obrigatorio: sem
        // ele o EF compara a REFERENCIA do dicionario, que nunca muda porque o
        // campo e readonly — e a alteracao do vendedor seria descartada no
        // SaveChanges sem erro nenhum aparecer.
        builder.Property<Dictionary<BlocoDoPlano, Bloco>>("_blocos")
            .HasColumnName("blocos")
            .HasConversion(
                blocos => JsonSerializer.Serialize(blocos, Json),
                texto => JsonSerializer.Deserialize<Dictionary<BlocoDoPlano, Bloco>>(texto, Json)!,
                new ValueComparer<Dictionary<BlocoDoPlano, Bloco>>(
                    (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                    v => JsonSerializer.Serialize(v, Json).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<Dictionary<BlocoDoPlano, Bloco>>(
                        JsonSerializer.Serialize(v, Json), Json)!))
            .IsRequired();
    }
}
