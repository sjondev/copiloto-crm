using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class LeadMap : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> e)
    {
        e.ToTable("leads");
        e.HasKey(l => l.Id);
        e.Property(l => l.Telefone).HasMaxLength(20).IsRequired();
        e.Property(l => l.Nome).HasMaxLength(200);
        e.Property(l => l.CriadoEm).IsRequired();

        // A oposicao a analise (#81) e' estado do titular, nao configuracao de
        // uso: ela precisa sobreviver a restart, a deploy e a troca de
        // instancia — senao o "parem de me analisar" vale ate a proxima subida.
        e.Property(l => l.AnaliseDeIaSuspensa).IsRequired();
        e.Property(l => l.OpostoEm);

        // O indice UNICO e o ponto que nao da para deixar so no codigo.
        //
        // A #22 resolveu a normalizacao, mas duas instancias processando a mesma
        // conversa em paralelo criam dois leads antes de qualquer `if` perceber —
        // e o historico se parte exatamente como a #22 existe para evitar, so que
        // por outro caminho. Regra de unicidade que nao esta no banco vale
        // enquanto o processo e um so.
        e.HasIndex(l => l.Telefone).IsUnique().HasDatabaseName("ux_leads_telefone");
    }
}
