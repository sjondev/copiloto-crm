using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class LeadMap : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Telefone).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Nome).HasMaxLength(200);
        builder.Property(l => l.CriadoEm).IsRequired();

        // O indice UNICO e o ponto que nao da para deixar so no codigo.
        //
        // A #22 resolveu a normalizacao, mas duas instancias processando a mesma
        // conversa em paralelo criam dois leads antes de qualquer `if` perceber —
        // e o historico se parte exatamente como a #22 existe para evitar, so que
        // por outro caminho. Regra de unicidade que nao esta no banco vale
        // enquanto o processo e um so.
        builder.HasIndex(l => l.Telefone).IsUnique().HasDatabaseName("ux_leads_telefone");
    }
}
