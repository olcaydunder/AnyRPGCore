using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ötüken: sunucuda (dosya arka ucu) her eşyanın Online/Items/{kimlik}.json kaydı olmalı; karakter kaydı yalnız eşya
    /// kimliğini tutar. AnyRPG'nin kendi yolları (ganimet, satıcı, üretim) kaydı yazar, ama Ötüken'in verdiği eşyalar
    /// (seviye ödülü, günlük armağan, Kut Dükkânı, yerdeki ganimet, depo sandığı) yazmıyordu: sunucu yeniden başlayınca
    /// (her sürüm güncellemesinde) bu eşyalar kayboluyordu. Karakter her kaydedildiğinde çantası, deposu, çantaları ve
    /// kuşandıkları denetlenir; kaydı olmayan eşya yazılır. Her kimlik bir kez denetlenir (dosya varlığı).
    /// </summary>
    public static class EsyaKaydi {

        private static readonly HashSet<long> denetlenen = new HashSet<long>();
        private static string klasor = null;

        /// <summary>bu oturumda kaydı yazılan (kurtarılan) eşya sayısı</summary>
        public static int Kurtarilan { get; private set; }

        public static void Denetle(SystemGameManager o, UnitController u) {
            if (o == null || u == null || u.CharacterInventoryManager == null) {
                return;
            }
            if (o.SystemConfigurationManager.ServerBackend != ServerBackend.File || o.NetworkManagerServer == null
                || o.NetworkManagerServer.ServerModeActive == false || o.NetworkManagerServer.ServerMode == NetworkServerMode.Lobby) {
                return;
            }
            if (klasor == null) {
                string oyunAdi = Regex.Replace(o.SystemConfigurationManager.GameName ?? string.Empty, "[^a-zA-Z0-9]", "");
                klasor = $"{Application.persistentDataPath}/{oyunAdi}/Online/Items";
            }
            CharacterInventoryManager env = u.CharacterInventoryManager;
            Yuvalar(o, env.InventorySlots);
            Yuvalar(o, env.BankSlots);
            Cantalar(o, env.BagNodes);
            Cantalar(o, env.BankNodes);
            if (u.CharacterEquipmentManager != null) {
                foreach (EquipmentInventorySlot y in u.CharacterEquipmentManager.CurrentEquipment.Values) {
                    if (y != null) {
                        Yokla(o, y.InstantiatedEquipment);
                    }
                }
            }
        }

        private static void Yuvalar(SystemGameManager o, List<InventorySlot> yuvalar) {
            if (yuvalar == null) {
                return;
            }
            foreach (InventorySlot y in yuvalar) {
                if (y == null || y.IsEmpty) {
                    continue;
                }
                foreach (InstantiatedItem e in y.InstantiatedItems.Values) {
                    Yokla(o, e);
                }
            }
        }

        private static void Cantalar(SystemGameManager o, List<BagNode> dugumler) {
            if (dugumler == null) {
                return;
            }
            foreach (BagNode d in dugumler) {
                if (d != null && d.InstantiatedBag != null) {
                    Yokla(o, d.InstantiatedBag);
                }
            }
        }

        private static void Yokla(SystemGameManager o, InstantiatedItem e) {
            if (e == null || denetlenen.Contains(e.InstanceId)) {
                return;
            }
            denetlenen.Add(e.InstanceId);
            if (File.Exists(klasor + "/" + e.InstanceId + ".json") == false) {
                o.ServerDataService.CreateItemInstance(e);
                Kurtarilan++;
                if (Kurtarilan <= 20 || Kurtarilan % 100 == 0) {
                    Debug.Log("[Sunucu] eşya kaydı yazıldı: " + e.DisplayName + " #" + e.InstanceId + " (" + Kurtarilan + ")");
                }
            }
        }
    }
}
