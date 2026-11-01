# Ringkasan Sesi 22 Oktober – 1 November 2026

Lanjutan dari `2026-10-20-ringkasan-sesi.md` (baca itu dulu untuk dasar proyek). Catatan ini berisi semua yang berubah sesudahnya.
Kalau catatan dan kode berbeda, **kode yang benar**.

---

## 1. Proyek, lokasi, publish

| Proyek | Folder | Jalan di | Publish (cmd) |
|---|---|---|---|
| **Panamon** (MonitoringSystem) | `Panamon_Now user 1\Panamon_Now\MonitoringSystem` | IIS server 10.83.33.103:6003 (PC `29485ACP101-103`) | `cd /d "...\MonitoringSystem"` lalu `dotnet publish MonitoringSystem.csproj -c Release -o publish` |
| **PLC ROHIB / Plclogger** | `Panamon_Now\PLC_ROHIB\Plclogger` | ikut Panamon, port 5000, proxy `/plcrohib-app/` | otomatis ikut publish Panamon ke `publish\PlcRohib` |
| **Dashboard AC** (terpisah) | `Panamon_Now user 1\Dashboard AC-20260828T030209Z-1-001\Dashboard AC` | IIS 10.83.33.103:5009 | `dotnet publish DashboardAC.csproj -c Release -o publish` |
| **Scanner AC (EVACOND)** | `Panamon_Now user 1\INI YANTO SCANNERNYA\SCANNER AC JULI (2)\SCANNER AC JULI\Panasonic_ScannerAC\iniReal` | | `dotnet publish iniReal\iniReal.csproj -c Release -o publish-evacond` lalu ganti `"Line": "ASSEMBLY"` → `"EVACOND"` di `publish-evacond\appsettings.json` |

Pasang ke server: stop app pool, **end task `Plclogger.exe`** (kalau Plclogger berubah), salin isi `publish` **tanpa menimpa `appsettings.json` server**, start app pool, buka Panamon sekali.

## 2. PLC (semua Mitsubishi MC 3E binary, port 5010)

| Source | IP | appsettings (PlcRohib) | Kode mesin |
|---|---|---|---|
| Expander Kyoshin 6.35 | 192.168.1.30 | `PlcConfig` | `MCH1-01` |
| Brazing (1 PLC) | 192.168.1.10 | `BrazingPlc` | `BRAZING-EVA`, `BRAZING-CON` |
| Fin Press (1 PLC) | 192.168.1.100 | `FinPressPlc` | `FINPRESS-FX80/-FIX36/-SF250/-FIX12` |
| Press Shop (1 PLC) | **192.168.1.115** (31-10 masih .110, diganti user 01-11) | `PressShopPlc` | `PRESSSHOP-200T/-110TA/-110TK` |

Laptop user (Wi-Fi kantor) **tidak** menjangkau 192.168.1.x; tes PLC sungguhan hanya dari server. Port 5010 Brazing menolak 2 koneksi bersamaan → semua akses PLC per PLC lewat lock + cache.

### Brazing — PENTING
- Tabel plan **persis Excel**: Condenser D300.. (SUT D1500, Plan D1620, Actual D1740, Plan SUT D1860, Defect D1980, Date PSI D2100, Ket Defect R2821), Evaporator D3300.. (D4500/D4620/D4740/D4860/D4980/D5100, R5821).
- **HOME DITUKAR (31-10)**: bukti log 10 hari → HOME D200 selalu model master Evaporator, D240 model master Condenser. Sekarang `BrazingLines`: **Condenser HOME D240 + D199, Evaporator HOME D200 + D198**. Tag alamat di panel HOME halaman Plan ikut ditukar.
- Timer loss tetap: Evaporator D6810, Condenser D6811.
- Data log 20–31 Okt (PlcKyoshinTrend/ChangePlan/DailyOutput/ShiftOutput) **masih tertukar kodenya** — menunggu izin user untuk menukar di DB.

### Press Shop (file `addres Machine prsss shop (version 1).xlsx`)
- Susunan sama dengan Fin Press: plan 200T D300.., 110T Amada D2100.., 110T Komatsu D3900.. (Model 10 word, SUT, P.Plan, Actual, Tanggal, Bulan); GOT R0/R120/R240; HOME 200T D200/D210, D280–D283; 110TA D220/D230, D284–D287; 110TK D240/D250, D288–D291.
- Output = ACTUAL HOME (D282/D286/D290); ACTUAL2 D7242.. diabaikan. Tidak ada defect.
- **Loss (01-11)**: D296 200T, D297 110T Amada, D298 110T Komatsu (menit, logika = Brazing).

## 3. Yang selesai 22-10 s/d 01-11

