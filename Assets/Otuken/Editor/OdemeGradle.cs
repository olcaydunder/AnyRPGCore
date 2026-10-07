using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Android derlemesinde Unity'nin ürettiği Gradle projesine:
    ///  - Google Play Faturalandırma Kitaplığı (Assets/Plugins/Android/OtukenOdeme.java),
    ///  - Google Mobile Ads (AdMob) ve kullanıcı onayı (UMP) kitaplıkları (OtukenReklam.java),
    ///  - AdMob uygulama kimliği (AndroidManifest meta-data; yoksa reklam kitaplığı açılışta uygulamayı çökertir)
    ///  - Play Oyun Hizmetleri v2 (OtukenGiris.java) ve proje kimliği (Google.txt; dize kaynağı + manifest)
    /// eklenir. Kitaplıklar derlemede Google'ın Maven deposundan iner. Küçültme (R8) açılırsa köprü sınıfları silinmesin
    /// diye koruma kuralı da yazılır. Reklam kimlikleri Assets/Otuken/Resources/Reklam.txt'tedir.
    /// </summary>
    public class OdemeGradle : IPostGenerateGradleAndroidProject {

        public const string Kitaplik = "com.android.billingclient:billing:8.0.0";
        public const string ReklamKitapligi = "com.google.android.gms:play-services-ads:24.6.0";
        public const string IzinKitapligi = "com.google.android.ump:user-messaging-platform:3.2.0";
        // Play Oyun Hizmetleri v2 (OtukenGiris.java): köprü derlensin diye her zaman eklenir; proje kimliği Google.txt'te
        public const string OyunHizmetleriKitapligi = "com.google.android.gms:play-services-games-v2:22.1.0";
        private const string OyunKimligiAdi = "com.google.android.gms.games.APP_ID";
        private const string UygulamaKimligiAdi = "com.google.android.gms.ads.APPLICATION_ID";

        public int callbackOrder {
            get { return 100; }
        }

        public void OnPostGenerateGradleAndroidProject(string path) {
            string gradle = Path.Combine(path, "build.gradle");
            if (File.Exists(gradle) == false) {
                Debug.LogError("[OdemeGradle] build.gradle bulunamadı: " + gradle);
                return;
            }
            string s = File.ReadAllText(gradle);
            Match m = Regex.Match(s, @"dependencies\s*\{");
            if (m.Success == false) {
                Debug.LogError("[OdemeGradle] build.gradle'da dependencies bölümü yok");
                return;
            }
            string eklenecek = string.Empty;
            if (s.Contains("com.android.billingclient:billing") == false) {
                eklenecek += "\n    implementation '" + Kitaplik + "'";
            }
            string uygulamaKimligi = UygulamaKimligi();
            if (uygulamaKimligi != null) {
                if (s.Contains("com.google.android.gms:play-services-ads") == false) {
                    eklenecek += "\n    implementation '" + ReklamKitapligi + "'";
                }
                if (s.Contains("com.google.android.ump:user-messaging-platform") == false) {
                    eklenecek += "\n    implementation '" + IzinKitapligi + "'";
                }
            }
            if (s.Contains("com.google.android.gms:play-services-games-v2") == false) {
                eklenecek += "\n    implementation '" + OyunHizmetleriKitapligi + "'";
            }
            if (eklenecek.Length > 0) {
                s = s.Insert(m.Index + m.Length, eklenecek);
                File.WriteAllText(gradle, s);
            }
            string oyunProjesi = OyunProjesi();
            if (oyunProjesi != null) {
                OyunHizmetleriniYaz(path, oyunProjesi);
            }

            if (uygulamaKimligi != null) {
                ManifesteYaz(path, uygulamaKimligi);
                AndroidXAc(path);
            } else {
                Debug.LogWarning("[OdemeGradle] Reklam.txt'te uygulama kimliği yok: reklam kitaplığı eklenmedi");
            }

            string proguard = Path.Combine(path, "proguard-unity.txt");
            string kural = "-keep class com.zootopiayazilim.otuken.** { *; }";
            if (File.Exists(proguard) && File.ReadAllText(proguard).Contains(kural) == false) {
                File.AppendAllText(proguard, "\n" + kural + "\n");
            }
            Debug.Log("[OdemeGradle] eklendi: " + Kitaplik + (uygulamaKimligi != null ? ", " + ReklamKitapligi + ", " + IzinKitapligi + ", AdMob " + uygulamaKimligi : string.Empty));
        }

        /// <summary>Reklam.txt'teki "uygulama=" (yoksa null)</summary>
        public static string UygulamaKimligi() {
            string yol = Path.Combine(Application.dataPath, "Otuken/Resources/Reklam.txt");
            if (File.Exists(yol) == false) {
                return null;
            }
            foreach (string satir in File.ReadAllLines(yol)) {
                string t = satir.Trim();
                if (t.StartsWith("uygulama=")) {
                    string k = t.Substring("uygulama=".Length).Trim();
                    return Regex.IsMatch(k, @"^ca-app-pub-\d+~\d+$") ? k : null;
                }
            }
            return null;
        }

        /// <summary>Google.txt'teki "proje=" (Play Oyun Hizmetleri proje kimliği; yoksa null)</summary>
        public static string OyunProjesi() {
            string yol = Path.Combine(Application.dataPath, "Otuken/Resources/Google.txt");
            if (File.Exists(yol) == false) {
                return null;
            }
            foreach (string satir in File.ReadAllLines(yol)) {
                string t = satir.Trim();
                if (t.StartsWith("proje=")) {
                    string k = t.Substring("proje=".Length).Trim();
                    return Regex.IsMatch(k, @"^\d{6,20}$") ? k : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Play Oyun Hizmetleri: proje kimliği bir dize kaynağına (sayı olarak okunmasın diye) ve manifeste
        /// (com.google.android.gms.games.APP_ID = @string/game_services_project_id) yazılır
        /// </summary>
        private static void OyunHizmetleriniYaz(string path, string proje) {
            string klasor = Path.Combine(path, "src", "main", "res", "values");
            Directory.CreateDirectory(klasor);
            File.WriteAllText(Path.Combine(klasor, "otuken_oyun_hizmetleri.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n    <string name=\"game_services_project_id\" translatable=\"false\">"
                + proje + "</string>\n</resources>\n");
            MetaVeriEkle(path, "<meta-data android:name=\"" + OyunKimligiAdi + "\" android:value=\"@string/game_services_project_id\" />", OyunKimligiAdi);
            Debug.Log("[OdemeGradle] Play Oyun Hizmetleri proje kimliği yazıldı: " + proje);
        }

        private static void ManifesteYaz(string path, string kimlik) {
            MetaVeriEkle(path, "<meta-data android:name=\"" + UygulamaKimligiAdi + "\" android:value=\"" + kimlik + "\" />", UygulamaKimligiAdi);
            Debug.Log("[OdemeGradle] AdMob uygulama kimliği manifeste yazıldı");
        }

        /// <summary>unityLibrary manifestinin application bölümüne meta-data ekler (varsa dokunmaz)</summary>
        private static void MetaVeriEkle(string path, string meta, string ad) {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifest) == false) {
                throw new BuildFailedException("[OdemeGradle] AndroidManifest.xml bulunamadı: " + manifest);
            }
            string s = File.ReadAllText(manifest);
            if (s.Contains(ad)) {
                return;
            }
            Match bas = Regex.Match(s, @"<application\b[^>]*?(/?)>", RegexOptions.Singleline);
            if (bas.Success) {
                if (bas.Groups[1].Value == "/") {
                    // <application ... /> : içi boş, açılıp kapatılır
                    string acik = bas.Value.Substring(0, bas.Value.Length - 2).TrimEnd() + ">";
                    s = s.Substring(0, bas.Index) + acik + "\n    " + meta + "\n  </application>" + s.Substring(bas.Index + bas.Length);
                } else {
                    s = s.Insert(bas.Index + bas.Length, "\n    " + meta);
                }
            } else {
                int son = s.LastIndexOf("</manifest>");
                if (son < 0) {
                    throw new BuildFailedException("[OdemeGradle] AndroidManifest.xml çözülemedi");
                }
                s = s.Insert(son, "  <application>\n    " + meta + "\n  </application>\n");
            }
            File.WriteAllText(manifest, s);
        }

        /// <summary>reklam kitaplığı AndroidX ister; Unity çoğunlukla açar, açık değilse gradle.properties'e yazılır</summary>
        private static void AndroidXAc(string path) {
            string ozellik = Path.Combine(Directory.GetParent(path).FullName, "gradle.properties");
            if (File.Exists(ozellik) == false) {
                return;
            }
            string s = File.ReadAllText(ozellik);
            string ek = string.Empty;
            if (Regex.IsMatch(s, @"^\s*android\.useAndroidX\s*=\s*true", RegexOptions.Multiline) == false) {
                ek += "\nandroid.useAndroidX=true";
            }
            if (Regex.IsMatch(s, @"^\s*android\.enableJetifier\s*=", RegexOptions.Multiline) == false) {
                ek += "\nandroid.enableJetifier=true";
            }
            if (ek.Length > 0) {
                File.AppendAllText(ozellik, ek + "\n");
            }
        }
    }
}
