using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBreakTimeStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakTime1End",
                table: "AdditionalBreakTimes");

            migrationBuilder.DropColumn(
                name: "BreakTime1Start",
                table: "AdditionalBreakTimes");

            migrationBuilder.DropColumn(
                name: "BreakTime2End",
                table: "AdditionalBreakTimes");

            migrationBuilder.DropColumn(
                name: "BreakTime2Start",
                table: "AdditionalBreakTimes");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AdditionalBreakTimes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "AdditionalBreakTimes");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AdditionalBreakTimes");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "AdditionalBreakTimes");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakTime1End",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakTime1Start",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakTime2End",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakTime2Start",
                table: "AdditionalBreakTimes",
                type: "time",
                nullable: true);
        }
    }
}
