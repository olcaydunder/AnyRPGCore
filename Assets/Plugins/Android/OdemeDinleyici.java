package com.zootopiayazilim.otuken;

/**
 * Ötüken: Google Play ödeme olaylarının Unity'ye (AnyRPG.Odeme, AndroidJavaProxy) gittiği arayüz.
 */
public interface OdemeDinleyici {
    void olay(String tur, String veri);
}
