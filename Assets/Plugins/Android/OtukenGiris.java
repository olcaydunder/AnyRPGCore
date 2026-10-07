package com.zootopiayazilim.otuken;

import android.app.Activity;

import com.google.android.gms.games.AuthenticationResult;
import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.google.android.gms.games.Player;
import com.google.android.gms.tasks.OnCompleteListener;
import com.google.android.gms.tasks.Task;

/**
 * Ötüken: Google Play Oyun Hizmetleri (v2) girişi köprüsü.
 *  - baslat: SDK açılır (Play Games kendiliğinden girişi dener), giriş durumu ve oyuncu adı bildirilir
 *  - girisYap: oyuncu girmemişse Play Games giriş penceresi
 *  - kodIste: sunucumuz için tek kullanımlık "sunucu yetki kodu" (sunucu bunu Google'da doğrular, oyuncu kimliğini alır)
 * Olaylar GirisDinleyici.olay(tür, veri): durum ("1"/"0"), oyuncu ("ad\tkimlik"), kod (kod), kodHata, hata
 */
public class OtukenGiris {

    private static OtukenGiris ornek;

    private final Activity etkinlik;
    private final GirisDinleyici dinleyici;

    private OtukenGiris(Activity etkinlik, GirisDinleyici dinleyici) {
        this.etkinlik = etkinlik;
        this.dinleyici = dinleyici;
    }

    private void olay(String tur, String veri) {
        try {
            dinleyici.olay(tur, veri);
        } catch (Throwable t) {
            // Unity tarafı kapanmış olabilir
        }
    }

    public static void baslat(final Activity etkinlik, final GirisDinleyici dinleyici) {
        etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (ornek == null) {
                    ornek = new OtukenGiris(etkinlik, dinleyici);
                    try {
                        PlayGamesSdk.initialize(etkinlik);
                    } catch (Throwable t) {
                        ornek.olay("hata", "başlatma: " + t.getMessage());
                        return;
                    }
                }
                ornek.durumSor();
            }
        });
    }

    private GamesSignInClient istemci() {
        return PlayGames.getGamesSignInClient(etkinlik);
    }

    private void durumSor() {
        try {
            istemci().isAuthenticated().addOnCompleteListener(new OnCompleteListener<AuthenticationResult>() {
                @Override
                public void onComplete(Task<AuthenticationResult> gorev) {
                    boolean girdi = gorev.isSuccessful() && gorev.getResult() != null && gorev.getResult().isAuthenticated();
                    olay("durum", girdi ? "1" : "0");
                    if (girdi) {
                        oyuncuSor();
                    }
                }
            });
        } catch (Throwable t) {
            olay("hata", "durum: " + t.getMessage());
        }
    }

    private void oyuncuSor() {
        try {
            PlayGames.getPlayersClient(etkinlik).getCurrentPlayer().addOnCompleteListener(new OnCompleteListener<Player>() {
                @Override
                public void onComplete(Task<Player> gorev) {
                    if (gorev.isSuccessful() && gorev.getResult() != null) {
                        Player p = gorev.getResult();
                        olay("oyuncu", p.getDisplayName() + "\t" + p.getPlayerId());
                    }
                }
            });
        } catch (Throwable t) {
            olay("hata", "oyuncu: " + t.getMessage());
        }
    }

    /** oyuncu Play Games'e girmemişse giriş penceresini açar */
    public static void girisYap() {
        final OtukenGiris o = ornek;
        if (o == null) {
            return;
        }
        o.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    o.istemci().signIn().addOnCompleteListener(new OnCompleteListener<AuthenticationResult>() {
                        @Override
                        public void onComplete(Task<AuthenticationResult> gorev) {
                            boolean girdi = gorev.isSuccessful() && gorev.getResult() != null && gorev.getResult().isAuthenticated();
                            o.olay("durum", girdi ? "1" : "0");
                            if (girdi) {
                                o.oyuncuSor();
                            }
                        }
                    });
                } catch (Throwable t) {
                    o.olay("hata", "giriş: " + t.getMessage());
                }
            }
        });
    }

    /** sunucumuz için tek kullanımlık yetki kodu (web istemci kimliğiyle) */
    public static void kodIste(final String webIstemci) {
        final OtukenGiris o = ornek;
        if (o == null) {
            return;
        }
        o.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    o.istemci().requestServerSideAccess(webIstemci, false).addOnCompleteListener(new OnCompleteListener<String>() {
                        @Override
                        public void onComplete(Task<String> gorev) {
                            if (gorev.isSuccessful() && gorev.getResult() != null && gorev.getResult().length() > 0) {
                                o.olay("kod", gorev.getResult());
                            } else {
                                Exception e = gorev.getException();
                                o.olay("kodHata", e != null ? e.getMessage() : "kod alınamadı");
                            }
                        }
                    });
                } catch (Throwable t) {
                    o.olay("kodHata", t.getMessage());
                }
            }
        });
    }
}
