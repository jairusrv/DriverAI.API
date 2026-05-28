using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTypeToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "UserSettings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "UserSettings");
        }
    }
}
