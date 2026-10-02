using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Bir modelin bütün görüntüleyicilerine tek bir malzeme verir. Aynı canavar modelinin renk çeşitlerini
    /// (kızıl, yeşil, mavi Körmös...) ayrı model dosyası olmadan, bir prefab üstünden yapmak için kullanılır.
    /// </summary>
    public class ModelMalzemesi : MonoBehaviour {

        [SerializeField]
        private Material malzeme = null;

        private void Awake() {
            if (malzeme == null) {
                return;
            }
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer modelRenderer in renderers) {
                Material[] materials = modelRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) {
                    materials[i] = malzeme;
                }
                modelRenderer.sharedMaterials = materials;
            }
        }
    }
}
