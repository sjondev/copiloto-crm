using Copiloto.Dominio.Ia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class AiInvocationMap : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> e)
    {
        e.ToTable("ai_invocations");
        e.HasKey(i => i.Id);
        e.Property(i => i.Modelo).HasMaxLength(100).IsRequired();
        e.Property(i => i.CustoEmReais).HasColumnType("decimal(18,6)").IsRequired();
        e.Property(i => i.Quando).IsRequired();

        // Nulavel de proposito: existe invocacao sem negocio (diagnostico, teste
        // de provedor). O que nao existe e Guid.Empty passando por preenchido —
        // isso o construtor ja recusa (#2).
        e.Property(i => i.DealId);
        e.HasIndex(i => i.DealId).HasDatabaseName("ix_ai_invocations_deal");
    }
}
