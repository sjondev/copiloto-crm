using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ProcedenciaDaChamada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextoEnviado",
                table: "ai_invocations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersaoDoPrompt",
                table: "ai_invocations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextoEnviado",
                table: "ai_invocations");

            migrationBuilder.DropColumn(
                name: "VersaoDoPrompt",
                table: "ai_invocations");
        }
    }
}
