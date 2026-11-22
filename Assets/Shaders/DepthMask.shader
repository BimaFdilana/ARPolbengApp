Shader "Custom/DepthMask"
{
    SubShader
    {
        // Render di antrian Geometry-1 agar digambar SEBELUM objek lain
        Tags { "Queue" = "Geometry-1" }
        
        // Jangan menggambar warna apa pun (R, G, B, A dimatikan)
        ColorMask 0
        
        // TAPI, tetap tulislah data kedalaman (Z-Buffer)
        ZWrite On

        Pass
        {
            // Pass kosong, hanya untuk mengaktifkan ZWrite
        }
    }
}