using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddListenSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListenSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionToken = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ListenersAtStart = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListenSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListenSessions_StartedAt",
                table: "ListenSessions",
                column: "StartedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ListenSessions");
        }
    }
}
