using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyTree.Migrations
{
    /// <inheritdoc />
    public partial class MultiSulaleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sıra kritik: önce yeni join tablosu oluşturulur, mevcut Persons.SulaleId verisi
            // buraya kopyalanır, ANCAK SONRA eski sütun/FK/index düşürülür. Aksi halde
            // (EF Core'un varsayılan üretim sırası: önce DropColumn) veri kopyalanamadan
            // kaybolur — bu yüzden bu migration elle yeniden sıralandı.
            migrationBuilder.CreateTable(
                name: "PersonSulaleler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    SulaleId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonSulaleler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonSulaleler_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonSulaleler_Sulaleler_SulaleId",
                        column: x => x.SulaleId,
                        principalTable: "Sulaleler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PersonSulaleler_PersonId_SulaleId",
                table: "PersonSulaleler",
                columns: new[] { "PersonId", "SulaleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonSulaleler_SulaleId",
                table: "PersonSulaleler",
                column: "SulaleId");

            // Veri taşıma: mevcut tekil Persons.SulaleId atamalarını yeni join tablosuna kopyala.
            migrationBuilder.Sql(
                "INSERT INTO `PersonSulaleler` (`PersonId`, `SulaleId`, `CreatedAt`) " +
                "SELECT `Id`, `SulaleId`, NOW() FROM `Persons` WHERE `SulaleId` IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Sulaleler_SulaleId",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Persons_SulaleId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "SulaleId",
                table: "Persons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SulaleId",
                table: "Persons",
                type: "int",
                nullable: true);

            // Veri taşıma (geri yön, kayıplı): many-to-many yapı tek bir SulaleId'ye
            // sığdırılamaz; her kişi için en küçük SulaleId korunur, diğerleri kaybolur.
            migrationBuilder.Sql(
                "UPDATE `Persons` p " +
                "SET p.`SulaleId` = (SELECT MIN(ps.`SulaleId`) FROM `PersonSulaleler` ps WHERE ps.`PersonId` = p.`Id`);");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_SulaleId",
                table: "Persons",
                column: "SulaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Sulaleler_SulaleId",
                table: "Persons",
                column: "SulaleId",
                principalTable: "Sulaleler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "PersonSulaleler");
        }
    }
}
