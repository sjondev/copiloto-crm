using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MidiaComoLacuna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "DuracaoDaMidia",
                table: "mensagens",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDeMidia",
                table: "mensagens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuracaoDaMidia",
                table: "mensagens");

            migrationBuilder.DropColumn(
                name: "TipoDeMidia",
                table: "mensagens");
        }
    }
}
