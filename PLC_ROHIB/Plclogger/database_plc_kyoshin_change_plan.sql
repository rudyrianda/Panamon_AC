-- =========================================================================
-- Change Plan & Actual harian Expander Kyoshin 6.35 dari PLC, satu baris per kali jalan model.
-- Dihitung Plclogger tiap menit dari dbo.PlcKyoshinTrend (hanya baca PLC).
-- Hari produksi = 07:00 s/d 07:00 hari berikutnya.
--   Hari berjalan : ChangePlan = PROD. PLAN (R20) model itu
--   Hari selesai  : ChangePlan = akumulasi PLAN BY SUT (R23) model itu di hari itu (IsFinal = 1)
-- Dipakai Panamon AC OEE Production Achievement (Change Plan & Actual).
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinChangePlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinChangePlan
    (
        MachineCode    NVARCHAR(20)  NOT NULL,              -- MCH1-01 (Expander Kyoshin 6.35)
        ProductionDate DATE          NOT NULL,              -- 07:00 s/d 07:00 hari berikutnya
        RunNo          INT           NOT NULL,              -- urutan kali jalan model di hari itu (1, 2, ...)
        Model          NVARCHAR(50)  NOT NULL,              -- R10
        StartAt        DATETIME2(0)  NOT NULL,              -- sampel pertama model ini di hari itu
        EndAt          DATETIME2(0)  NOT NULL,              -- sampel terakhir model ini di hari itu
        ProdPlan       INT           NOT NULL,              -- R20 PROD. PLAN terakhir
        PlanBySut      INT           NOT NULL,              -- akumulasi R23 PLAN BY SUT di hari itu
        Actual         INT           NOT NULL,              -- akumulasi R22 ACTUAL di hari itu
        ChangePlan     INT           NOT NULL,              -- ProdPlan selama hari berjalan, PlanBySut setelah hari selesai
        IsFinal        BIT           NOT NULL,              -- 1 = hari sudah lewat 07:00 besoknya
        UpdatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_PlcKyoshinChangePlan_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_PlcKyoshinChangePlan PRIMARY KEY (MachineCode, ProductionDate, RunNo)
    );
END
GO
