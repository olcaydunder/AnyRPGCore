package com.zootopiayazilim.otuken;

import android.app.Activity;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.OnUserEarnedRewardListener;
import com.google.android.gms.ads.initialization.InitializationStatus;
import com.google.android.gms.ads.initialization.OnInitializationCompleteListener;
import com.google.android.gms.ads.rewarded.RewardItem;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;
import com.google.android.ump.ConsentForm;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.FormError;
import com.google.android.ump.UserMessagingPlatform;

/**
 * Ötüken: AdMob ödüllü reklam köprüsü (Google Mobile Ads SDK + kullanıcı onayı için UMP).
 * Akış: baslat -> UMP onay bilgisi (AB/İngiltere'de onay penceresi) -> reklam izni varsa MobileAds -> ödüllü reklam yüklenir.
 * Unity tarafı AnyRPG.Reklam; olaylar ReklamDinleyici.olay(tür, veri) ile gider:
 *   izin ("1"/"0" gizlilik seçenekleri gerekli mi), hazir, yuklenemedi (kod mesaj), odul, kapandi, gosterilemedi, yok, hata
 * Ödül oyunda (çevrimiçinde sunucuda, günlük sınırla) verilir; köprü yalnız reklamın sonuna kadar izlendiğini bildirir.
 */
public class OtukenReklam {

    private static OtukenReklam ornek;

    private final Activity etkinlik;
    private final ReklamDinleyici dinleyici;
    private final String birim;
    private ConsentInformation izin;
    private RewardedAd reklam;
    private boolean yukleniyor = false;
    private boolean adsBaslatildi = false;
    private boolean odulKazanildi = false;
    private int deneme = 0;

    private OtukenReklam(Activity etkinlik, ReklamDinleyici dinleyici, String birim) {
        this.etkinlik = etkinlik;
        this.dinleyici = dinleyici;
        this.birim = birim;
    }

