package com.zootopiayazilim.otuken;

import android.app.Activity;

import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.ConsumeParams;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Ötüken: Google Play Faturalandırma Kitaplığı 8 köprüsü (Kut paketleri, tüketilebilir tek seferlik ürünler).
 * Unity tarafı AnyRPG.Odeme; olaylar OdemeDinleyici.olay(tür, veri) ile gider:
 *   hazir, urun ("kod\tfiyat"), satin ("json\u001Fimza"), bekliyor, iptal, hata, tuketildi
 * Satın almanın doğrulanması ve Kut'un yüklenmesi oyunda (sunucuda) yapılır; tüketme ancak yükleme olunca çağrılır.
 */
public class OtukenOdeme implements PurchasesUpdatedListener {

    private static OtukenOdeme ornek;

    private final Activity etkinlik;
    private final OdemeDinleyici dinleyici;
    private final List<String> kodlar = new ArrayList<>();
    private final Map<String, ProductDetails> urunler = new HashMap<>();
    private BillingClient istemci;
    private boolean bagli = false;

    private OtukenOdeme(Activity etkinlik, OdemeDinleyici dinleyici, String kodlarYazisi) {
        this.etkinlik = etkinlik;
        this.dinleyici = dinleyici;
        for (String k : kodlarYazisi.split(",")) {
            if (k.trim().length() > 0) {
                kodlar.add(k.trim());
            }
        }
    }

