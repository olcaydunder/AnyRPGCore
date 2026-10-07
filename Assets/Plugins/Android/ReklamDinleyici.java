package com.zootopiayazilim.otuken;

/**
 * Ötüken: AdMob ödüllü reklam olaylarının Unity'ye (AnyRPG.Reklam, AndroidJavaProxy) gittiği arayüz.
 */
public interface ReklamDinleyici {
    void olay(String tur, String veri);
}
