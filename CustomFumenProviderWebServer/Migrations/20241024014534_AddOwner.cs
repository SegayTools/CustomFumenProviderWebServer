using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFumenProviderWebServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerMusicId",
                table: "FumenSets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FumenOwners",
                columns: table => new
                {
                    MusicId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Contact = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FumenOwners", x => x.MusicId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FumenSets_OwnerMusicId",
                table: "FumenSets",
                column: "OwnerMusicId");

            migrationBuilder.AddForeignKey(
                name: "FK_FumenSets_FumenOwners_OwnerMusicId",
                table: "FumenSets",
                column: "OwnerMusicId",
                principalTable: "FumenOwners",
                principalColumn: "MusicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FumenSets_FumenOwners_OwnerMusicId",
                table: "FumenSets");

            migrationBuilder.DropTable(
                name: "FumenOwners");

            migrationBuilder.DropIndex(
                name: "IX_FumenSets_OwnerMusicId",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "OwnerMusicId",
                table: "FumenSets");
        }
    }
}
