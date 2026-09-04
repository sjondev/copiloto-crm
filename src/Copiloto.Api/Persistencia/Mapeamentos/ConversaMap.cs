using Copiloto.Dominio.Conversas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class ConversaMap : IEntityTypeConfiguration<Conversa>
{
    public void Configure(EntityTypeBuilder<Conversa> builder)
    {
        builder.ToTable("conversas");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.LeadId).IsRequired();

        builder.HasMany(c => c.Mensagens).WithOne().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Mensagens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class MensagemMap : IEntityTypeConfiguration<Mensagem>
{
    public void Configure(EntityTypeBuilder<Mensagem> builder)
    {
        builder.ToTable("mensagens");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Autor).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Texto).IsRequired();
        builder.Property(m => m.EnviadaEm).IsRequired();

        // Ordenar por envio e a consulta mais frequente da tela — e foi por ela
        // que a #22 existe: balao fora de ordem faz o dossie ler a conversa ao
        // contrario.
        builder.HasIndex(m => m.EnviadaEm).HasDatabaseName("ix_mensagens_enviada_em");
    }
}
