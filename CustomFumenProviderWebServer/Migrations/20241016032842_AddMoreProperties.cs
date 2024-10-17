using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFumenProviderWebServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Genre",
                table: "FumenSets");

            migrationBuilder.AddColumn<string>(
                name: "VersionName",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdateTime",
                table: "FumenSets",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<string>(
                name: "BossCardName",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BossLockHpCoef",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BossVoiceNo",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GenreId",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GenreName",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "MusicRightsId",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MusicRightsName",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PublishState",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StageId",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StageName",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "VersionId",
                table: "FumenSets",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BossCardName",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "BossLockHpCoef",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "BossVoiceNo",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "GenreId",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "GenreName",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "MusicRightsId",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "MusicRightsName",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "PublishState",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "StageId",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "StageName",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "VersionId",
                table: "FumenSets");

            migrationBuilder.DropColumn(
                name: "VersionName",
                table: "FumenSets");

            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "FumenSets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdateTime",
                table: "FumenSets",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");
        }
    }
}
