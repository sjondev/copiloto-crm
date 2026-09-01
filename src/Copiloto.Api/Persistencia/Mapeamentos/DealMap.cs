using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class DealMap : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> e)
    {
        e.ToTable("deals");
        e.HasKey(d => d.Id);
        e.Property(d => d.LeadId).IsRequired();
        e.Property(d => d.AbertoEm).IsRequired();
        e.Property(d => d.Estagio).HasConversion<string>().HasMaxLength(20).IsRequired();
        e.Property(d => d.FechadoEm);

        // Dinheiro em `decimal(18,6)`, nunca em ponto flutuante: seis casas
        // porque uma invocacao de IA custa fracao de centavo, e arredondar cada
        // uma para dois faria o acumulado divergir da soma — que e justamente o
        // que o teste da #2 confere.
        e.Property(d => d.CustoIaAcumulado).HasColumnType("decimal(18,6)").IsRequired();

        // Pelo campo, e nao pela propriedade: `Invocacoes` e IReadOnlyList e o
        // EF precisa escrever na lista de verdade.
        e.HasMany(d => d.Invocacoes)
            .WithOne()
            .HasForeignKey(i => i.DealId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Navigation(d => d.Invocacoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
