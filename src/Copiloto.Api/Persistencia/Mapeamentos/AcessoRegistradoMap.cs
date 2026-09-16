using Copiloto.Dominio.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class AcessoRegistradoMap : IEntityTypeConfiguration<AcessoRegistrado>
{
    public void Configure(EntityTypeBuilder<AcessoRegistrado> builder)
    {
        builder.ToTable("acessos");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.LeadId).IsRequired();
        builder.Property(a => a.UsuarioId);
        builder.Property(a => a.Quando).IsRequired();
        builder.Property(a => a.Detalhe).HasMaxLength(500);

        // Enum como texto: a trilha e lida por gente em investigacao, e um `3`
        // na coluna obriga quem le a abrir o codigo para saber o que aconteceu.
        builder.Property(a => a.Operacao).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Origem).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Os dois indices sao as duas perguntas do incidente: "o que este
        // usuario alcancou" e "quem tocou neste titular". Sem eles, a consulta
        // que decide o tamanho da comunicacao roda em varredura de tabela —
        // justamente no dia em que ninguem tem tempo.
        builder.HasIndex(a => new { a.UsuarioId, a.Quando }).HasDatabaseName("ix_acessos_usuario");
        builder.HasIndex(a => new { a.LeadId, a.Quando }).HasDatabaseName("ix_acessos_lead");
    }
}