    public static void baslat(final Activity etkinlik, final OdemeDinleyici dinleyici, final String kodlar) {
        etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    if (ornek == null) {
                        ornek = new OtukenOdeme(etkinlik, dinleyici, kodlar);
                    }
                    ornek.baglan();
                } catch (Throwable t) {
                    if (ornek != null) {
                        ornek.olay("hata", "başlatma: " + t.getMessage());
                    }
                }
            }
        });
    }

    public static void satinAl(final String kod) {
        if (ornek == null) {
            return;
        }
        ornek.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    ornek.odemeAc(kod);
                } catch (Throwable t) {
                    ornek.olay("hata", "ödeme ekranı: " + t.getMessage());
                }
            }
        });
    }

    public static void tuket(final String token) {
        if (ornek == null || ornek.istemci == null) {
            return;
        }
        ConsumeParams p = ConsumeParams.newBuilder().setPurchaseToken(token).build();
        ornek.istemci.consumeAsync(p, (sonuc, t) -> {
            try {
                if (sonuc.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                    ornek.olay("tuketildi", t);
                } else {
                    ornek.olay("hata", "tüketilemedi: " + sonuc.getDebugMessage());
                }
            } catch (Throwable e) {
                ornek.olay("hata", "tüketme: " + e.getMessage());
            }
        });
    }

    /** Kut Dükkânı her açılışta: bağlı değilse yeniden bağlanır; fiyatlar gelmediyse (ürün sonradan etkinleştirildiyse) yeniden sorar */
    public static void bekleyenleriSor() {
        if (ornek == null) {
            return;
        }
        ornek.etkinlik.runOnUiThread(new Runnable() {
            @Override
            public void run() {
              try {
                if (ornek.bagli == false) {
                    ornek.baglan();
                    return;
                }
                if (ornek.urunler.isEmpty()) {
                    ornek.urunleriSor();
                }
                ornek.satinAlmalariSor();
              } catch (Throwable t) {
                ornek.olay("hata", "bekleyenler: " + t.getMessage());
              }
            }
        });
    }

    private void olay(String tur, String veri) {
        try {
            dinleyici.olay(tur, veri == null ? "" : veri);
        } catch (Throwable e) {
            // Unity tarafı kapandıysa
        }
    }

    private void baglan() {
        if (istemci == null) {
            istemci = BillingClient.newBuilder(etkinlik)
                .setListener(this)
                .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
                .enableAutoServiceReconnection()
                .build();
        }
        if (bagli) {
            satinAlmalariSor();
            return;
        }
        istemci.startConnection(new BillingClientStateListener() {
            @Override
            public void onBillingSetupFinished(BillingResult sonuc) {
                try {
                    if (sonuc.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                        bagli = true;
                        urunleriSor();
                        satinAlmalariSor();
                    } else {
                        olay("hata", "bağlantı " + sonuc.getResponseCode() + " " + sonuc.getDebugMessage());
                    }
                } catch (Throwable t) {
                    olay("hata", "bağlantı: " + t.getMessage());
                }
            }

            @Override
            public void onBillingServiceDisconnected() {
                bagli = false;
            }
        });
    }

    private void urunleriSor() {
        List<QueryProductDetailsParams.Product> liste = new ArrayList<>();
        for (String k : kodlar) {
            liste.add(QueryProductDetailsParams.Product.newBuilder().setProductId(k).setProductType(BillingClient.ProductType.INAPP).build());
        }
        QueryProductDetailsParams p = QueryProductDetailsParams.newBuilder().setProductList(liste).build();
        istemci.queryProductDetailsAsync(p, (sonuc, urunSonucu) -> {
          try {
            if (sonuc.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                StringBuilder eksik = new StringBuilder();
                for (ProductDetails d : urunSonucu.getProductDetailsList()) {
                    urunler.put(d.getProductId(), d);
                    ProductDetails.OneTimePurchaseOfferDetails teklif = d.getOneTimePurchaseOfferDetails();
                    if (teklif != null) {
                        olay("urun", d.getProductId() + "\t" + teklif.getFormattedPrice());
                    } else {
                        eksik.append(d.getProductId()).append(":teklif-yok ");
                    }
                }
                // teşhis: Play'in getiremediği ürünler ve nedenleri (Billing 8 getUnfetchedProductList; yansımayla, sürüm farkında derleme bozulmasın)
                try {
                    Object gelmeyenler = urunSonucu.getClass().getMethod("getUnfetchedProductList").invoke(urunSonucu);
                    if (gelmeyenler instanceof List) {
                        for (Object u : (List<?>) gelmeyenler) {
                            Object kimlik = u.getClass().getMethod("getProductId").invoke(u);
                            Object durum = u.getClass().getMethod("getStatusCode").invoke(u);
                            eksik.append(kimlik).append(":").append(durum).append(" ");
                        }
                    }
                } catch (Exception e) {
                    // eski Billing sürümü
                }
                if (eksik.length() > 0) {
                    olay("eksik", eksik.toString().trim());
                }
                olay("hazir", "");
            } else {
                olay("hata", "ürünler " + sonuc.getResponseCode() + " " + sonuc.getDebugMessage());
            }
          } catch (Throwable t) {
            olay("hata", "ürünler: " + t.getMessage());
          }
        });
    }

    private void satinAlmalariSor() {
        QueryPurchasesParams p = QueryPurchasesParams.newBuilder().setProductType(BillingClient.ProductType.INAPP).build();
        istemci.queryPurchasesAsync(p, (sonuc, satinAlmalar) -> {
            try {
                if (sonuc.getResponseCode() == BillingClient.BillingResponseCode.OK && satinAlmalar != null) {
                    for (Purchase s : satinAlmalar) {
                        isle(s);
                    }
                }
            } catch (Throwable t) {
                olay("hata", "satın almalar: " + t.getMessage());
            }
        });
    }

    private void odemeAc(String kod) {
        ProductDetails d = urunler.get(kod);
        if (istemci == null || bagli == false || d == null) {
            olay("hata", "ürün hazır değil");
            return;
        }
        BillingFlowParams.ProductDetailsParams.Builder urun = BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(d);
        ProductDetails.OneTimePurchaseOfferDetails teklif = d.getOneTimePurchaseOfferDetails();
        if (teklif != null && teklif.getOfferToken() != null) {
            urun.setOfferToken(teklif.getOfferToken());
        }
        List<BillingFlowParams.ProductDetailsParams> liste = new ArrayList<>();
        liste.add(urun.build());
        BillingFlowParams p = BillingFlowParams.newBuilder().setProductDetailsParamsList(liste).build();
        BillingResult sonuc = istemci.launchBillingFlow(etkinlik, p);
        if (sonuc.getResponseCode() != BillingClient.BillingResponseCode.OK) {
            olay("hata", "ödeme ekranı " + sonuc.getResponseCode() + " " + sonuc.getDebugMessage());
        }
    }

    @Override
    public void onPurchasesUpdated(BillingResult sonuc, List<Purchase> satinAlmalar) {
        try {
            satinAlmaGeldi(sonuc, satinAlmalar);
        } catch (Throwable t) {
            olay("hata", "satın alma: " + t.getMessage());
        }
    }

    private void satinAlmaGeldi(BillingResult sonuc, List<Purchase> satinAlmalar) {
        int kod = sonuc.getResponseCode();
        if (kod == BillingClient.BillingResponseCode.OK && satinAlmalar != null) {
            for (Purchase s : satinAlmalar) {
                isle(s);
            }
        } else if (kod == BillingClient.BillingResponseCode.USER_CANCELED) {
            olay("iptal", "");
        } else if (kod == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) {
            // önceki satın alma tüketilmemiş: yeniden teslim edilsin
            satinAlmalariSor();
        } else {
            olay("hata", kod + " " + sonuc.getDebugMessage());
        }
    }

    private void isle(Purchase s) {
        if (s.getPurchaseState() == Purchase.PurchaseState.PURCHASED) {
            olay("satin", s.getOriginalJson() + "\u001F" + s.getSignature());
        } else if (s.getPurchaseState() == Purchase.PurchaseState.PENDING) {
            olay("bekliyor", s.getProducts().isEmpty() ? "" : s.getProducts().get(0));
        }
    }
}
