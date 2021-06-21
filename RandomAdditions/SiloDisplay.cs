using UnityEngine;

namespace RandomAdditions
{
    public class SiloDisplay : MonoBehaviour
    {
        internal ModuleItemSilo siloMain;
        private ChunkTypes displayChunk;
        private BlockTypes displayBlock;

        public void OnPool()
        {
            var meshFV = gameObject.GetComponent<MeshFilter>();
            if (!meshFV)
            {
                var meshFR = gameObject.AddComponent<MeshFilter>().mesh;
                var meshN = new Mesh();
                meshN.vertices = new Vector3[]
                {
                    new Vector3(-0.5f,-0.5f,0),
                    new Vector3(0.5f,-0.5f,0),
                    new Vector3(0.5f,0.5f,0),
                    new Vector3(-0.5f,0.5f,0)
                };
                meshN.uv = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 0)
                };
                meshFR = meshN;
                meshFR.RecalculateNormals();
                meshFR.RecalculateBounds();
                gameObject.GetComponent<MeshFilter>().mesh = meshFR;
                Debug.Log("RandomAdditions: Added new plane Mesh");
            }
            var meshV = gameObject.GetComponent<MeshRenderer>();
            if (!meshV)
            {
                gameObject.AddComponent<MeshRenderer>();
            }
            var meshR = gameObject.GetComponent<MeshRenderer>();
            if (siloMain.StoresBlocksInsteadOfChunks)
                meshR.material.SetTexture("_MainTex", Singleton.Manager<ManUI>.inst.GetSprite(new ItemTypeInfo(ObjectTypes.Block, (int)BlockTypes.GSOAIController_111)).texture);
            else
                meshR.material.SetTexture("_MainTex", Singleton.Manager<ManUI>.inst.GetSprite(new ItemTypeInfo(ObjectTypes.Chunk, (int)ChunkTypes.Null)).texture);
        }

        public void UpdateDisplay()
        {
            if (siloMain.StoresBlocksInsteadOfChunks)
            {
                if (displayBlock != siloMain.GetBlockType)
                {
                    displayBlock = siloMain.GetBlockType;
                    var meshR = gameObject.GetComponent<MeshRenderer>();
                    meshR.material.SetTexture("_MainTex", Singleton.Manager<ManUI>.inst.GetSprite(new ItemTypeInfo(ObjectTypes.Block, (int)displayBlock)).texture);
                }

            }
            else
            {
                if (displayChunk != siloMain.GetChunkType)
                {
                    displayChunk = siloMain.GetChunkType;
                    var meshR = gameObject.GetComponent<MeshRenderer>();
                    meshR.material.SetTexture("_MainTex", Singleton.Manager<ManUI>.inst.GetSprite(new ItemTypeInfo(ObjectTypes.Chunk, (int)displayChunk)).texture);
                }
            }
        }
    }
}
