# Laporan Pengerjaan 16 – 22 Oktober 2026

## Jumat, 16 Oktober 2026 — Dashboard AC
- Analisis aplikasi Dashboard AC; diputuskan tetap aplikasi terpisah dengan alamat sendiri.
- Tabel Machine Efficiency: Expander Kyoshin 6.35, Evaporator, dan Condenser ditaruh di urutan teratas.
- OEE, Operating, Ability, dan Quality Expander Kyoshin 6.35 disamakan dengan rumus Production Data AC OEE, dihitung untuk satu hari produksi penuh (07:00–07:00, semua model).
- Batas nilai: Ability tidak dibatasi 100%, Operating dan Quality maksimal 100%.
- Pengecekan rumus Plan by SUT dan rekap defect harian.
- Publish Dashboard AC.

## Senin, 19 Oktober 2026 — Dashboard AC, Panamon AC OEE, Scanner AC
**Dashboard AC**
- Total Plan CU/CS mengambil SAP Plan selama operator belum menginput plan.
- Perbaikan selisih data loss: sumber loss diambil langsung dari PROMOSYS (tabel sinkron lama tidak pernah menghapus data).
- Perbaikan status warna merah/kuning/hijau CU dan CS.
- Break time ditampilkan di grafik sebagai penanda (bukan loss); legenda Working Time diganti Break Time; load time tidak dikurangi loss.
- Klik bar loss membuka Detail Loss Time; tooltip menampilkan rentang waktu dan menit loss.
- Klik kartu mengarah ke halaman Performance AC OEE.
- Publish Dashboard AC (beberapa kali).

**Panamon AC OEE**
- Hapus 23 data loss Expander kategori "Other" yang reason-nya belum diisi di GOT.
- Menu Plan Machine langsung membuka Plan Expander Kyoshin 6.35 (tanpa popup), dengan pilihan SOURCE Expander / Brazing.
- Import SAP Plan / PSI: model yang tidak ada di master data memakai SUT dummy 35 detik dengan keterangan; import Oktober dijalankan manual (CU 34.156, CS 34.158).
- Plan Brazing: tombol "Isi dari Rencana Database" mengambil SAP Plan harian (CU = Condenser, CS = Evaporator, tanpa KIOS & DN), urutan mengikuti prioritas PSI.
- Inventory AC OEE: grafik "Expander Kyoshin 6.35 Actual vs Plan (Assembly)" di samping tabel, filter tanggal (default bulan berjalan), beserta penyesuaian tampilan.

**Scanner AC**
- Halaman EVACOND (Evaporator / Condenser) dan halaman Loss Time Evaporator / Condenser (tampilan awal).

## Selasa, 20 Oktober 2026 — Plan Brazing terhubung PLC
- SUT Condenser (69 model) dan Evaporator (67 model) dari master data PROMOSYS dimasukkan ke Master Data Produk; model tanpa SUT = 35 detik.
- Kirim plan Brazing ke PLC Mitsubishi 192.168.1.10 (Model, SUT, Prod Plan, Date PSI) dengan verifikasi baca ulang — terbukti terkirim ke PLC.
- Tombol "Cek Koneksi PLC" di halaman Plan Brazing.
- Perbaikan saran model Evaporator (model CS) dan tombol "Tarik Nilai Aktual".
- Live Preview dan layar HOME Brazing: perbaikan kolom HOME, baca PLC dibagi ke semua pengguna (cache), akses PLC antre satu per satu (port PLC hanya menerima satu koneksi), tombol pindah line di layar HOME.
- Ket Defect dibaca dari PLC dan ditempelkan ke model yang sedang berjalan.
- Logger Brazing: data layar HOME dicatat tiap menit, beserta Change Plan, output harian, dan output per shift (struktur sama dengan Expander).
- Performance AC OEE: dropdown mesin Expander / Evaporator / Condenser.
- Catatan sesi dan prompt untuk agen lain.
- Publish Panamon.

## Rabu, 21 Oktober 2026 — Plan Machine & GitHub
- Hapus 6 data loss Expander "Other" tanpa reason (180 menit).
- Plan Machine: LINE MESIN Expander berisi Kyoshin 6.35 dan Li-chin (Li-chin disiapkan, menunggu master data & address PLC).
- Pengecekan address plan Brazing terhadap file Excel (tetap sesuai Excel).
- Date PSI dikirim ke PLC dengan format tanggal-bulan-tahun (contoh 12-10-2026).
- SOURCE Fin Press (FX80, FIX36, SF250, FIX12): address plan D300–D6449 disiapkan sesuai Excel, halaman Panamon /planfinpress.
- Push seluruh kode laptop ke branch main GitHub (main lama dicadangkan ke branch backup).

## Kamis, 22 Oktober 2026 — Loss Time Brazing & halaman AC OEE
- Penjelasan aturan jam istirahat untuk perhitungan loss time.
- Loss time Brazing dari timer PLC (Evaporator D6810, Condenser D6811): dicatat otomatis, dipotong per shift, tanpa jam istirahat — logika sama dengan Expander.
- Scanner AC: halaman Loss Time Evaporator / Condenser untuk assign reason (kategori sama dengan Expander); loss yang belum di-assign dihitung sebagai Other.
- Evaporator & Condenser ditambahkan di Production Achievement, Detail Loss Time, dan Trend Loss Time AC OEE.
- Dashboard AC: OEE Evaporator & Condenser dihitung dengan rumus yang sama, warna merah/kuning/hijau dan balon denah otomatis, klik baris membuka Performance.
- Publish Panamon, Dashboard AC, dan Scanner EVACOND.
- Perbaikan error halaman Plan Machine di server (koneksi Panamon ke PLC ROHIB tidak lagi lewat proxy kantor).
