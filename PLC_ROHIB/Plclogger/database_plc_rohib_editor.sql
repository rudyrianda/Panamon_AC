-- =========================================================================
-- PLC ROHIB (Plan Exp 6.35): isi editor Production Plan terakhir per mesin.
-- Dipakai halaman editor supaya isinya tidak hilang saat di-refresh.
-- Hanya membuat tabel baru; tidak mengubah tabel lain. Aman dijalankan ulang.
-- =========================================================================
USE [PROMOSYS];
GO

IF OBJECT_ID(N'dbo.PlcRohibEditorRow', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlcRohibEditorRow
    (
        MachineCode NVARCHAR(20) NOT NULL,                 -- contoh: MCH1-01 (Expander Kyoshin 6.35)
        RowNo       INT          NOT NULL,                 -- baris editor 1-120
        ModelName   NVARCHAR(50) NOT NULL CONSTRAINT DF_PlcRohibEditorRow_ModelName DEFAULT (N''),
        ProdPlan    INT          NOT NULL CONSTRAINT DF_PlcRohibEditorRow_ProdPlan  DEFAULT (0),
        Sut         INT          NOT NULL CONSTRAINT DF_PlcRohibEditorRow_Sut       DEFAULT (0),
        UpdatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_PlcRohibEditorRow_UpdatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_PlcRohibEditorRow PRIMARY KEY (MachineCode, RowNo),
        CONSTRAINT CK_PlcRohibEditorRow_RowNo CHECK (RowNo BETWEEN 1 AND 999)
    );
END
GO

-- Tanggal plan tiap baris (dari list rencana dbo.PsiWeeklyPlan), ditampilkan di kolom "Urutan Antrian / Tanggal"
IF COL_LENGTH(N'dbo.PlcRohibEditorRow', N'PlanDate') IS NULL
    ALTER TABLE dbo.PlcRohibEditorRow ADD PlanDate DATE NULL;
GO

-- 2026-10-06: ACTUAL & DEFECT terakhir tiap baris, disimpan halaman editor setiap kali berubah
--   Actual = output Inventory AC OEE (dbo.PlcKyoshinDailyOutput) yang dibagi ke baris ini
--   Defect = register DEFECT PLC baris ini, selama model di PLC sama dengan model baris editor
IF COL_LENGTH(N'dbo.PlcRohibEditorRow', N'Actual') IS NULL
    ALTER TABLE dbo.PlcRohibEditorRow ADD Actual INT NOT NULL CONSTRAINT DF_PlcRohibEditorRow_Actual DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.PlcRohibEditorRow', N'Defect') IS NULL
    ALTER TABLE dbo.PlcRohibEditorRow ADD Defect INT NOT NULL CONSTRAINT DF_PlcRohibEditorRow_Defect DEFAULT (0);
GO
