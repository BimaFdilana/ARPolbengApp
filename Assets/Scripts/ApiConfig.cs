using UnityEngine;

public static class ApiConfig
{
    // --- GANTI URL DI SINI SAJA (SATU TEMPAT UNTUK SEMUA) ---
    
    // Opsi 1: Localhost (Untuk Editor)
    public const string BaseUrl = "http://127.0.0.1:8000/api";

    // Opsi 2: IP Laptop (Untuk HP Android - GANTI IP INI)
    // public const string BaseUrl = "https://pin-lab.virtualtouralamayangpku.my.id/api"; 

    // Opsi 3: Ngrok / Hosting (Untuk Internet)
    // public const string BaseUrl = "https://myapp.ngrok.io/api";
    
    // ------------------------------------------------------
}