using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFumenProviderWebServer.Migrations
{
    /// <inheritdoc />
    public partial class DiffAddMore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BeamCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BellCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BpmCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BulletCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FlickCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HoldCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeterCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SoflanCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TapCount",
                table: "FumenDifficults",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BeamCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "BellCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "BpmCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "BulletCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "FlickCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "HoldCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "MeterCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "SoflanCount",
                table: "FumenDifficults");

            migrationBuilder.DropColumn(
                name: "TapCount",
                table: "FumenDifficults");
        }
    }
}
