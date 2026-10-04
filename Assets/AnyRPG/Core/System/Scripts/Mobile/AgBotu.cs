using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Çevrimiçi oyunun otomatik denemesi için istemci botu. Linux derlemesi "-istemciBotu Ad" ile açılınca:
    /// sunucuya (-adres, varsayılan 127.0.0.1:7770) Ad adlı hesapla girer (yoksa hesap açılır), karakteri yoksa
    /// oluşturur, dünyaya girer, otomatik avı açar ve -sure saniye boyunca (varsayılan 60) beş saniyede bir
    /// gördüklerini yazar: öteki oyuncular, sunucunun doğurduğu NPC/düşmanlar, seviye ve tecrübe.
    /// Sonunda -rapor dosyasına (varsayılan ag_botu_Ad.txt) özet yazar ve oyunu kapatır. Workflow sunucuyu ve iki
    /// botu aynı makinede çalıştırır: iki bot birbirini görürse çevrimiçi oyun çalışıyor demektir.
    /// </summary>
    public class AgBotu : MonoBehaviour {

        private static string Arguman(string ad) {
            string[] a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, ad);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static bool Etkin {
            get { return Arguman("-istemciBotu") != null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Kur() {
            if (Etkin == false || Application.isEditor || Sunucu.Etkin) {
                return;
            }
            Application.targetFrameRate = 30;
            AudioListener.volume = 0f;
            GameObject go = new GameObject("AgBotu");
            DontDestroyOnLoad(go);
            go.AddComponent<AgBotu>();
        }

        private readonly StringBuilder rapor = new StringBuilder();
        private string ad = "Bot";
        private string raporYolu = null;
        private float baslangic = 0f;

        private void Not(string satir) {
            string s = "[" + (Time.realtimeSinceStartup - baslangic).ToString("0") + " sn] " + satir;
            rapor.AppendLine(s);
            Debug.Log("[AgBotu] " + s);
            try {
                File.WriteAllText(raporYolu, rapor.ToString());
            } catch (Exception) {
            }
        }

        private IEnumerator Start() {
            baslangic = Time.realtimeSinceStartup;
            ad = Arguman("-istemciBotu");
            string adres = Arguman("-adres") ?? "127.0.0.1:" + Sunucu.VarsayilanPort;
            raporYolu = Arguman("-rapor") ?? Path.Combine(Directory.GetCurrentDirectory(), "ag_botu_" + ad + ".txt");
            float sure = 60f;
            float.TryParse(Arguman("-sure") ?? "60", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sure);
            Not("bot " + ad + " başladı, sunucu " + adres + ", istemci sürümü " + Application.version);

            SystemGameManager oyun = null;
            while (true) {
                oyun = FindAnyObjectByType<SystemGameManager>();
                if (oyun != null && oyun.LevelManagerClient != null && oyun.LevelManagerClient.IsMainMenu() && oyun.NetworkManagerClient != null) {
                    break;
                }
                if (Time.realtimeSinceStartup - baslangic > 180f) {
                    Bitir("SONUÇ: ana menü açılmadı");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(3f);

            // giriş (hesap yoksa sunucu açar)
            bool istendi = false;
            try {
                istendi = oyun.NetworkManagerClient.Login(ad, "deneme-" + ad, adres);
            } catch (Exception e) {
                Not("giriş hatası: " + e.Message);
            }
            Not("giriş isteği: " + istendi);
            float t = Time.realtimeSinceStartup;
            while (oyun.NetworkManagerClient.AccountId <= 0 && Time.realtimeSinceStartup - t < 60f) {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (oyun.NetworkManagerClient.AccountId <= 0) {
                Bitir("SONUÇ: giriş olmadı (60 sn)");
                yield break;
            }
            Not("giriş tamam: hesap " + oyun.NetworkManagerClient.AccountId + ", kip " + oyun.NetworkManagerClient.ClientMode);
            yield return new WaitForSecondsRealtime(2f);

            // karakter listesi; yoksa oluştur
            List<SinglePlayerSaveData> liste = null;
            for (int deneme = 0; deneme < 2; deneme++) {
                bool geldi = false;
                Action dinle = () => geldi = true;
                oyun.LoadGameManager.OnLoadCharacterList += dinle;
                oyun.LoadGameManager.LoadCharacterList();
                t = Time.realtimeSinceStartup;
                while (geldi == false && Time.realtimeSinceStartup - t < 30f) {
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                oyun.LoadGameManager.OnLoadCharacterList -= dinle;
                liste = oyun.LoadGameManager.CharacterList;
                Not("karakter listesi: " + (geldi ? liste.Count + " karakter" : "gelmedi"));
                if (liste != null && liste.Count > 0) {
                    break;
                }
                if (deneme == 0) {
                    try {
                        oyun.NewGameManager.InitializeData();
                        oyun.NewGameManager.SetupSaveData();
                        oyun.NewGameManager.EditPlayerName(ad);
                        oyun.NewGameManager.CreateNetworkCharacter();
                        Not("karakter oluşturma istendi");
                    } catch (Exception e) {
                        Not("karakter oluşturulamadı: " + e.Message);
                    }
                    yield return new WaitForSecondsRealtime(5f);
                }
            }
            if (liste == null || liste.Count == 0) {
                Bitir("SONUÇ: karakter yok");
                yield break;
            }

            // dünyaya gir
            oyun.LoadGameManager.LoadGame(liste[0]);
            Not("karaktere giriliyor: " + liste[0].CharacterSaveData.CharacterName + " (" + liste[0].CharacterSaveData.CurrentScene + ")");
            t = Time.realtimeSinceStartup;
            while ((oyun.PlayerManagerClient.PlayerUnitSpawned == false || oyun.PlayerManagerClient.UnitController == null) && Time.realtimeSinceStartup - t < 120f) {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            UnitController ben = oyun.PlayerManagerClient.UnitController;
            if (ben == null) {
                Bitir("SONUÇ: dünyaya girilemedi (120 sn)");
                yield break;
            }
            Not("dünyada: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", konum " + ben.transform.position.ToString("0"));
            yield return new WaitForSecondsRealtime(5f);
            if (OtomatikAv.Acik == false) {
                OtomatikAv.Degistir();
            }

            int enCokOyuncu = 0;
            int enCokNpc = 0;
            int ilkTecrube = ben.CharacterStats != null ? ben.CharacterStats.CurrentXP : 0;
            int ilkSeviye = ben.CharacterStats != null ? ben.CharacterStats.Level : 0;
            Vector3 ilkKonum = ben.transform.position;
            HashSet<string> gorulenOyuncular = new HashSet<string>();
            t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < sure) {
                yield return new WaitForSecondsRealtime(5f);
                ben = oyun.PlayerManagerClient.UnitController;
                if (ben == null) {
                    Not("oyuncu birimi kayboldu");
                    break;
                }
                int oyuncu = 0;
                int npc = 0;
                foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                    if (u == ben) {
                        continue;
                    }
                    if (u.UnitControllerMode == UnitControllerMode.Player) {
                        oyuncu++;
                        gorulenOyuncular.Add(u.DisplayName);
                    } else if (u.UnitControllerMode == UnitControllerMode.AI) {
                        npc++;
                    }
                }
                enCokOyuncu = Mathf.Max(enCokOyuncu, oyuncu);
                enCokNpc = Mathf.Max(enCokNpc, npc);
                Not("öteki oyuncu " + oyuncu + ", NPC/düşman " + npc + ", seviye " + ben.CharacterStats.Level + ", tecrübe " + ben.CharacterStats.CurrentXP
                    + ", konum " + ben.transform.position.ToString("0") + (ben.Target != null ? ", hedef " + ben.Target.DisplayName : string.Empty));
            }
            Vector3 son = ben != null ? ben.transform.position : ilkKonum;
            Bitir("SONUÇ: " + (enCokOyuncu > 0 ? "öteki oyuncu GÖRÜLDÜ (" + string.Join(", ", gorulenOyuncular) + ")" : "öteki oyuncu görülmedi")
                + ", en çok NPC/düşman " + enCokNpc + ", yürüdüğü " + Vector3.Distance(ilkKonum, son).ToString("0") + " m"
                + ", seviye " + ilkSeviye + " → " + (ben != null ? ben.CharacterStats.Level : 0)
                + ", tecrübe " + ilkTecrube + " → " + (ben != null ? ben.CharacterStats.CurrentXP : 0));
        }

        private void Bitir(string sonuc) {
            Not(sonuc);
            StartCoroutine(Kapat());
        }

        private IEnumerator Kapat() {
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
