using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions.RailSystem
{
    internal static class RailMeshBuilder
    {
        public const int segmentPoolInitSize = 8;
        public const int crossPoolInitSize = 32;

        public const int segmentSafePoolSize = 64;

        internal static Transform railIronPrefab;


        private static void InsureRailIronPrefab()
        {
            if (railIronPrefab == null)
            {
                if (MPB == null)
                {
                    DebugRandAddi.Log("MPB fetched");
                    matProp = (int)typeof(MaterialSwapper).GetField("s_matPropCoreFourId", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    MPBReset = typeof(MaterialSwapper).GetMethod("InitStatic", BindingFlags.NonPublic | BindingFlags.Static);
                    MPB = (MaterialPropertyBlock)typeof(MaterialSwapper).GetField("s_matPropBlock", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                }
                DebugRandAddi.Log("InsureRailIronPrefab init");
                GameObject Rail = UnityEngine.Object.Instantiate(new GameObject("RailIronPrefab"), null);
                Transform railTrans = Rail.transform;
                railTrans.localPosition = Vector3.zero;
                railTrans.localRotation = Quaternion.identity;
                railTrans.localScale = Vector3.one;

                var MF = Rail.AddComponent<MeshFilter>();
                var MR = Rail.AddComponent<MeshRenderer>();
                var res = (Material[])Resources.FindObjectsOfTypeAll(typeof(Material));
                MR.sharedMaterial = res.FirstOrDefault(delegate (Material cand)
                {
                    return cand.name.Equals("GSO_Main");
                });
                railTrans.CreatePool(64);
                railIronPrefab = railTrans;
                DebugRandAddi.Log("InsureRailIronPrefab end");
            }
        }


        private static MaterialPropertyBlock MPB;
        private static int matProp;
        private static MethodInfo MPBReset;
        internal static void ChangeTrackIronGameObject(Transform trans, ref Transform transIron, RailType Type,
            string name, Vector3[] localPoints, bool RightSide)
        {
            if (transIron == null)
                transIron = CreateTrackIronGameObject(trans, name);
            FormTrackIronMesh(Type, transIron.GetComponent<MeshFilter>(), localPoints, RightSide);
        }
        internal static void SetTrackSkin(Transform railTransBase, RailType Type, byte skinID)
        {
            MPBReset.Invoke(null, new object[0]);
            RailTypeStats stats = ManRails.railTypeStats[Type];
            int skinIndex = ManCustomSkins.inst.SkinIDToIndex(skinID, stats.railCorp);
            MPB.SetVector(matProp, new Vector4(
                ManTechMaterialSwap.inst.GetDamageColourFloat(ManTechMaterialSwap.MaterialColour.Normal),
                ManTechMaterialSwap.inst.GetMinEmissiveForCorporation(stats.railCorp),
                skinIndex / 8f,
                skinIndex % 8f));
            foreach (var item in railTransBase.GetComponentsInChildren<Renderer>(true))
            {
                if (item.name != foundationName)
                    item.SetPropertyBlock(MPB);
            }
        }


        internal static Transform CreateTrackIronGameObject(Transform transform, string name)
        {
            InsureRailIronPrefab();
            Transform railTrans = railIronPrefab.Spawn(transform);
            railTrans.localPosition = Vector3.zero;
            railTrans.localRotation = Quaternion.identity;
            railTrans.localScale = Vector3.one;

            GameObject Rail = railTrans.gameObject;
            Rail.name = name;
            return railTrans;
        }


        private const int prefabFrameVerts = 6;

        private static Vector3[] prefabFrameStart = new Vector3[prefabFrameVerts];
        private static Vector3[] prefabFrameEnd = new Vector3[prefabFrameVerts];
        // 12 frameSection vertices
        private static Vector3[] prefabFrameSection = new Vector3[prefabFrameVerts * 2];
        // Set up the end triangles
        private static readonly int[] prefabFrameEndIndexes = new int[]
                {   // (Fan Method)
                    0,1,2,
                    0,2,3,
                    0,3,4,
                    0,4,5,
                };
        private const int prefabFrameEndIndexCount = 6;
        // 12 frameSection vertices
        /// <summary> Generates 12 faces connecting 6 points to the next 6 points </summary>
        private static readonly int[] prefabFrameSectionIndexes = new int[]
                {
                    12,1,0,  1,12,13,
                    14,3,2,  3,14,15,
                    16,5,4,  5,16,17,
                    18,7,6,  7,18,19,
                    20,9,8,  9,20,21,
                    22,11,10,  11,22,23,
                };
        private const int frameSectionIndexCount = 12;

        private static readonly Vector3[] frameEndNormals = new Vector3[prefabFrameVerts] {
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                    new Vector3(0, 0, 1),
                };  // FORWARDS FACING
                    // 12 frameSection vertices
        private static readonly Vector3[] frameSectionNormals = new Vector3[prefabFrameVerts * 2] {
                    new Vector3(0, -1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 1, 0).normalized,
                    new Vector3(1, 1, 0).normalized,
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(-1, 1, 0).normalized,
                    new Vector3(-1, 1, 0).normalized,
                    new Vector3(-1, 0, 0),
                    new Vector3(-1, 0, 0),
                };
        private static string foundationName = "Foundation";

        internal static void FormTrackIronMesh(RailType Type, MeshFilter MF, Vector3[] localPoints, bool invertAngling)
        {
            // VERTICES
            DebugRandAddi.Info("Creating Track Iron...");
            //DebugRandAddi.Log("Making Vertices...");
            float scale = ManRails.railTypeStats[Type].railIronScale;
            float bevel = 0.075f * scale;
            float widthHalf = 0.2f * scale;
            float heightL = 0.02f * scale;
            float heightR = 0.02f * scale;
            float bottom = -0.3f * scale;
            if (invertAngling)
                heightR = -0.04f * scale;
            else
                heightL = -0.04f * scale;

            float LowSide = widthHalf - bevel;
            float TLSide = heightL - bevel;
            float TRSide = heightR - bevel;

            prefabFrameStart[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameStart[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameStart[2] = new Vector3(widthHalf, TRSide, 0);
            prefabFrameStart[3] = new Vector3(LowSide, heightR, 0);
            prefabFrameStart[4] = new Vector3(-LowSide, heightL, 0);
            prefabFrameStart[5] = new Vector3(-widthHalf, TLSide, 0);

            
            prefabFrameEnd[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameEnd[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameEnd[2] = new Vector3(widthHalf, TLSide, 0);
            prefabFrameEnd[3] = new Vector3(LowSide, heightL, 0);
            prefabFrameEnd[4] = new Vector3(-LowSide, heightR, 0);
            prefabFrameEnd[5] = new Vector3(-widthHalf, TRSide, 0);

            // 12 frameSection vertices
            prefabFrameSection[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameSection[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameSection[2] = new Vector3(widthHalf, bottom, 0);
            prefabFrameSection[3] = new Vector3(widthHalf, TRSide, 0);
            prefabFrameSection[4] = new Vector3(widthHalf, TRSide, 0);
            prefabFrameSection[5] = new Vector3(LowSide, heightR, 0);
            prefabFrameSection[6] = new Vector3(LowSide, heightR, 0);
            prefabFrameSection[7] = new Vector3(-LowSide, heightL, 0);
            prefabFrameSection[8] = new Vector3(-LowSide, heightL, 0);
            prefabFrameSection[9] = new Vector3(-widthHalf, TLSide, 0);
            prefabFrameSection[10] = new Vector3(-widthHalf, TLSide, 0);
            prefabFrameSection[11] = new Vector3(-widthHalf, bottom, 0);

            Mesh iron = MF.mesh;
            if (iron == null)
               iron = new Mesh();
            FormElongatedPrismFromSpecs(iron, Type, MF, localPoints);
        }


        internal static void ChangeTrackFoundationGameObject(Transform trans, ref Transform transFoundation, 
            RailType Type, Vector3[] localPoints)
        {
            if (transFoundation == null)
                CreateTrackFoundationGameObject(trans, ref transFoundation, Type);
            SetTrackFoundationMesh(ref transFoundation, Type, localPoints);
        }
        private static void CreateTrackFoundationGameObject(Transform trans, ref Transform railTrans,
            RailType Type)
        {
            InsureRailIronPrefab();

            railTrans = railIronPrefab.Spawn(trans);
            railTrans.localPosition = Vector3.zero;
            railTrans.localRotation = Quaternion.identity;
            railTrans.localScale = Vector3.one;

            GameObject Rail = railTrans.gameObject;
            Rail.name = foundationName;

            SetTrackFoundationTexture(ref railTrans, Type);
        }
        private static void SetTrackFoundationMesh(ref Transform railTrans,
            RailType Type, Vector3[] localPoints)
        {
            FormTrackFoundationMesh(Type, railTrans.GetComponent<MeshFilter>(), localPoints);
        }
        private static void SetTrackFoundationTexture(ref Transform railTrans, RailType Type)
        {
            Material mat = ManTechMaterialSwap.inst.m_FinalCorpMaterials[(int)FactionSubTypes.GSO];
            railTrans.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        private static void FormTrackFoundationMesh(RailType Type, MeshFilter MF, Vector3[] localPoints)
        {
            // VERTICES
            DebugRandAddi.Info("Creating Track Foundation...");
            //DebugRandAddi.Log("Making Vertices...");
            float scale = ManRails.railTypeStats[Type].RailGauge;
            float bevel = 0.325f * scale;
            float widthHalf = 1.2f * scale;
            float height = -0.15f * scale;
            float bottom = -2.5f * scale;

            float LowSide = widthHalf - bevel;
            float TSide = height - bevel;

            prefabFrameStart[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameStart[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameStart[2] = new Vector3(widthHalf, TSide, 0);
            prefabFrameStart[3] = new Vector3(LowSide, height, 0);
            prefabFrameStart[4] = new Vector3(-LowSide, height, 0);
            prefabFrameStart[5] = new Vector3(-widthHalf, TSide, 0);

            prefabFrameEnd[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameEnd[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameEnd[2] = new Vector3(widthHalf, TSide, 0);
            prefabFrameEnd[3] = new Vector3(LowSide, height, 0);
            prefabFrameEnd[4] = new Vector3(-LowSide, height, 0);
            prefabFrameEnd[5] = new Vector3(-widthHalf, TSide, 0);

            // 12 frameSection vertices
            prefabFrameSection[0] = new Vector3(-widthHalf, bottom, 0);
            prefabFrameSection[1] = new Vector3(widthHalf, bottom, 0);
            prefabFrameSection[2] = new Vector3(widthHalf, bottom, 0);
            prefabFrameSection[3] = new Vector3(widthHalf, TSide, 0);
            prefabFrameSection[4] = new Vector3(widthHalf, TSide, 0);
            prefabFrameSection[5] = new Vector3(LowSide, height, 0);
            prefabFrameSection[6] = new Vector3(LowSide, height, 0);
            prefabFrameSection[7] = new Vector3(-LowSide, height, 0);
            prefabFrameSection[8] = new Vector3(-LowSide, height, 0);
            prefabFrameSection[9] = new Vector3(-widthHalf, TSide, 0);
            prefabFrameSection[10] = new Vector3(-widthHalf, TSide, 0);
            prefabFrameSection[11] = new Vector3(-widthHalf, bottom, 0);

            Mesh iron = MF.mesh;
            if (iron == null)
                iron = new Mesh();
            FormElongatedPrismFromSpecs(iron, Type, MF, localPoints);
        }


        private static List<Vector3> verts = new List<Vector3>();
        private static List<int> vertIndices = new List<int>();
        private static List<Vector3> Normals = new List<Vector3>();
        private static List<Vector2> UVs = new List<Vector2>();
        /// <summary>
        /// set frameStart, frameEnd, and frameSection entirely before use!!!
        /// </summary>
        /// <param name="MF"></param>
        /// <param name="localPoints"></param>
        private static void FormElongatedPrismFromSpecs(Mesh iron, RailType Type, MeshFilter MF, Vector3[] localPoints)
        {
            int localPointsCount = localPoints.Length;
            if (localPointsCount == 0)
            {
                // Nothing to make.  Just Clear.
                MF.mesh = null;
                return;
            }

            // Set up the starting vertices
            Quaternion quat = Quaternion.LookRotation((localPoints[0] - localPoints[1]).normalized, Vector3.up);
            verts.Clear();
            for (int step = 0; step < prefabFrameStart.Length; step++)
            {
                verts.Add(localPoints[0] + (quat * prefabFrameStart[step]));
            }
            for (int step2 = 0; step2 < prefabFrameSection.Length; step2++)
            {
                verts.Add(localPoints[0] + (quat * prefabFrameSection[step2]));
            }

            // Set up the middle vertices
            for (int step = 1; step < localPointsCount - 2; step++)
            {
                quat = Quaternion.LookRotation((localPoints[step - 1] - localPoints[step + 1]).normalized, Vector3.up);
                for (int step2 = 0; step2 < prefabFrameSection.Length; step2++)
                {
                    verts.Add(localPoints[step] + (quat * prefabFrameSection[step2]));
                }
            }
            // Set up the end vertices
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 2] - localPoints[localPointsCount - 1]).normalized, Vector3.up);
            for (int step2 = 0; step2 < prefabFrameSection.Length; step2++)
            {
                verts.Add(localPoints[localPointsCount - 1] + (quat * prefabFrameSection[step2]));
            }
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 1] - localPoints[localPointsCount - 2]).normalized, Vector3.up);
            for (int step = 0; step < prefabFrameEnd.Length; step++)
            {
                verts.Add(localPoints[localPointsCount - 1] + (quat * prefabFrameEnd[step]));
            }

            // Push the vertices!
            MF.mesh = iron;
            iron.Clear();
            iron.SetVertices(verts);
            //DebugRandAddi.Log("Set " + vertices.Count + " Vertices.");


            // TRIANGLES
            //DebugRandAddi.Log("Making Triangles...");
            vertIndices.Clear();
            // Set up start indices
            vertIndices.AddRange(prefabFrameEndIndexes);
            int indiceIndexPos = prefabFrameEndIndexCount;

            // Set up middle indices
            for (int step = 0; step < localPointsCount - 2; step++)
            {
                for (int step2 = 0; step2 < prefabFrameSectionIndexes.Length; step2++)
                {
                    vertIndices.Add(prefabFrameSectionIndexes[step2] + indiceIndexPos);
                }
                indiceIndexPos += frameSectionIndexCount;
            }
            indiceIndexPos += frameSectionIndexCount;
            // Set up end indices
            for (int step2 = 0; step2 < prefabFrameEndIndexes.Length; step2++)
            {
                vertIndices.Add(prefabFrameEndIndexes[step2] + indiceIndexPos);
            }

            // Push Triangles
            iron.SetTriangles(vertIndices, 0);
            //DebugRandAddi.Log("Highest index is " + (5 + indiceIndexPos));
            //DebugRandAddi.Log("Set " + vertIndices.Count + " points for Triangles.");


            // NORMALS!
            //DebugRandAddi.Log("Making Normals...");
            Normals.Clear();
            // Set up starting normals
            quat = Quaternion.LookRotation((localPoints[0] - localPoints[1]).normalized, Vector3.up);
            for (int step = 0; step < frameEndNormals.Length; step++)
            {
                Normals.Add(localPoints[0] + (quat * frameEndNormals[step]));
            }
            for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
            {
                Normals.Add(localPoints[0] + (quat * frameSectionNormals[step2]));
            }

            // Set up middle normals
            for (int step = 1; step < localPointsCount - 2; step++)
            {
                quat = Quaternion.LookRotation((localPoints[step - 1] - localPoints[step + 1]).normalized, Vector3.up);
                for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
                {
                    Normals.Add(localPoints[step] + (quat * frameSectionNormals[step2]));
                }
            }
            // Set up end normals
            
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 2] - localPoints[localPointsCount - 1]).normalized, Vector3.up);
            for (int step2 = 0; step2 < frameSectionNormals.Length; step2++)
            {
                Normals.Add(localPoints[localPointsCount - 1] + (quat * frameSectionNormals[step2]));
            }
            quat = Quaternion.LookRotation((localPoints[localPointsCount - 1] - localPoints[localPointsCount - 2]).normalized, Vector3.up);
            for (int step = 0; step < frameEndNormals.Length; step++)
            {
                Normals.Add(localPoints[localPointsCount - 1] + (quat * frameEndNormals[step]));
            }

            // Push Normals
            iron.SetNormals(Normals);
            iron.RecalculateNormals();
            //DebugRandAddi.Log("Set " + Normals.Count + " Normals.");

            // Push UVs
            //DebugRandAddi.Log("Making UVs...");
            Vector2 refUVSpot1 = ManRails.railTypeStats[Type].railIronTexPos;
            Vector2 refUVSpot2 = new Vector2(refUVSpot1.x + 0.001f, refUVSpot1.y);
            Vector2 refUVSpot3 = new Vector2(refUVSpot1.x + 0.001f, refUVSpot1.y - 0.001f);
            UVs.Clear();
            for (int step = 0; step < verts.Count / 3; step++)
            {
                UVs.Add(refUVSpot1);
                UVs.Add(refUVSpot2);
                UVs.Add(refUVSpot3);
            }
            iron.SetUVs(0, UVs);
            //DebugRandAddi.Log("Set " + UVs.Count + " UV points.");

            DebugRandAddi.Info("Complete!");
        }
    }
}
