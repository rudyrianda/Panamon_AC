-- =========================================================================
-- Output (actual) harian Expander Kyoshin 6.35 per model dari PLC, satu baris per hari produksi per model.
-- Hari produksi = 07:00 s/d 07:00 hari berikutnya. Actual = akumulasi ACTUAL (R22) semua kali jalan model itu di hari itu.
-- Dihitung Plclogger tiap menit bersama dbo.PlcKyoshinChangePlan (dari riwayat dbo.PlcKyoshinTrend, hanya baca PLC).
-- Dipakai Panamon AC OEE Quality (TOTAL OUTPUT).
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinDailyOutput', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinDailyOutput
    (
        MachineCode    NVARCHAR(20)  NOT NULL,              -- MCH1-01 (Expander Kyoshin 6.35)
        ProductionDate DATE          NOT NULL,              -- 07:00 s/d 07:00 hari berikutnya
        Model          NVARCHAR(50)  NOT NULL,              -- R10
        Actual         INT           NOT NULL,              -- akumulasi R22 model ini di hari itu
        IsFinal        BIT           NOT NULL,              -- 1 = hari sudah lewat 07:00 besoknya
        UpdatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_PlcKyoshinDailyOutput_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_PlcKyoshinDailyOutput PRIMARY KEY (MachineCode, ProductionDate, Model)
    );
END
GO
