using Copiloto.Dominio.Ia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class AiInvocationMap : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        builder.ToTable("ai_invocations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Modelo).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CustoEmReais).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(i => i.Quando).IsRequired();

        // Nulavel de proposito: existe invocacao sem negocio (diagnostico, teste
        // de provedor). O que nao existe e Guid.Empty passando por preenchido —
        // isso o construtor ja recusa (#2).
        builder.Property(i => i.DealId);
        builder.HasIndex(i => i.DealId).HasDatabaseName("ix_ai_invocations_deal");
    }
}
