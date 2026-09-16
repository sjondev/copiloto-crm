using Copiloto.Dominio.Vendas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class LeadMap : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");
        // O id nasce no dominio, nunca no banco (#158). Ver ConversaMap.
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Telefone).HasMaxLength(20).IsRequired();

        // Os OUTROS numeros da mesma pessoa (#177) viram JSON numa coluna, e nao
        // tabela — mesma razao das lacunas do dossie: numero adicional nao tem
        // identidade propria nem e consultado sozinho.
        //
        // O preco esta declarado: o indice unico continua valendo so para o
        // numero PRINCIPAL. Dois leads podem, em tese, listar o mesmo numero
        // adicional, e quem impede isso hoje e a busca — que so adiciona o numero
        // depois de nao achar dono. Vira tabela no dia em que houver tela de
        // fundir lead, que e quando duas pessoas passam a mexer nisso ao mesmo
        // tempo.
        builder.PrimitiveCollection<List<string>>("_outrosNumeros")
            .HasColumnName("outros_numeros");

        builder.Property(l => l.NomeFonte).HasMaxLength(60);
        builder.Property(l => l.Nome).HasMaxLength(200);
        builder.Property(l => l.CriadoEm).IsRequired();

        // A oposicao a analise (#81) e' estado do titular, nao configuracao de
        // uso: ela precisa sobreviver a restart, a deploy e a troca de
        // instancia — senao o "parem de me analisar" vale ate a proxima subida.
        builder.Property(l => l.AnaliseDeIaSuspensa).IsRequired();
        builder.Property(l => l.OpostoEm);
        // Relacao como texto, e nao numero: a coluna e lida em investigacao e
        // em consulta manual, e um `1` obriga quem le a abrir o codigo (#85).
        builder.Property(l => l.Relacao).HasConversion<string>().HasMaxLength(20).IsRequired();
        // O dono do lead (#49). Indice porque a consulta do vendedor filtra por
        // ele em toda tela — e sem indice a lista fica lenta exatamente para
        // quem tem carteira grande.
        builder.Property(l => l.VendedorId);
        builder.HasIndex(l => l.VendedorId).HasDatabaseName("ix_leads_vendedor");

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
