using FishNet.Component.Animating;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace AnyRPG.EditorTools {
    public class FishNetNewCharacterWizard: NewCharacterWizardBase {

        protected const string pathToNetworkDefaultCharacterUnitPrefab = "/AnyRPG/Addons/anymmo-fishnet/Prefabs/Character/Unit/NetworkDefaultCharacterUnit.prefab";
        protected const string pathToNetworkDefaultMountUnitPrefab = "/AnyRPG/Addons/anymmo-fishnet/Prefabs/Character/Unit/NetworkDefaultMountUnit.prefab";

        [Header("Network Settings")]

        [SerializeField]
        public bool isMount = false;

        [MenuItem("Tools/AnyRPG/Wizard/New Character/New Online Character Wizard (FishNet)")]
        public static void CreateWizard() {
            ScriptableWizard.DisplayWizard<FishNetNewCharacterWizard>("New Online Character Wizard (FishNet)", "Create");
        }

        public override void ModifyModelPrefab(GameObject modelPrefab) {
            base.ModifyModelPrefab(modelPrefab);
        }

        public override void CreateExtraModelPrefabs(UnitProfile unitProfile, GameObject modelGameObject) {
            base.CreateExtraModelPrefabs(unitProfile, modelGameObject);

            string newModelFolder = gameParentFolder + fileSystemGameName + "/Prefab/Character";

            // copy existing model to new path
            string pathToNetworkModelCopy = $"Assets{newModelFolder}/Network{WizardUtilities.GetScriptableObjectFileSystemName(characterName)}.prefab";

            //GameObject modelGameObject = (GameObject)AssetDatabase.LoadMainAssetAtPath("Assets" + pathToModelCopy);

            // instantiate original
            GameObject instantiatedGO = (GameObject)PrefabUtility.InstantiatePrefab(modelGameObject);

            NetworkAnimator networkAnimator = instantiatedGO.AddComponent<NetworkAnimator>();
            if (networkAnimator != null) {
                SerializedObject serializedObject = new SerializedObject(networkAnimator);
                SerializedProperty clientAuthoritativeProperty = serializedObject.FindProperty("_clientAuthoritative");
                if (clientAuthoritativeProperty != null) {
                    clientAuthoritativeProperty.boolValue = true; // set to true to allow client to control animations
                    serializedObject.ApplyModifiedProperties();
                }
            }
            //NetworkObject networkObject = instantiatedGO.AddComponent<NetworkObject>();
            NetworkObject networkObject = instantiatedGO.GetComponent<NetworkObject>();
            if (networkObject != null) {
                SerializedObject serializedObject = new SerializedObject(networkObject);
                SerializedProperty initializeOrderProperty = serializedObject.FindProperty("_initializeOrder");
                if (initializeOrderProperty != null) {
                    initializeOrderProperty.intValue = (isMount ? -2 : 0);
                    serializedObject.ApplyModifiedProperties();
                }
            }
            instantiatedGO.AddComponent<FishNetCharacterModel>();

            // make variant on disk
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instantiatedGO, pathToNetworkModelCopy);

            // remove original from scene
            GameObject.DestroyImmediate(instantiatedGO);

            GameObject networkModelCopyObject = (GameObject)AssetDatabase.LoadMainAssetAtPath(pathToNetworkModelCopy);
            unitProfile.UnitPrefabProps.NetworkModelPrefab = networkModelCopyObject;
        }

        protected override void ModifyUnitProfile(UnitProfile unitProfileAsset) {
            base.ModifyUnitProfile(unitProfileAsset);

            if (isMount) {
                GameObject networkMountUnitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets" + pathToNetworkDefaultMountUnitPrefab);
                unitProfileAsset.UnitPrefabProps.NetworkUnitPrefab = networkMountUnitPrefab;
            } else {
                GameObject networkCharacterUnitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets" + pathToNetworkDefaultCharacterUnitPrefab);
                unitProfileAsset.UnitPrefabProps.NetworkUnitPrefab = networkCharacterUnitPrefab;
            }

        }

    }

}
