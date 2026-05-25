using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImeiToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Imei",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Imei",
                table: "Users");
        }
    }
}
