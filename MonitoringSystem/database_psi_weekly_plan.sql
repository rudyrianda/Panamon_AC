-- =========================================================================
-- List mingguan (Senin-Minggu) dari "Daily prod plan", diisi worker SapPlanImport (Services/SapPlanImport/PsiWeeklyPlanner.cs).
-- Satu baris = satu model di satu hari dalam list minggu itu, sudah urut (SeqInWeek / SeqInDay).
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PsiWeeklyPlan', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PsiWeeklyPlan
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PsiWeeklyPlan PRIMARY KEY,
        MachineCode      NVARCHAR(20)  NOT NULL,             -- MCH1-01 (bagian CU)
        WeekStart        DATE          NOT NULL,             -- Senin (atau tanggal 1 jika bulan mulai di tengah minggu)
        WeekEnd          DATE          NOT NULL,             -- Minggu (atau tanggal terakhir bulan)
        PlanDate         DATE          NOT NULL,             -- hari model ini dikerjakan (hari pertama bila digabung)
        SeqInWeek        INT           NOT NULL,             -- urutan dalam list minggu itu (1, 2, ...)
        SeqInDay         INT           NOT NULL,             -- urutan dalam hari itu
        ProductName      NVARCHAR(100) NOT NULL,             -- nama model persis seperti di Excel
        Qty              INT           NOT NULL,
        SourceDates      NVARCHAR(400) NOT NULL,             -- tanggal asal qty, mis. "2026-10-06,2026-10-07"
        IsMerged         BIT           NOT NULL,             -- 1 = gabungan beberapa tanggal (ditaruh paling akhir di harinya)
        MergeReason      NVARCHAR(50)  NULL,                 -- HariBerurutan / QtyKecil / HariBerurutan+QtyKecil
        PriorityCategory NVARCHAR(20)  NOT NULL,             -- No. 1 ... No. 19 (psi-weekly-priority.json)
        PriorityRank     INT           NOT NULL,
        SourceFileHash   CHAR(64)      NOT NULL,             -- SHA256 file Excel sumber
        ImportedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_PsiWeeklyPlan_ImportedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT UQ_PsiWeeklyPlan_Seq UNIQUE (MachineCode, WeekStart, SeqInWeek),
        CONSTRAINT CK_PsiWeeklyPlan_Qty CHECK (Qty > 0)
    );

    CREATE INDEX IX_PsiWeeklyPlan_PlanDate ON dbo.PsiWeeklyPlan (MachineCode, PlanDate);
END
GO
