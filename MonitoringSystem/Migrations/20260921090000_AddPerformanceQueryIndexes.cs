using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MonitoringSystem.Data;

#nullable disable

namespace MonitoringSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260921090000_AddPerformanceQueryIndexes")]
    public partial class AddPerformanceQueryIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.OEESN', N'U') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.OEESN')
                         AND name = N'IX_OEESN_MachineCode_SDate'
                   )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_OEESN_MachineCode_SDate
                    ON dbo.OEESN (MachineCode ASC, SDate DESC)
                    INCLUDE (ID, Product_Id, SN_GOOD, TargetUnit, Performance, ShiftMode);
                END;

                IF OBJECT_ID(N'dbo.NG_RPTS', N'U') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.NG_RPTS')
                         AND name = N'IX_NG_RPTS_MachineCode_SDate'
                   )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_NG_RPTS_MachineCode_SDate
                    ON dbo.NG_RPTS (MachineCode ASC, SDate DESC);
                END;

                IF OBJECT_ID(N'dbo.AssemblyLossTime', N'U') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.AssemblyLossTime')
                         AND name = N'IX_AssemblyLossTime_MachineCode_Date'
                   )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_AssemblyLossTime_MachineCode_Date
                    ON dbo.AssemblyLossTime (MachineCode ASC, [Date] DESC)
                    INCLUDE ([Time], EndDateTime, LossTime);
                END;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.AssemblyLossTime')
                      AND name = N'IX_AssemblyLossTime_MachineCode_Date'
                )
                    DROP INDEX IX_AssemblyLossTime_MachineCode_Date ON dbo.AssemblyLossTime;

                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.NG_RPTS')
                      AND name = N'IX_NG_RPTS_MachineCode_SDate'
                )
                    DROP INDEX IX_NG_RPTS_MachineCode_SDate ON dbo.NG_RPTS;

                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.OEESN')
                      AND name = N'IX_OEESN_MachineCode_SDate'
                )
                    DROP INDEX IX_OEESN_MachineCode_SDate ON dbo.OEESN;
            ");
        }
    }
}
