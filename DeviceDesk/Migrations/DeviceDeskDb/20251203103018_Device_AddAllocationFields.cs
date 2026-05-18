using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class Device_AddAllocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AllocatedAt",
                table: "Devices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllocatedByUserId",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocationType",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StudentIdNumber",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentName",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherName",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherPersalNumber",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllocatedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AllocatedByUserId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AllocationType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "StudentIdNumber",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "StudentName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TeacherName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TeacherPersalNumber",
                table: "Devices");
        }
    }
}
