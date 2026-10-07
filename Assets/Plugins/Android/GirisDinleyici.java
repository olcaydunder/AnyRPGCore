package com.zootopiayazilim.otuken;

/**
 * Ötüken: Google Play Oyun Hizmetleri giriş olaylarının Unity'ye (AnyRPG.GoogleGiris, AndroidJavaProxy) gittiği arayüz.
 */
public interface GirisDinleyici {
    void olay(String tur, String veri);
}
