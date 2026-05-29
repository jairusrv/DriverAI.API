using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleMaintenanceToUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecopeData_Users_UserId",
                table: "RecopeData");

            migrationBuilder.DropIndex(
                name: "IX_RecopeData_UserId",
                table: "RecopeData");

            migrationBuilder.AddColumn<double>(
                name: "MaintenanceCostPerKm",
                table: "UserSettings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "UserSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "RecopeData",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RawData",
                table: "RecopeData",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Producto",
                table: "RecopeData",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "RecopeData",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "RecopeData",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Fecha",
                table: "RecopeData",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaintenanceCostPerKm",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "UserSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Tipo",
                table: "RecopeData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RawData",
                table: "RecopeData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Producto",
                table: "RecopeData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "RecopeData",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Origen",
                table: "RecopeData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Fecha",
                table: "RecopeData",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_RecopeData_UserId",
                table: "RecopeData",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecopeData_Users_UserId",
                table: "RecopeData",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
