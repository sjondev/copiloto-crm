using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDoEstagioEDaRelacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recoloca o backfill que se perdeu quando as migrations de 03-04/09
            // foram REGERADAS para caber no historico de 15-16/09 (#172).
            // `dotnet ef migrations add` so gera a mudanca de schema — o SQL
            // escrito a mao dentro do `Up` some sem aviso, e a esteira nao
            // percebe porque migration nao roda na CI: os testes montam o schema
            // com EnsureCreated() a partir do MODELO, e o modelo estava certo.
            // O que ficou errado foi o DADO das linhas que ja existiam.

            // EstagioDesde nasceu com DateTimeOffset.MinValue nas linhas antigas,
            // e para o Vigia (#53) isso nao e "vazio": e "parado ha dois mil
            // anos". Com a aplicacao de pe, ele reportava "Parado em Novo ha
            // 739874 dias" — o funil inteiro alertando na primeira passagem, que
            // e como um vendedor aprende no primeiro dia a ignorar a lista.
            //
            // AbertoEm e a unica data verdadeira que temos: o negocio esteve no
            // estagio atual, no minimo, desde que abriu. Erra para o lado seguro
            // — alerta cedo demais, nunca tarde.
            // O predicado NAO compara com '0001-01-01'. O Postgres guarda
            // DateTimeOffset.MinValue num `timestamptz` como `-infinity`, e
            // igualdade com a data literal nao casa nada — conferido no banco:
            // a coluna mostra `-infinity`, e o UPDATE com a data passou reto
            // sobre as duas linhas defeituosas.
            //
            // Isto vale registrar: a migration ORIGINAL, que eu perdi ao regerar,
            // comparava com a data literal. Ela tambem nao teria corrigido nada.
            // O backfill existia e estava escrito com cuidado; o que faltou foi
            // roda-lo uma vez contra Postgres de verdade.
            //
            // `< '1900-01-01'` cobre os dois: -infinity e' menor que qualquer
            // data, e nenhum negocio real abriu antes disso.
            migrationBuilder.Sql(
                "UPDATE deals SET \"EstagioDesde\" = \"AbertoEm\" "
                + "WHERE \"EstagioDesde\" < '1900-01-01';");

            // Relacao ficou com string vazia, que nao e valor do enum. Isto NAO
            // esta quebrando hoje: o conversor do EF cai para default(Relacao),
            // que e Cliente = 0, exatamente o valor que o backfill queria. E
            // dado invalido que acerta por acidente — e para de acertar no dia
            // em que alguem inserir outro membro em 0.
            migrationBuilder.Sql(
                "UPDATE leads SET \"Relacao\" = 'Cliente' WHERE \"Relacao\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: desfazer significaria devolver as linhas para o ano 1 e
            // para a string vazia, que e o defeito. Migration de dado que
            // restaura dado ruim nao e reversibilidade, e vandalismo agendado.
        }
    }
}
