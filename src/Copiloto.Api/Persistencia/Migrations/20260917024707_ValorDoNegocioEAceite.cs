using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ValorDoNegocioEAceite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ValorEmReais",
                table: "deals",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SugestaoAceita",
                table: "ai_invocations",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorEmReais",
                table: "deals");

            migrationBuilder.DropColumn(
                name: "SugestaoAceita",
                table: "ai_invocations");
        }
    }
}
