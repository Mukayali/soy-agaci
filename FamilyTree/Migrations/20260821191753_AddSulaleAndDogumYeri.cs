using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyTree.Migrations
{
    /// <inheritdoc />
    public partial class AddSulaleAndDogumYeri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DogumYeri",
                table: "Persons",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SulaleId",
                table: "Persons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sulaleler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ad = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aciklama = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sulaleler", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_SulaleId",
                table: "Persons",
                column: "SulaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sulaleler_Ad",
                table: "Sulaleler",
                column: "Ad",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Sulaleler_SulaleId",
                table: "Persons",
                column: "SulaleId",
                principalTable: "Sulaleler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Sulaleler_SulaleId",
                table: "Persons");

            migrationBuilder.DropTable(
                name: "Sulaleler");

            migrationBuilder.DropIndex(
                name: "IX_Persons_SulaleId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "DogumYeri",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "SulaleId",
                table: "Persons");
        }
    }
}
