using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XlightsQueue.Migrations
{
    public partial class AddLocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ShowConfigs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ShowConfigs",
                type: "REAL",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Latitude", table: "ShowConfigs");
            migrationBuilder.DropColumn(name: "Longitude", table: "ShowConfigs");
        }
    }
}
