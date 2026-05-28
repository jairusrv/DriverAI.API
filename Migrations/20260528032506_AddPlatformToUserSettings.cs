using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Platform",
                table: "UserSettings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Platform",
                table: "UserSettings");
        }
    }
}
