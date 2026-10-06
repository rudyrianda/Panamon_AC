-- =========================================================================
-- Output & defect Expander Kyoshin 6.35 per hari produksi, per shift, per model (untuk diolah / analisa).
-- Dihitung Plclogger (PC server logger) tiap menit dari riwayat layar utama GOT dbo.PlcKyoshinTrend (hanya baca PLC):
--   Actual    = akumulasi kenaikan ACTUAL (R22)        di shift itu
--   Defect    = akumulasi kenaikan DEFECT (R24)        di shift itu
--   PlanBySut = akumulasi kenaikan PLAN BY SUT (R23)   di shift itu
--   ProdPlan  = PROD. PLAN (R20) terakhir model itu di shift itu
-- Counter turun / ganti model = dihitung dari 0 (aturan sama dengan dbo.PlcKyoshinChangePlan),
-- jadi total semua shift = dbo.PlcKyoshinDailyOutput hari itu.
-- Hari produksi = 07:00 s/d 07:00 besoknya; shift 1 = 07:00-15:45, 2 = 15:45-23:15, 3 = 23:15-07:00.
-- Hanya membuat tabel/kolom baru; tidak mengubah data yang ada. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinShiftOutput', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinShiftOutput
    (
        MachineCode    NVARCHAR(20)  NOT NULL,              -- MCH1-01 (Expander Kyoshin 6.35)
        ProductionDate DATE          NOT NULL,              -- 07:00 s/d 07:00 hari berikutnya
        ShiftNo        TINYINT       NOT NULL,              -- 1, 2, 3
        Model          NVARCHAR(50)  NOT NULL,              -- R10
        Actual         INT           NOT NULL,              -- kenaikan R22
        Defect         INT           NOT NULL,              -- kenaikan R24
        PlanBySut      INT           NOT NULL,              -- kenaikan R23
        ProdPlan       INT           NOT NULL,              -- R20 terakhir
        FirstAt        DATETIME2(0)  NOT NULL,              -- sampel pertama model ini di shift itu
        LastAt         DATETIME2(0)  NOT NULL,              -- sampel terakhir model ini di shift itu
        IsFinal        BIT           NOT NULL,              -- 1 = hari produksi sudah lewat 07:00 besoknya
        UpdatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_PlcKyoshinShiftOutput_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_PlcKyoshinShiftOutput PRIMARY KEY (MachineCode, ProductionDate, ShiftNo, Model),
        CONSTRAINT CK_PlcKyoshinShiftOutput_ShiftNo CHECK (ShiftNo BETWEEN 1 AND 3)
    );
END
GO

-- Defect harian per model di samping Actual (jumlah semua shift)
IF COL_LENGTH(N'dbo.PlcKyoshinDailyOutput', N'Defect') IS NULL
    ALTER TABLE dbo.PlcKyoshinDailyOutput ADD Defect INT NOT NULL CONSTRAINT DF_PlcKyoshinDailyOutput_Defect DEFAULT (0);
GO
