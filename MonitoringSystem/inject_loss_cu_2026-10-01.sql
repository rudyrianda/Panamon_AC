/*
  Inject Loss Time PWK Actual - Line CU (MCH1-01), 2026-10-01, Non-Shift
  DB: PROMOSYS (10.83.33.103)

  1. Hapus 6 loss lama "Trial conveyor finish good"
     (08:05, 08:44, 09:15, 09:25, 09:35, 09:46).
  2. Tambah 3 loss baru (Machine & Tools Trouble):
       1. Persiapan produksi                          07:00 - 08:00  (60 menit)
       2. Trial conveyor finish good                  08:00 - 08:15  (15 menit)
       3. Trouble m.c gas change (value pump patah)   08:15 - 09:30  (75 menit)
  3. Bila PWK_ACTUAL untuk tanggal/line/shift ini sudah pernah disimpan,
     CatatanJson ditimpa supaya kolom HAMBATAN berisi 1, 2, 3.

  Kolom LossTime di AssemblyLossTime berisi DETIK.
  Script batal (ROLLBACK) bila jumlah loss lama yang ditemukan bukan 6.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @tgl  date        = '2026-10-01';
DECLARE @mc   varchar(20) = 'MCH1-01';
DECLARE @shift varchar(20) = 'Non-Shift';

-- Preview data sebelum diubah
SELECT 'SEBELUM' AS Info, Id, [Date], [Time], EndDateTime, LossTime, Reason, DetailedReason
FROM dbo.AssemblyLossTime
WHERE MachineCode = @mc AND [Date] = @tgl
ORDER BY [Time], Id;

BEGIN TRAN;

DECLARE @deleted TABLE (Id int);

DELETE FROM dbo.AssemblyLossTime
OUTPUT deleted.Id INTO @deleted
WHERE MachineCode = @mc
  AND [Date] = @tgl
  AND Reason = 'Machine & Tools Trouble'
  AND LTRIM(RTRIM(DetailedReason)) = 'Trial conveyor finish good'
  AND CONVERT(char(5), CAST([Time] AS time), 108) IN ('08:05','08:44','09:15','09:25','09:35','09:46');

IF (SELECT COUNT(*) FROM @deleted) <> 6
BEGIN
    DECLARE @n int = (SELECT COUNT(*) FROM @deleted);
    ROLLBACK TRAN;
    RAISERROR('Loss lama yang cocok = %d (harus 6). Dibatalkan, tidak ada perubahan.', 16, 1, @n);
    RETURN;
END;

DECLARE @new TABLE (No int, Id int);

INSERT INTO dbo.AssemblyLossTime ([Date], MachineCode, [Time], LossTime, Reason, EndDateTime, DetailedReason)
VALUES (@tgl, @mc, '07:00:00', 3600, 'Machine & Tools Trouble', '08:00:00', 'Persiapan produksi');
INSERT INTO @new VALUES (1, SCOPE_IDENTITY());

INSERT INTO dbo.AssemblyLossTime ([Date], MachineCode, [Time], LossTime, Reason, EndDateTime, DetailedReason)
VALUES (@tgl, @mc, '08:00:00', 900, 'Machine & Tools Trouble', '08:15:00', 'Trial conveyor finish good');
INSERT INTO @new VALUES (2, SCOPE_IDENTITY());

INSERT INTO dbo.AssemblyLossTime ([Date], MachineCode, [Time], LossTime, Reason, EndDateTime, DetailedReason)
VALUES (@tgl, @mc, '08:15:00', 4500, 'Machine & Tools Trouble', '09:30:00', 'Trouble m.c gas change (value pump patah)');
INSERT INTO @new VALUES (3, SCOPE_IDENTITY());

-- Catatan Masalah (bagian D): Hambatan diisi angka 1-3.
-- Nama properti PascalCase mengikuti CatatanRow (System.Text.Json case-sensitive).
DECLARE @catatan nvarchar(max) =
    N'[' +
    N'{"LossId":' + CAST((SELECT Id FROM @new WHERE No = 1) AS nvarchar(20)) +
        N',"Hambatan":"1","Analisa":"Machine & Tools Trouble: Persiapan produksi","Tindakan":"","Pic":"60 menit"},' +
    N'{"LossId":' + CAST((SELECT Id FROM @new WHERE No = 2) AS nvarchar(20)) +
        N',"Hambatan":"2","Analisa":"Machine & Tools Trouble: Trial conveyor finish good","Tindakan":"","Pic":"15 menit"},' +
    N'{"LossId":' + CAST((SELECT Id FROM @new WHERE No = 3) AS nvarchar(20)) +
        N',"Hambatan":"3","Analisa":"Machine & Tools Trouble: Trouble m.c gas change (value pump patah)","Tindakan":"","Pic":"75 menit"}' +
    N']';

UPDATE dbo.PWK_ACTUAL
SET CatatanJson = @catatan
WHERE Tanggal = @tgl AND MachineCode = @mc AND Shift = @shift;

IF @@ROWCOUNT = 0
    PRINT 'PERHATIAN: PWK_ACTUAL Line CU 2026-10-01 Non-Shift belum pernah disimpan. '
        + 'Loss sudah masuk, tapi Hambatan akan tampil format otomatis (jam). '
        + 'Ubah Hambatan jadi 1/2/3 di halaman lalu klik Simpan.';

COMMIT TRAN;

-- Hasil
SELECT 'SESUDAH' AS Info, Id, [Date], [Time], EndDateTime, LossTime, Reason, DetailedReason
FROM dbo.AssemblyLossTime
WHERE MachineCode = @mc AND [Date] = @tgl
ORDER BY [Time], Id;

SELECT Id, Tanggal, MachineCode, Shift, CatatanJson
FROM dbo.PWK_ACTUAL
WHERE Tanggal = @tgl AND MachineCode = @mc AND Shift = @shift;
