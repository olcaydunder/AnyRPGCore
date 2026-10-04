using UnityEngine;
using UnityEditor;

namespace AnyRPG {
    public class FishNetNewSceneWizard : NewSceneWizardBase, ICreateSceneRequestor {

        private const string pathToPhysicsSceneSync = "/AnyRPG/Addons/anymmo-fishnet/GameManager/FishNetPhysicsSceneSync.prefab";
        private const string portalTemplatePath = "/AnyRPG/Addons/anymmo-fishnet/Templates/Prefabs/Portal/FishNetStonePortal.prefab";

        public static string PortalTemplatePath => portalTemplatePath;

        [MenuItem("Tools/AnyRPG/Wizard/New Scene/New Online Scene Wizard (FishNet)")]
        public static void CreateWizard() {
            ScriptableWizard.DisplayWizard<FishNetNewSceneWizard>("New Online Scene Wizard (FishNet)", "Create");
        }

        public override void ModifyScene() {
            base.ModifyScene();
            string sceneConfigPrefabAssetPath = "Assets" + pathToPhysicsSceneSync;
            GameObject sceneSyncGameObject = (GameObject)AssetDatabase.LoadMainAssetAtPath(sceneConfigPrefabAssetPath);
            GameObject instantiatedGO = (GameObject)PrefabUtility.InstantiatePrefab(sceneSyncGameObject);
            instantiatedGO.transform.SetAsFirstSibling();
        }

        public static bool CheckRequiredTemplatesExistStatic() {

            if (NewSceneWizardBase.CheckRequiredBaseTemplatesExist() == false) {
                return false;
            }

            // Check for presence of portal template
            if (WizardUtilities.CheckFileExists(portalTemplatePath, "portal template") == false) {
                return false;
            }

            return true;
        }

        public override string GetPortalTemplatePath() {
            return portalTemplatePath;
        }

    }


}
