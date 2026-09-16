using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PlanoDeAbordagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planos_de_abordagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    blocos = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planos_de_abordagem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_planos_deal",
                table: "planos_de_abordagem",
                column: "DealId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planos_de_abordagem");
        }
    }
}