**Plclogger**
- Fin Press: kirim / cek / baca PLC, master FX80 (14 kode part, SUT 20) — 27-10.
- Rumus counter A+B (koreksi kecil = 0, reset = dari 0; target waktu jalan / SUT saat Plan SUT tertahan) — 27-10.
- Press Shop: `PressShopLines`, `PressPlcOf()` (fungsi *FinPress* dipakai bersama Fin Press & Press Shop), endpoint `/api/pressshop/plc/send|status|read`, `PressShopTrendLogger`, `PressShopLossLogger` (pakai `BrazingLossTracker`).
- **Kirim plan hanya yang berubah** (`WritePlanTableDiff`, Brazing + Fin Press + Press Shop): baca isi lama, cocokkan baris per Model + tanggal; urutan pindah → Actual/Plan SUT/Defect/Ket Defect ikut pindah; plan baru / tanggal belum mulai → hasil 0; baris tidak berubah tidak disentuh. Dites di PLC tiruan (MelsecMcServer), belum ke PLC asli.
- Endpoint `/api/brazing/plc/home` (HOME Brazing live, cache 3 dtk) untuk layar TV.

**Panamon**
- Inventory (menu RAC, `Pages/Inventory`): EVA/COND = `PlcKyoshinDailyOutput` BRAZING-EVA/CON, berdiri sendiri (tidak ikut All), baris 0 disembunyikan.
- `/planpressshop`, Performance AC OEE dropdown 200 Ton / 110 Ton Amada / 110 Ton Komatsu.

**Dashboard AC**
- Machine Efficiency: mesin PLC tanpa actual & plan hari ini = "-" (balon abu-abu); Press Shop (200 Ton / 110 Ton Amada / 110 Ton Komatsu) dari PLC.
- **Layar TV `/evaporator` & `/condenser`** (`Views/Home/EvaCond.cshtml`, TV Panasonic 32" NS500, 1366×768, satu line per TV):
  - DAILY PLAN = Prod Plan editor Plan Brazing bertanggal hari itu; tidak ada di editor → PSI harian `SapPlan` (CU→Condenser, CS→Evaporator, NS, tanpa KIOS & DN).
  - ACTUAL = output hari 07:00–07:00; EFFICIENCY = OEE (Operating × Ability × Quality), warna <80/<85/≥85.
  - CURRENT RUNNING dari HOME PLC: MODEL, PROD. PLAN, ACTUAL, PLAN (Plan by SUT + logika Performance), TAKT TIME (plan = SUT master, act = rata-rata 5 menit).
  - **P/H/H** = ACTUAL harian ÷ (jumlah orang × jam kerja 07:00 s/d sekarang tanpa istirahat). Jumlah orang dari appsettings Dashboard AC `EvaCond:Operators` (user isi EVA 13, CON 12).
  - Update realtime: cek `/evacond/live` tiap 3 dtk, angka berubah saat ACTUAL masuk; cadangan muat ulang 15 dtk; data lengkap tiap 60 dtk.
  - Klik "Date" → pilih tanggal (riwayat, CURRENT RUNNING kosong, kembali otomatis 5 menit).
  - appsettings Dashboard AC: `"PlcRohib": { "BaseUrl": "http://localhost:5000" }`, `"EvaCond": { "Operators": { ... } }`.

## 4. Status publish terakhir (lokal, harus dipasang user di server)
- Panamon + Plclogger: `MonitoringSystem\publish` — Plclogger 01-11 10:12 (HOME Brazing ditukar, kirim-diff, loss Press Shop, IP Press Shop .115).
- Dashboard AC: `Dashboard AC\publish` — 31-10 14:18 (layar TV + pilih tanggal + P/H/H).
- Per 31-10 13:52 server **masih Plclogger lama** (HOME Brazing belum tertukar). Log Press Shop berhenti 31-10 11:03 (PLC tidak terhubung; IP lalu diganti .115).

## 5. Tertunda / menunggu user
1. Izin menukar kode BRAZING-EVA ↔ BRAZING-CON untuk data log Oktober.
2. Press Shop: master data model + SUT (200 Ton / 110 Ton Amada / 110 Ton Komatsu masih 0 model), kirim plan dari web, sumber rencana, cek koneksi IP .115, halaman assign reason & AC OEE lain (Production Achievement, Loss Time, Quality, Inventory).
3. Fin Press: master FIX36/SF250/FIX12, sumber rencana, logger.
4. P/H/H di Production Achievement AC OEE Panamon (rumus & sumber jumlah orang) — ditunda user.
5. Efficiency layar TV: tetap OEE atau Actual ÷ Plan by SUT saja — belum dijawab user.
6. Timer loss Brazing berjalan saat line idle & Plan SUT PLC tetap naik tanpa produksi → tanya orang PLC.
7. Halaman Production Data / Quality / Inventory AC OEE untuk Evaporator/Condenser.

## 6. Harness bantu (scratchpad sesi, bukan bagian proyek)
`C:\Users\70N0141\AppData\Local\Temp\claude\C--Users-70N0141-rudy-pens\775fdff4-f024-433b-833c-d83f31803265\scratchpad\`
- `otherq` — query baca-saja ke DB (pakai config Plclogger).
- `difftest` — uji Plclogger ke PLC tiruan `MelsecMcServer` (tanpa DB, tanpa PLC asli).
- `xl_dump.ps1` — baca isi .xlsx tanpa Python.
