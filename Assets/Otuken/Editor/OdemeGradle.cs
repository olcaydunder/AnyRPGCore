using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Android derlemesinde Unity'nin ürettiği Gradle projesine Google Play Faturalandırma Kitaplığı'nı ekler
    /// (Assets/Plugins/Android/OtukenOdeme.java onu kullanır). Kitaplık derlemede Google'ın Maven deposundan iner.
    /// Küçültme (R8) açılırsa köprü sınıfları silinmesin diye koruma kuralı da yazılır.
    /// </summary>
    public class OdemeGradle : IPostGenerateGradleAndroidProject {

        public const string Kitaplik = "com.android.billingclient:billing:8.0.0";

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
            if (s.Contains("com.android.billingclient:billing") == false) {
                Match m = Regex.Match(s, @"dependencies\s*\{");
                if (m.Success == false) {
                    Debug.LogError("[OdemeGradle] build.gradle'da dependencies bölümü yok");
                    return;
                }
                s = s.Insert(m.Index + m.Length, "\n    implementation '" + Kitaplik + "'");
                File.WriteAllText(gradle, s);
            }
            string proguard = Path.Combine(path, "proguard-unity.txt");
            string kural = "-keep class com.zootopiayazilim.otuken.** { *; }";
            if (File.Exists(proguard) && File.ReadAllText(proguard).Contains(kural) == false) {
                File.AppendAllText(proguard, "\n" + kural + "\n");
            }
            Debug.Log("[OdemeGradle] Google Play Faturalandırma Kitaplığı eklendi: " + Kitaplik);
        }
    }
}
