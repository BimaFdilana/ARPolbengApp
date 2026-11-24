using UnityEngine;

public static class ApiConfig
{
    // --- GANTI URL DI SINI SAJA (SATU TEMPAT UNTUK SEMUA) ---
    
    // Produksi (VPS, HTTPS lewat Cloudflare Tunnel) - dipakai Editor & HP Android
    public const string BaseUrl = "https://ar-indoor-nav.artitechlabs.com/api";

    // Opsi lain: Localhost (Untuk Editor, saat menjalankan `php artisan serve`)
    // public const string BaseUrl = "http://127.0.0.1:8000/api";
    
    // ------------------------------------------------------
}