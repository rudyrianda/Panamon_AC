using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitoringSystem.Migrations.ScaffoldedMachineDb
{
    /// <inheritdoc />
    public partial class FullSchemaMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineList",
                columns: table => new
                {
                    IdMachine = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineList", x => x.IdMachine);
                });

            migrationBuilder.CreateTable(
                name: "Efficiency",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMachine = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkingTime = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PlanQty = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    GoodProductionQty = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    DefectQty = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    OEE = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    OperatingRatio = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Ability = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Quality = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Achievement = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Efficiency", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Efficiency_MachineList",
                        column: x => x.IdMachine,
                        principalTable: "MachineList",
                        principalColumn: "IdMachine");
                });

            migrationBuilder.CreateTable(
                name: "EfficiencyLoss",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EfficiencyID = table.Column<int>(type: "int", nullable: false),
                    LossCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LossGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LossMinutes = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EfficiencyLoss", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EfficiencyLoss_Efficiency",
                        column: x => x.EfficiencyID,
                        principalTable: "Efficiency",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Efficiency_IdMachine",
                table: "Efficiency",
                column: "IdMachine");

            migrationBuilder.CreateIndex(
                name: "IX_EfficiencyLoss_EfficiencyID",
                table: "EfficiencyLoss",
                column: "EfficiencyID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EfficiencyLoss");

            migrationBuilder.DropTable(
                name: "Efficiency");

            migrationBuilder.DropTable(
                name: "MachineList");
        }
    }
}
