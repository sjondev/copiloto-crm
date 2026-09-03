using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Copiloto.Api.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EstagioDesdeNoDeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O default gerado e DateTimeOffset.MinValue (ano 1), e para linha
            // existente isso nao e "vazio": e "parado ha dois mil anos". O Vigia
            // (#53) alertaria o funil inteiro na primeira passagem, e o vendedor
            // aprenderia no primeiro dia a ignorar a lista.
            //
            // O backfill usa AbertoEm porque e a unica data verdadeira que
            // temos: o negocio esteve no estagio atual, no minimo, desde que
            // abriu. Erra para o lado seguro — alerta cedo demais, nunca tarde.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EstagioDesde",
                table: "deals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                "UPDATE deals SET \"EstagioDesde\" = \"AbertoEm\" "
                + "WHERE \"EstagioDesde\" = '0001-01-01T00:00:00Z';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstagioDesde",
                table: "deals");
        }
    }
}
