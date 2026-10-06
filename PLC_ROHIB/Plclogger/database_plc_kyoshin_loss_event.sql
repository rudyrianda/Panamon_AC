-- =========================================================================
-- Kejadian loss time Expander Kyoshin 6.35 dari PLC, dicatat Plclogger (hanya baca PLC).
-- R103 = timer loss (menit): bukan 0 = loss mulai, kembali 0 = loss selesai.
-- Reason diambil dari layar GOT LOSS TIME (18 kotak): nama R410..R418, durasi (menit) R419; R420/R429; ... R580/R589.
--   Reason kejadian = kotak yang durasinya bertambah selama loss (dicek sampai 10 menit setelah loss selesai).
-- Dipakai Panamon AC OEE: LOSS TIME per model & TOTAL LOSS TIME per shift (Production Data) dan Detail Loss Time.
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcKyoshinLossEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcKyoshinLossEvent
    (
        Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlcKyoshinLossEvent PRIMARY KEY,
        MachineCode    NVARCHAR(20)   NOT NULL,             -- MCH1-01 (Expander Kyoshin 6.35)
        ProductionDate DATE           NOT NULL,             -- 07:00 s/d 07:00 hari berikutnya (dari StartAt)
        ShiftNo        TINYINT        NOT NULL,             -- shift saat loss mulai: 1 = 07:00-15:45, 2 = 15:45-23:15, 3 = 23:15-07:00
        Model          NVARCHAR(50)   NOT NULL,             -- model (R10) saat loss mulai
        StartAt        DATETIME2(0)   NOT NULL,             -- perkiraan mulai = saat R103 pertama terbaca dikurangi nilai R103 (menit)
        EndAt          DATETIME2(0)   NULL,                 -- NULL = loss masih berjalan
        DurationMin    INT            NOT NULL,             -- nilai R103 terakhir sebelum kembali 0 (menit)
        ReasonName     NVARCHAR(50)   NULL,                 -- reason dengan kenaikan durasi terbesar (NULL = belum terdeteksi)
        ReasonDetail   NVARCHAR(400)  NULL,                 -- semua reason yang bertambah, mis. "MODEL CHANGE +5; MATERIAL +2"
        ReasonsAtStart NVARCHAR(2000) NULL,                 -- salinan 18 reason (nama & menit) saat loss mulai
        UpdatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_PlcKyoshinLossEvent_UpdatedAt DEFAULT (SYSDATETIME())
    );

    CREATE INDEX IX_PlcKyoshinLossEvent_Day ON dbo.PlcKyoshinLossEvent (MachineCode, ProductionDate, StartAt);
END
GO

-- -------------------------------------------------------------------------
-- 2026-10-05: loss dipotong per shift, penantian reason disimpan, cegah pencatatan dobel.
--   GroupId            = Id potongan pertama satu loss (semua potongan satu loss sama)
--   R103Offset         = nilai R103 di awal potongan (DurationMin = R103 - R103Offset)
--   ReasonPendingUntil = reason belum terisi; Plclogger masih menunggu sampai jam ini
--   UX_PlcKyoshinLossEvent_Open = hanya satu loss terbuka (EndAt NULL) per mesin
-- -------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.PlcKyoshinLossEvent', N'GroupId') IS NULL
    ALTER TABLE dbo.PlcKyoshinLossEvent ADD GroupId BIGINT NULL;
GO
IF COL_LENGTH(N'dbo.PlcKyoshinLossEvent', N'R103Offset') IS NULL
    ALTER TABLE dbo.PlcKyoshinLossEvent ADD R103Offset INT NOT NULL CONSTRAINT DF_PlcKyoshinLossEvent_R103Offset DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.PlcKyoshinLossEvent', N'ReasonPendingUntil') IS NULL
    ALTER TABLE dbo.PlcKyoshinLossEvent ADD ReasonPendingUntil DATETIME2(0) NULL;
GO
UPDATE dbo.PlcKyoshinLossEvent SET GroupId = Id WHERE GroupId IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PlcKyoshinLossEvent_Open' AND object_id = OBJECT_ID(N'dbo.PlcKyoshinLossEvent'))
    CREATE UNIQUE INDEX UX_PlcKyoshinLossEvent_Open ON dbo.PlcKyoshinLossEvent (MachineCode) WHERE EndAt IS NULL;
GO
