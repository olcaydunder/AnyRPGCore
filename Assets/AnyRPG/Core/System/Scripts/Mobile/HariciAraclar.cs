using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// GitHub'dan eklenen paketlerin prefab'larına koddan erişmek için (Resources/HariciAraclar).
    ///   graphyPrefab : Graphy (Tayx94/graphy, MIT) - FPS ve bellek göstergesi
    ///   konsolPrefab : In-game Debug Console (yasirkula/UnityIngameDebugConsole, MIT) - telefonda hata konsolu
    /// Prefab'lar ancak seçeneklerden açılınca oluşturulur; kapalıyken oyuna hiçbir yük getirmez.
    /// </summary>
    public class HariciAraclar : ScriptableObject {

        public const string ResourcePath = "HariciAraclar";

        [SerializeField]
        private GameObject graphyPrefab = null;

        [SerializeField]
        private GameObject konsolPrefab = null;

        private static HariciAraclar instance = null;
        private static bool loaded = false;

        public static HariciAraclar Instance {
            get {
                if (loaded == false) {
                    loaded = true;
                    instance = Resources.Load<HariciAraclar>(ResourcePath);
                    if (instance == null) {
                        Debug.LogWarning("HariciAraclar: Resources/HariciAraclar bulunamadı");
                    }
                }
                return instance;
            }
        }

        public GameObject GraphyPrefab { get => graphyPrefab; }
        public GameObject KonsolPrefab { get => konsolPrefab; }
    }
}
