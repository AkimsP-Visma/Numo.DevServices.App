using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Numo.DevServices.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSampleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "DevServices");

            migrationBuilder.CreateTable(
                name: "SampleItems",
                schema: "DevServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleItems", x => x.Id);
                    table.CheckConstraint("CK_SampleItems_Id_NotEmpty", "\"Id\" <> '00000000-0000-0000-0000-000000000000'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleItems_Name",
                schema: "DevServices",
                table: "SampleItems",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleItems",
                schema: "DevServices");
        }
    }
}
