using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Uygulama simgesi. Play Store'a yüklenen 512x512 simgeyle telefondaki simge aynı çizimdir: ikisini de
    /// Tools~/logo/logo.py üretir (Assets/Otuken/Simge). Derlemeden önce Derleme.Derle çağırır:
    ///  - varsayılan ve Android "Legacy" simgesi: simge.png (kare)
    ///  - Android "Round" (yuvarlak simge isteyen telefonlar): simge_yuvarlak.png
    ///  - Android "Adaptive" (8.0+): arka plan uyarlanabilir_arka.png, ön katman uyarlanabilir_on.png (çizim, telefonun
    ///    maskesinde kesilmesin diye ortadaki güvenli alanda; görünen kısmı Play simgesiyle aynı oranda)
    /// Android modülünün türlerine (AndroidPlatformIconKind) bağlanmamak için simge türleri adlarından bulunur; böylece
    /// Linux sunucu derlemesinde de derlenir.
    /// </summary>
    public static class Simgeler {

        private const string Klasor = "Assets/Otuken/Simge/";

        public static void Kur() {
            Texture2D kare = Yukle("simge.png");
            Texture2D yuvarlak = Yukle("simge_yuvarlak.png");
            Texture2D arka = Yukle("uyarlanabilir_arka.png");
            Texture2D on = Yukle("uyarlanabilir_on.png");
            if (kare == null) {
                Debug.LogError("[Simgeler] " + Klasor + "simge.png bulunamadı");
                return;
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new Texture2D[] { kare }, IconKind.Any);

            string rapor = "varsayılan";
            foreach (PlatformIconKind tur in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android)) {
                string ad = tur.ToString();
                PlatformIcon[] simgeler = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, tur);
                if (simgeler == null || simgeler.Length == 0) {
                    continue;
                }
                foreach (PlatformIcon simge in simgeler) {
                    if (ad.IndexOf("Adaptive", StringComparison.OrdinalIgnoreCase) >= 0) {
                        if (arka != null && on != null) {
                            simge.SetTextures(arka, on);
                        }
                    } else if (ad.IndexOf("Round", StringComparison.OrdinalIgnoreCase) >= 0) {
                        simge.SetTextures(yuvarlak != null ? yuvarlak : kare);
                    } else {
                        simge.SetTextures(kare);
                    }
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, tur, simgeler);
                rapor += ", " + ad + " (" + simgeler.Length + ")";
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Simgeler] uygulama simgesi kuruldu: " + rapor);
        }

        private static Texture2D Yukle(string ad) {
            string yol = Klasor + ad;
            TextureImporter ice = AssetImporter.GetAtPath(yol) as TextureImporter;
            if (ice != null && (ice.textureCompression != TextureImporterCompression.Uncompressed || ice.mipmapEnabled || ice.npotScale != TextureImporterNPOTScale.None)) {
                // simge keskin kalsın: sıkıştırmasız, mip haritasız
                ice.textureCompression = TextureImporterCompression.Uncompressed;
                ice.mipmapEnabled = false;
                ice.npotScale = TextureImporterNPOTScale.None;
                ice.alphaIsTransparency = true;
                ice.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
        }
    }
}
