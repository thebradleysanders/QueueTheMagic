using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class RemoveHoursOfOperation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite does not support DROP COLUMN — columns are left in the DB but
            // are no longer mapped in the model and will be ignored by EF Core.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "HoursStart", table: "ShowConfigs", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "HoursEnd", table: "ShowConfigs", type: "TEXT", nullable: true);
        }
    }
}
