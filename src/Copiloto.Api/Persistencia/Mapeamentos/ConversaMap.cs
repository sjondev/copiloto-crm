using Copiloto.Dominio.Conversas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class ConversaMap : IEntityTypeConfiguration<Conversa>
{
    public void Configure(EntityTypeBuilder<Conversa> e)
    {
        e.ToTable("conversas");
        e.HasKey(c => c.Id);
        e.Property(c => c.LeadId).IsRequired();

        e.HasMany(c => c.Mensagens).WithOne().OnDelete(DeleteBehavior.Cascade);
        e.Navigation(c => c.Mensagens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class MensagemMap : IEntityTypeConfiguration<Mensagem>
{
    public void Configure(EntityTypeBuilder<Mensagem> e)
    {
        e.ToTable("mensagens");
        e.HasKey(m => m.Id);
        e.Property(m => m.Autor).HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(m => m.Texto).IsRequired();
        e.Property(m => m.EnviadaEm).IsRequired();

        // Ordenar por envio e a consulta mais frequente da tela — e foi por ela
        // que a #22 existe: balao fora de ordem faz o dossie ler a conversa ao
        // contrario.
        e.HasIndex(m => m.EnviadaEm).HasDatabaseName("ix_mensagens_enviada_em");
    }
}
