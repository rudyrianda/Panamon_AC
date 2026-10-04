-- =========================================================================
-- Riwayat layar utama GOT (B-1) Expander Kyoshin 6.35, dicatat Plclogger tiap menit (hanya baca PLC).
-- Dipakai grafik Plan vs Actual di Panamon AC OEE (Plan = R23, Actual = R22).
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinTrend', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinTrend
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlcKyoshinTrend PRIMARY KEY,
        MachineCode    NVARCHAR(20)  NOT NULL,              -- MCH1-01 (Expander Kyoshin 6.35)
        SampleAt       DATETIME2(0)  NOT NULL,              -- waktu catat (tiap menit, detik 00)
        ProductionDate DATE          NOT NULL,              -- sebelum 07:00 = hari sebelumnya
        ShiftNo        TINYINT       NOT NULL,              -- 1 = 07:00-15:45, 2 = 15:45-23:15, 3 = 23:15-07:00
        Model          NVARCHAR(50)  NOT NULL,              -- R10
        ProdPlan       INT           NOT NULL,              -- R20 PROD. PLAN
        PlanBySut      INT           NOT NULL,              -- R23 PLAN
        Actual         INT           NOT NULL,              -- R22 ACTUAL
        Difference     INT           NOT NULL,              -- D20 DIFFERENCE
        Defect         INT           NOT NULL,              -- R24 DEFECT
        LossTime       INT           NOT NULL               -- R50 LOSS TIME/MIN
    );

    CREATE INDEX IX_PlcKyoshinTrend_Shift ON dbo.PlcKyoshinTrend (MachineCode, ProductionDate, ShiftNo, SampleAt);
END
GO
