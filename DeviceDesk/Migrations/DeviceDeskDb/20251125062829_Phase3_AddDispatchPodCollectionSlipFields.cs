using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class Phase3_AddDispatchPodCollectionSlipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RnrBatchId",
                table: "DispatchPods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionSlipNumber",
                table: "DispatchPods",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLockedToCollectionSlip",
                table: "DispatchPods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CollectionSlipValidated",
                table: "DispatchPods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CollectionSlipValidatedAt",
                table: "DispatchPods",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionSlipValidatedBy",
                table: "DispatchPods",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmisCode",
                table: "DispatchPods",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalDevicesExpected",
                table: "DispatchPods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalDevicesScanned",
                table: "DispatchPods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedBy",
                table: "DispatchPods",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionNotes",
                table: "DispatchPods",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RnrBatchId",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectionSlipNumber",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "IsLockedToCollectionSlip",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectionSlipValidated",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectionSlipValidatedAt",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectionSlipValidatedBy",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "EmisCode",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "TotalDevicesExpected",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "TotalDevicesScanned",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectedBy",
                table: "DispatchPods");

            migrationBuilder.DropColumn(
                name: "CollectionNotes",
                table: "DispatchPods");
        }
    }
}
