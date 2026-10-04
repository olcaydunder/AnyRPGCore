# Çevrimiçi oyun (AnyMMO + FishNet) kaynakları

Depoya olduğu gibi eklendi (değiştirilmeden):

| Klasör | Kaynak | Sürüm |
|---|---|---|
| `Assets/FishNet` | https://github.com/FirstGearGames/FishNet (`Assets/FishNet`, `Demos` hariç) | 4.7.3, commit 7c4a6448d48ed65b154ebd5615c86fb9001da1cf |
| `Assets/AnyRPG/Addons/anymmo-fishnet` | https://github.com/AnyRPG/anymmo-fishnet (`Games`, belge ve derleme profilleri hariç) | commit 5dc8daa811acef2ec23bfa474f13cd4e4bbcbf1a |

Lisanslar: FishNet kendi lisansı (`Assets/FishNet/LICENSE.txt`: oyunlarda ücretsiz kullanılabilir),
AnyMMO MIT (`Assets/AnyRPG/Addons/anymmo-fishnet/LICENSE`).

Güncellemek için iki depoyu yeniden kopyalayıp bu tabloyu yenile. Oyuna özgü ağ ayarları derleme anında
`Assets/Otuken/Editor/AgHazirlik.cs` ile yapılır (sahnelere NetworkObject, birim profillerine FishNet prefabları).
