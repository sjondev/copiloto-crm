using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LedgerCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Agente",
                table: "ai_invocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "ai_invocations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatenciaMs",
                table: "ai_invocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Sucesso",
                table: "ai_invocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Tentativas",
                table: "ai_invocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokensEntrada",
                table: "ai_invocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TokensSaida",
                table: "ai_invocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_ai_invocations_sucesso",
                table: "ai_invocations",
                column: "Sucesso");

            // As linhas que ja existiam vieram do unico caminho que gravava
            // antes: a sugestao de bloco do plano (#189). Sem backfill elas
            // ficariam com `Agente = ''` — que NAO e valor do enum, o mesmo
            // defeito que a #172 achou na Relacao do Lead —, com zero tentativas
            // (o construtor recusa) e marcadas como FALHA, quando so o sucesso
            // gerava linha ate aqui.
            //
            // O predicado e por valor invalido, e nao por data: ele acerta
            // qualquer linha nessa condicao e nao faz nada nas outras.
            migrationBuilder.Sql(
                "UPDATE ai_invocations SET \"Agente\" = 'Plano', \"Tentativas\" = 1, "
                + "\"Sucesso\" = true WHERE \"Agente\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_invocations_sucesso",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "Agente",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "LatenciaMs",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "Sucesso",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "Tentativas",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "TokensEntrada",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "TokensSaida",
                table: "ai_invocations");
        }
    }
}
