using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ötüken: her karede tekrarlanan aynı hata günlüğü doldurmasın.
    /// Aynı hata (tür + ilk yığın satırı) dakikada en çok bir kez yazılır; arada bastırılanların sayısı bir sonrakine eklenir.
    /// Sunucuda bir yaratığın yapay zekâsı her fizik adımında hata atınca /var/log/syslog günde gigabaytlarca büyümüştü.
    /// </summary>
    public static class TekrarSuzgeci {

        private const float Aralik = 60f;
        private const int EnCokAnahtar = 256;

        private class Kayit {
            public float sonYazilma;
            public int bastirilan;
        }

        private static readonly Dictionary<string, Kayit> kayitlar = new Dictionary<string, Kayit>();

        /// <summary>hatayı yazar ya da (son dakikada aynısı yazıldıysa) yalnız sayar</summary>
        public static void Yaz(Exception hata, string yer, UnityEngine.Object baglam = null) {
            if (hata == null) {
                return;
            }
            string anahtar = Anahtar(hata, yer);
            float simdi = Time.realtimeSinceStartup;
            Kayit k;
            if (kayitlar.TryGetValue(anahtar, out k)) {
                if (simdi - k.sonYazilma < Aralik) {
                    k.bastirilan++;
                    return;
                }
            } else {
                if (kayitlar.Count >= EnCokAnahtar) {
                    kayitlar.Clear();
                }
                k = new Kayit();
                kayitlar[anahtar] = k;
            }
            int bastirilan = k.bastirilan;
            k.bastirilan = 0;
            k.sonYazilma = simdi;
            string ad = "";
            try {
                if (baglam != null) {
                    ad = baglam.name;
                }
            } catch (Exception) {
            }
            Debug.LogWarning($"[Hata] {yer} {ad}: {hata.GetType().Name}: {hata.Message}"
                + (bastirilan > 0 ? $" (son dakikada {bastirilan} kez daha, yazılmadı)" : "")
                + "\n" + IlkSatirlar(hata.StackTrace, 6));
        }

        private static string Anahtar(Exception hata, string yer) {
            string iz = hata.StackTrace ?? "";
            int son = iz.IndexOf('\n');
            if (son > 0) {
                // ilk iki satır: hatanın atıldığı yer ve onu çağıran
                int ikinci = iz.IndexOf('\n', son + 1);
                son = ikinci > 0 ? ikinci : son;
                iz = iz.Substring(0, son);
            }
            return yer + "|" + hata.GetType().Name + "|" + iz;
        }

        private static string IlkSatirlar(string metin, int adet) {
            if (string.IsNullOrEmpty(metin)) {
                return "";
            }
            int konum = 0;
            for (int i = 0; i < adet; i++) {
                int yeni = metin.IndexOf('\n', konum);
                if (yeni < 0) {
                    return metin;
                }
                konum = yeni + 1;
            }
            return metin.Substring(0, konum);
        }
    }
}
