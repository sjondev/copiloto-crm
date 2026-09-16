using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DossiePersistido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dossies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TemperaturaLida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DirecaoLida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Lacunas = table.Column<string[]>(type: "text[]", nullable: false),
                    sinais = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dossies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dossies_deal_gerado",
                table: "dossies",
                columns: new[] { "DealId", "GeradoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dossies");
        }
    }
}
