# SubPanamon

Proyek .NET 8 terpisah dari `MonitoringSystem`, berisi 3 halaman:

| Menu sidebar | URL |
|---|---|
| PWK Actual (halaman awal) | `/subpanamon/PWKActual` |
| Machine Efficiency | `/subpanamon/MachineEfficiency` |
| PWK | `/subpanamon/Pwk` |

Membuka `/subpanamon` langsung diarahkan ke PWK Actual.

## Konfigurasi

Isi password database di `appsettings.json` (`ISI_PASSWORD`):

- `DefaultConnection`: database PROMOSYS (PWK Actual & PWK)
- `MachineConnection`: database MachineDB (Machine Efficiency)

`PathBase`:

- **IIS sub-application** (`/subpanamon` di bawah site 6003): biarkan `""`, IIS yang mengatur path.
- **Jalan sendiri** (Kestrel / service): isi `"/subpanamon"`.

## Menjalankan lokal

```cmd
cd SubPanamon
dotnet run
```

Buka `http://localhost:5140/subpanamon`.

## Publish

```cmd
cd SubPanamon
dotnet publish -c Release -o publish
```

Lalu salin isi folder `publish` ke server. Di IIS: klik kanan site port 6003 → **Add Application** → Alias `subpanamon`, Physical path = folder publish, Application pool sendiri (**No Managed Code**).
