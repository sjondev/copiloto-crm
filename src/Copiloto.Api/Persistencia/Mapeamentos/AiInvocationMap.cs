using Copiloto.Dominio.Ia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Copiloto.Api.Persistencia.Mapeamentos;

public class AiInvocationMap : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        builder.ToTable("ai_invocations");
        // O id nasce no dominio, nunca no banco (#158). Ver ConversaMap.
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Modelo).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CustoEmReais).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(i => i.Quando).IsRequired();

        // Nulavel de proposito: existe invocacao sem negocio (diagnostico, teste
        // de provedor). O que nao existe e Guid.Empty passando por preenchido —
        // isso o construtor ja recusa (#2).
        builder.Property(i => i.DealId);
        builder.HasIndex(i => i.DealId).HasDatabaseName("ix_ai_invocations_deal");

        // O agente como TEXTO, e nao numero: a coluna e lida em investigacao e
        // em consulta manual, e um `1` obriga quem le a abrir o codigo — a mesma
        // razao ja escrita na Relacao do Lead (#85).
        builder.Property(i => i.Agente).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(i => i.TokensEntrada).IsRequired();
        builder.Property(i => i.TokensSaida).IsRequired();
        builder.Property(i => i.LatenciaMs).IsRequired();
        builder.Property(i => i.Tentativas).IsRequired();
        builder.Property(i => i.Sucesso).IsRequired();

        // Calculada: coluna guardaria a soma de dois campos que ja estao aqui, e
        // uma soma persistida e uma soma que pode divergir.
        builder.Ignore(i => i.TokensTotais);

        builder.Property(i => i.CorrelationId).HasMaxLength(64);

        // O contexto enviado, para o "por que essa sugestao?" (#51). SEM limite
        // de tamanho: cortar aqui produziria uma auditoria que mostra meio
        // contexto, e meia auditoria responde "confie em mim" — que e o
        // contrario do que o botao existe para fazer.
        //
        // Ele E dado pessoal, mesmo mascarado (#83), e a retencao segue a da
        // conversa (#45). Quando a #45 entrar, este e um dos campos a expurgar.
        builder.Property(i => i.ContextoEnviado);

        builder.Property(i => i.VersaoDoPrompt).HasMaxLength(20);

        // O indice de quem PAGOU sem receber: relatorio de custo perdido comeca
        // por aqui, e sem indice ele varre a tabela inteira (#3).
        builder.HasIndex(i => i.Sucesso).HasDatabaseName("ix_ai_invocations_sucesso");
    }
}
