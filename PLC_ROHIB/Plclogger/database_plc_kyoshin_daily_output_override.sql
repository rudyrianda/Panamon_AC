-- ============================================================================
-- Koreksi manual Actual Inventory Expander Kyoshin 6.35.
-- Nilai di tabel ini dipakai kembali ketika Plclogger membangun ulang
-- dbo.PlcKyoshinDailyOutput dari histori PLC, sehingga koreksi tidak tertimpa.
-- ============================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinDailyOutputOverride', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinDailyOutputOverride
    (
        MachineCode    NVARCHAR(20) NOT NULL,
        ProductionDate DATE         NOT NULL,
        Model          NVARCHAR(50) NOT NULL,
        Actual         INT          NOT NULL,
        UpdatedAt      DATETIME2(0) NOT NULL
            CONSTRAINT DF_PlcKyoshinDailyOutputOverride_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_PlcKyoshinDailyOutputOverride PRIMARY KEY (MachineCode, ProductionDate, Model),
        CONSTRAINT CK_PlcKyoshinDailyOutputOverride_Actual CHECK (Actual >= 0)
    );
END
GO
