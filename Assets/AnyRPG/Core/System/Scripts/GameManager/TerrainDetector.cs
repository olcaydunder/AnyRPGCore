using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Finds which terrain texture (grass, sand, stone...) is under a position, for footstep sounds.
    /// Maps built from meshes have no Unity Terrain; then index 0 is returned, so the scene's first footstep sound plays.
    /// (Ötüken: before this, every footstep on such maps threw a NullReferenceException.)
    /// </summary>
    public class TerrainDetector {
        private Terrain terrain;
        private TerrainData terrainData;
        private Vector3 terrainPosition;
        private Vector3 terrainSize;
        private int alphamapWidth;
        private int alphamapHeight;
        private float[,,] splatmapData;
        private int numTextures;

        public void LoadSceneSettings() {
            //Debug.Log("TerrainDetector.LoadSceneSettings()");
            terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) {
                ClearSceneSettings();
                return;
            }
            //mainMapCameraController.targetTerrain = Terrain.activeTerrain;
            terrainData = terrain.terrainData;
            terrainPosition = terrain.transform.position;
            terrainSize = terrainData.size;
            alphamapWidth = terrainData.alphamapWidth;
            alphamapHeight = terrainData.alphamapHeight;

            splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            numTextures = alphamapWidth * alphamapHeight > 0 ? splatmapData.Length / (alphamapWidth * alphamapHeight) : 0;
            //Debug.Log($"TerrainDetector.LoadSceneSettings(); numTextures: {numTextures}");
        }

        public void ClearSceneSettings() {
            terrain = null;
            terrainData = null;
            alphamapWidth = 0;
            alphamapHeight = 0;

            splatmapData = new float[,,] { };
            numTextures = 0;
        }

        public int GetActiveTerrainTextureIdx(Vector3 position) {
            //Debug.Log($"TerrainDetector.GetActiveTerrainTextureIdx({position})");
            // no terrain (mesh map), or the terrain was unloaded with its scene
            if (terrain == null || terrainData == null || numTextures == 0 || terrainSize.x <= 0f || terrainSize.z <= 0f) {
                return 0;
            }
            int x = Mathf.Clamp((int)((position.x - terrainPosition.x) / terrainSize.x * alphamapWidth), 0, alphamapWidth - 1);
            int z = Mathf.Clamp((int)((position.z - terrainPosition.z) / terrainSize.z * alphamapHeight), 0, alphamapHeight - 1);
            int activeTerrainIndex = 0;
            float largestOpacity = 0f;

            for (int i = 0; i < numTextures; i++) {
                if (largestOpacity < splatmapData[z, x, i]) {
                    activeTerrainIndex = i;
                    largestOpacity = splatmapData[z, x, i];
                }
            }

            //Debug.Log($"TerrainDetector.GetActiveTerrainTextureIdx({position}); return {activeTerrainIndex}");
            return activeTerrainIndex;
        }

    }
}
