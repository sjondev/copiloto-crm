using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class DealMap : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("deals");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.LeadId).IsRequired();
        builder.Property(d => d.AbertoEm).IsRequired();
        builder.Property(d => d.Estagio).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.FechadoEm);

        // Dinheiro em `decimal(18,6)`, nunca em ponto flutuante: seis casas
        // porque uma invocacao de IA custa fracao de centavo, e arredondar cada
        // uma para dois faria o acumulado divergir da soma — que e justamente o
        // que o teste da #2 confere.
        builder.Property(d => d.CustoIaAcumulado).HasColumnType("decimal(18,6)").IsRequired();

        // Pelo campo, e nao pela propriedade: `Invocacoes` e IReadOnlyList e o
        // EF precisa escrever na lista de verdade.
        builder.HasMany(d => d.Invocacoes)
            .WithOne()
            .HasForeignKey(i => i.DealId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Invocacoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