    public static void baslat(final Activity etkinlik, final ReklamDinleyici dinleyici, final String birim) {
        etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (ornek != null) {
                    ornek.yukle();
                    return;
                }
                ornek = new OtukenReklam(etkinlik, dinleyici, birim);
                ornek.izinIste();
            }
        });
    }

    private void olay(String tur, String veri) {
        try {
            dinleyici.olay(tur, veri);
        } catch (Throwable t) {
            // Unity tarafı kapanmış olabilir
        }
    }

    // ---------------------------------------------------------------- onay (UMP)

    private void izinIste() {
        try {
            izin = UserMessagingPlatform.getConsentInformation(etkinlik);
            ConsentRequestParameters parametreler = new ConsentRequestParameters.Builder().build();
            izin.requestConsentInfoUpdate(etkinlik, parametreler,
                new ConsentInformation.OnConsentInfoUpdateSuccessListener() {
                    @Override
                    public void onConsentInfoUpdateSuccess() {
                        // geri çağrılar sonradan, dıştaki try'ın dışında çalışır: hata oyunu kapatmasın
                        try {
                            if (etkinlik.isFinishing()) {
                                return;
                            }
                            UserMessagingPlatform.loadAndShowConsentFormIfRequired(etkinlik, new ConsentForm.OnConsentFormDismissedListener() {
                                @Override
                                public void onConsentFormDismissed(FormError hata) {
                                    try {
                                        if (hata != null) {
                                            olay("hata", "onay penceresi: " + hata.getMessage());
                                        }
                                        izinBildir();
                                        if (izin.canRequestAds()) {
                                            adsBaslat();
                                        }
                                    } catch (Throwable t) {
                                        olay("hata", "onay sonrası: " + t.getMessage());
                                    }
                                }
                            });
                        } catch (Throwable t) {
                            olay("hata", "onay penceresi: " + t.getMessage());
                        }
                    }
                },
                new ConsentInformation.OnConsentInfoUpdateFailureListener() {
                    @Override
                    public void onConsentInfoUpdateFailure(FormError hata) {
                        try {
                            olay("hata", "onay bilgisi: " + (hata != null ? hata.getMessage() : "?"));
                            if (izin.canRequestAds()) {
                                adsBaslat();
                            }
                        } catch (Throwable t) {
                            olay("hata", "onay bilgisi: " + t.getMessage());
                        }
                    }
                });
            // önceki oturumdan izin varsa beklemeden başla
            if (izin.canRequestAds()) {
                adsBaslat();
            }
        } catch (Throwable t) {
            olay("hata", "onay: " + t.getMessage());
            adsBaslat();
        }
    }

    private void izinBildir() {
        try {
            boolean gerekli = izin != null
                && izin.getPrivacyOptionsRequirementStatus() == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED;
            olay("izin", gerekli ? "1" : "0");
        } catch (Throwable t) {
            olay("hata", "izin: " + t.getMessage());
        }
    }

    /** Ayarlar'daki "Reklam gizlilik seçenekleri": onayı değiştirme penceresi */
    public static void gizlilikSecenekleri() {
        final OtukenReklam o = ornek;
        if (o == null) {
            return;
        }
        o.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    UserMessagingPlatform.showPrivacyOptionsForm(o.etkinlik, new ConsentForm.OnConsentFormDismissedListener() {
                        @Override
                        public void onConsentFormDismissed(FormError hata) {
                            try {
                                if (hata != null) {
                                    o.olay("hata", "gizlilik: " + hata.getMessage());
                                }
                                o.izinBildir();
                                if (o.izin != null && o.izin.canRequestAds()) {
                                    o.adsBaslat();
                                }
                            } catch (Throwable t) {
                                o.olay("hata", "gizlilik: " + t.getMessage());
                            }
                        }
                    });
                } catch (Throwable t) {
                    o.olay("hata", "gizlilik: " + t.getMessage());
                }
            }
        });
    }

    // ---------------------------------------------------------------- reklam

    private void adsBaslat() {
        if (adsBaslatildi) {
            return;
        }
        adsBaslatildi = true;
        // Google önerisi: başlatma ana iş parçacığını bekletmesin
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    MobileAds.initialize(etkinlik, new OnInitializationCompleteListener() {
                        @Override
                        public void onInitializationComplete(InitializationStatus durum) {
                            try {
                                etkinlik.runOnUiThread(new Runnable() {
                                    @Override
                                    public void run() {
                                        yukle();
                                    }
                                });
                            } catch (Throwable t) {
                                olay("hata", "başlatma sonrası: " + t.getMessage());
                            }
                        }
                    });
                } catch (Throwable t) {
                    olay("hata", "başlatma: " + t.getMessage());
                }
            }
        }).start();
    }

    private void yukle() {
        if (adsBaslatildi == false || reklam != null || yukleniyor || etkinlik.isFinishing()) {
            return;
        }
        yukleniyor = true;
        try {
            RewardedAd.load(etkinlik, birim, new AdRequest.Builder().build(), new RewardedAdLoadCallback() {
                @Override
                public void onAdLoaded(RewardedAd yuklenen) {
                    reklam = yuklenen;
                    yukleniyor = false;
                    deneme = 0;
                    olay("hazir", "");
                }

                @Override
                public void onAdFailedToLoad(LoadAdError hata) {
                    reklam = null;
                    yukleniyor = false;
                    deneme++;
                    olay("yuklenemedi", hata != null ? hata.getCode() + " " + hata.getMessage() : "?");
                }
            });
        } catch (Throwable t) {
            yukleniyor = false;
            olay("hata", "yükleme: " + t.getMessage());
        }
    }

    /** reklam yüklenmemişse yeniden dener (Unity, yüklenemedi olayından bir süre sonra çağırır) */
    public static void yenidenYukle() {
        final OtukenReklam o = ornek;
        if (o == null) {
            return;
        }
        o.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                o.yukle();
            }
        });
    }

    public static void goster() {
        final OtukenReklam o = ornek;
        if (o == null) {
            return;
        }
        o.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                o.gosterUi();
            }
        });
    }

    private void gosterUi() {
        if (reklam == null) {
            olay("yok", "");
            yukle();
            return;
        }
        odulKazanildi = false;
        RewardedAd r = reklam;
        r.setFullScreenContentCallback(new FullScreenContentCallback() {
            @Override
            public void onAdDismissedFullScreenContent() {
                reklam = null;
                olay(odulKazanildi ? "odul" : "kapandi", "");
                try {
                    yukle();
                } catch (Throwable t) {
                    olay("hata", "yükleme: " + t.getMessage());
                }
            }

            @Override
            public void onAdFailedToShowFullScreenContent(AdError hata) {
                reklam = null;
                olay("gosterilemedi", hata != null ? hata.getMessage() : "");
                try {
                    yukle();
                } catch (Throwable t) {
                    olay("hata", "yükleme: " + t.getMessage());
                }
            }
        });
        try {
            r.show(etkinlik, new OnUserEarnedRewardListener() {
                @Override
                public void onUserEarnedReward(RewardItem odul) {
                    odulKazanildi = true;
                }
            });
        } catch (Throwable t) {
            reklam = null;
            olay("gosterilemedi", t.getMessage());
            yukle();
        }
    }
}
