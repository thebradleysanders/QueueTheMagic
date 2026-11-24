using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddGuestPasses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequestCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestPasses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestPasses_Code",
                table: "GuestPasses",
                column: "Code",
                unique: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "QueueItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GuestPassesEnabled",
                table: "ShowConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "GuestMaxSongsPerWindow",
                table: "ShowConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GuestPasses");
            migrationBuilder.DropColumn(name: "IsGuest", table: "QueueItems");
            migrationBuilder.DropColumn(name: "GuestPassesEnabled", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "GuestMaxSongsPerWindow", table: "ShowConfigs");
        }
    }
}
