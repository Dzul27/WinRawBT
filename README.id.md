# WinRawBT: cetak struk dari POS web ke printer thermal Bluetooth di Windows

[English](README.md) | **Bahasa Indonesia**

**WinRawBT adalah aplikasi tray Windows gratis dan open source yang memungkinkan POS berbasis browser mencetak struk ESC/POS ke printer thermal Bluetooth yang sudah dipasangkan (paired).** Secara bawaan aplikasi ini mendengarkan di `http://localhost:8080/` dan `ws://localhost:8080/` (port bisa diubah di aplikasi), menerima byte ESC/POS mentah, lalu mengirimkannya lewat soket Bluetooth RFCOMM (Serial Port Profile). Tanpa setting port COM, tanpa dialog cetak Windows, tanpa langkah instal driver printer.

Ini versi Windows dari ide aplikasi Android **RawBT**: pilih printer yang sudah paired, klik *Start Service*, lalu cetak. Unduhannya **sekitar 132 KB dalam bentuk zip** (`.exe` 140 KB plus satu DLL) dan berjalan di .NET Framework 4.8 yang sudah ada di Windows 10 dan 11.

![Jendela utama WinRawBT: daftar printer Bluetooth, tombol Start Service dan Test Connection](docs/screenshot.png)

> **Status: beta (v0.1.x).** Tidak berafiliasi dengan atau didukung oleh proyek RawBT. Lihat [Printer yang sudah diuji](#printer-yang-sudah-diuji) dan [Masalah yang diketahui](#masalah-yang-diketahui).

**Langsung ke:** [Instalasi](#instalasi) · [Izinkan POS web](#izinkan-pos-web-anda-penting) · [Pemakaian](#pemakaian-dari-halaman-web) · [FAQ](#faq) · [Perbandingan](#bagaimana-perbandingan-winrawbt-dengan-opsi-lain) · [Printer yang diuji](#printer-yang-sudah-diuji)

## Fitur

- Menampilkan perangkat Bluetooth yang sudah paired, atau memindai dan memasangkan perangkat baru (Secure Simple Pairing, tanpa menebak PIN).
- Tombol Test Connection mencetak struk diagnostik.
- Endpoint HTTP `POST` dan WebSocket lokal, hanya di `localhost`.
- Menerima byte mentah, Base64, URL gaya RawBT `rawbt:base64,...` / `intent:base64,...`, URI `data:`, bungkus JSON, dan teks hex.
- Connect-on-demand: soket RFCOMM dibuka per pekerjaan cetak, jadi printer yang tidur tetap bisa dipakai. Pekerjaan cetak diantrekan satu per satu.
- System tray, opsi jalan otomatis saat Windows start, dan log aktivitas dengan pratinjau hex untuk setiap pekerjaan.
- Satu executable .NET Framework 4.8 yang kecil (sudah bawaan Windows 10/11), satu dependensi: [32feet.NET](https://github.com/inthehand/32feet).

## Persyaratan

- Windows 10 atau 11 dengan adapter Bluetooth yang berfungsi
- Printer thermal Bluetooth yang mendukung Serial Port Profile (SPP)
- Untuk build: .NET SDK 8 atau lebih baru (target `net48`)

## Instalasi

1. Unduh zip dari [Releases](../../releases) dan ekstrak di mana saja (simpan `WinRawBT.exe` dan `InTheHand.Net.Bluetooth.dll` di folder yang sama).
2. Jalankan `WinRawBT.exe`, pilih printer, klik **Test Connection**, lalu **Start Service**.

## Pilih website yang boleh mencetak

Secara default **website mana pun** boleh mengirim print job ke bridge, jadi POS web langsung jalan tanpa setup. Bridge hanya mendengarkan di `localhost`, tetapi itu juga berarti halaman apa pun yang terbuka di browser pada PC yang sama bisa mencetak.

Klik tombol **Allowed websites** di jendela utama untuk memilih salah satu dari dua mode. Perubahan langsung berlaku, tanpa restart:

- **Allow all websites** (default): semua website boleh mencetak. Paling mudah.
- **Only allow these websites**: ketik alamat POS Anda (misalnya `pos.example.com`) lalu klik **Add**. Selainnya mendapat HTTP 403.

Pilihan disimpan di `%AppData%\WinRawBT\config.json` (`AllowAnyOrigin`, `AllowedOrigins`); file itu juga bisa diedit manual saat aplikasi tertutup. Origin harus persis (skema + host + port, tanpa path); halaman yang dibuka dari path `file://` mengirim origin `null`. Request tanpa header `Origin` (curl, aplikasi desktop) selalu diterima. Origin yang ditolak muncul di Activity Log beserta caranya.

## Pemakaian dari halaman web

```js
// Byte mentah
await fetch("http://localhost:8080/", {
  method: "POST",
  headers: { "Content-Type": "application/octet-stream" },
  body: new Uint8Array([0x1b, 0x40, ...new TextEncoder().encode("Halo\n"), 0x1d, 0x56, 0x42, 0x00]),
});

// Base64
await fetch("http://localhost:8080/", {
  method: "POST",
  headers: { "Content-Type": "text/plain" },
  body: btoa("\x1b@Halo\n\x1dV\x42\x00"),
});

// WebSocket
const ws = new WebSocket("ws://localhost:8080/");
ws.onopen = () => ws.send(btoa("\x1b@Halo\n"));
```

Respons: `200 {"status":"OK","bytesSent":N}`, `400` payload salah/kosong, `403` origin tidak diizinkan, `500` printer tidak terjangkau. `GET /` mengembalikan status service.

Dari shell: `curl -X POST --data-binary @receipt.bin -H "Content-Type: application/octet-stream" http://localhost:8080/`

`TestClient/test-pos.html` adalah halaman uji siap pakai (tambahkan `"null"` ke `AllowedOrigins` jika dibuka sebagai file lokal).

Contoh memakai port bawaan 8080; ganti `8080` dengan port Anda jika sudah mengubahnya.

## Pengaturan lain

| Key | Default | Arti |
|---|---|---|
| `Port` | `8080` | Port listener. Ubah lewat kolom **Port** di jendela utama (hanya saat service berhenti, rentang 1024-65535) atau di sini. URL di POS harus memakai port yang sama |
| `AutoStartService` | `false` | Jalankan service saat aplikasi dibuka jika printer sudah tersimpan |
| `MinimizeToTray` | `true` | Menutup jendela akan menyembunyikannya ke tray |
| `PrependInit` | `false` | Tambahkan `ESC @` di depan pekerjaan yang belum diawali itu |

## FAQ

### Bagaimana cara cetak dari POS web ke printer thermal Bluetooth di Windows?

Pasangkan (pair) printer di Windows, jalankan WinRawBT, pilih printer, klik **Start Service**, lalu dari halaman POS panggil `fetch("http://localhost:8080/", { method: "POST", body: bytes })` dengan byte ESC/POS. Contoh lengkap ada di [Pemakaian dari halaman web](#pemakaian-dari-halaman-web).

### Apakah ada RawBT untuk Windows?

RawBT adalah aplikasi Android dan tidak punya versi Windows. WinRawBT memakai alur kerja yang sama di Windows (printer paired, endpoint lokal, byte ESC/POS mentah) dan menerima payload gaya RawBT `rawbt:base64,...`. Proyek ini independen dan tidak berafiliasi dengan RawBT.

### Kenapa tidak pakai Web Bluetooth API di browser?

Web Bluetooth hanya bisa berbicara dengan perangkat Bluetooth Low Energy (GATT). Kebanyakan printer struk 58 mm dan 80 mm yang murah memakai Bluetooth klasik dengan Serial Port Profile (SPP/RFCOMM), yang tidak bisa dibuka oleh halaman web. WinRawBT membuka soket RFCOMM itu dari proses native dan menyediakannya ke halaman web lewat `localhost`.

### Apakah perlu membuat port COM atau menginstal driver printer?

Tidak. WinRawBT tersambung langsung ke perangkat paired lewat soket Bluetooth RFCOMM (memakai 32feet.NET). Anda tidak perlu memetakan port COM virtual, dan pekerjaan cetak tidak melewati dialog cetak maupun spooler Windows karena byte dikirim mentah.

### Printer Bluetooth apa saja yang bisa dipakai?

Printer yang menyediakan Serial Port Profile (SPP) lewat Bluetooth klasik dan menerima ESC/POS. Yang sudah terkonfirmasi: EPPOS 58 mm. Lihat [Printer yang sudah diuji](#printer-yang-sudah-diuji) dan mohon [laporkan model printer Anda](../../issues/new?template=printer-report.yml). Printer BLE-only tidak didukung.

### Apa arti error 10049 AddressNotAvailable?

Windows tidak menemukan layanan SPP di perangkat. Penyebab umumnya printer dual-mode atau BLE-only, atau dipasangkan dengan cara yang tidak mendaftarkan layanan "Serial Port". Hapus printer di pengaturan Bluetooth Windows, pasangkan ulang sebagai perangkat Bluetooth klasik, lalu pastikan Windows menampilkan layanan "Serial Port" untuk perangkat itu.

### Apakah aman menjalankan print server lokal yang bisa diakses website?

Bridge hanya terikat ke `127.0.0.1`. Tidak ada autentikasi, dan secara default website mana pun yang terbuka di PC itu bisa mencetak (`AllowAnyOrigin: true`). Jangan membuka port ke jaringan; isi `AllowAnyOrigin` dengan `false` dan daftarkan POS Anda di `AllowedOrigins` bila PC itu juga dipakai membuka situs yang tidak Anda percaya.

### Apakah bisa mengonversi gambar atau HTML ke ESC/POS?

Tidak. WinRawBT hanya meneruskan byte. Buat byte ESC/POS di sisi POS Anda (library encoder ESC/POS apa pun bisa) lalu kirim hasilnya.

## Bagaimana perbandingan WinRawBT dengan opsi lain?

| | WinRawBT | RawBT | QZ Tray | Web Bluetooth |
|---|---|---|---|---|
| Platform | Windows 10/11 | Android | Windows, macOS, Linux | Browser Chromium |
| Printer Bluetooth klasik (SPP) | Ya, RFCOMM langsung | Ya | Biasanya lewat port COM virtual | Tidak (hanya BLE) |
| Perlu setting port COM | Tidak | Tidak | Biasanya perlu untuk Bluetooth | Tidak |
| Antarmuka untuk POS web | HTTP + WebSocket di `localhost` | Intent / URL scheme aplikasi | WebSocket + library JS | API browser |
| Runtime | .NET Framework 4.8 (bawaan), zip sekitar 132 KB | Aplikasi Android | Java | Tidak ada |
| Lisensi | MIT | Aplikasi gratis | Open source | n/a |

Perbandingan ini mengacu pada deskripsi publik tiap proyek saat ditulis dan hanya sebagai orientasi, bukan benchmark. Periksa dokumentasi proyek lain sebelum memutuskan.

## Build

```
dotnet build -c Release
```

Hasilnya ada di `bin/Release/net48/`.

## Printer yang sudah diuji

| Printer | Hasil |
|---|---|
| Printer thermal EPPOS 58 mm | Berfungsi: struk tes dan pencetakan dari bridge |
| Model lain | Silakan [buka laporan printer](../../issues/new?template=printer-report.yml) dengan model printer, versi Windows, dan isi Activity Log |

## Masalah yang diketahui

- Beberapa printer gagal tersambung dengan `10049 AddressNotAvailable` (Windows tidak menemukan layanan SPP di perangkat). Penyebab umumnya printer dual-mode / BLE-only. Pasangkan ulang printer sebagai perangkat Bluetooth klasik dan pastikan Windows menampilkan layanan "Serial Port" untuk perangkat itu.
- Tidak ada konversi gambar atau HTML ke ESC/POS; bridge hanya meneruskan byte.
- Hanya satu printer dalam satu waktu.

## Catatan keamanan

- Hanya terikat ke `localhost` / `127.0.0.1`. Tidak ada autentikasi; perlindungan terhadap website adalah allowlist origin di atas.
- Jangan membuka port ini ke jaringan.

## Kontribusi

Issue dan pull request diterima. Untuk masalah printer, sertakan model printer dan isi Activity Log. Riwayat rilis ada di [CHANGELOG.md](CHANGELOG.md).

## Lisensi

[MIT](LICENSE). Memakai 32feet.NET (MIT).
