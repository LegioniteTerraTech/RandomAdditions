using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions.RailSystem
{
    public enum RailTieType : byte
    {
        Default,
        OneWay,
        OneWayReversed,
    }

    internal class RailTypeStats
    {
        internal readonly RailType Type;
        internal readonly float RailGauge;
        internal readonly float railTurnRate;
        internal readonly float railMiniHeight;
        internal readonly float MaxIdealHeightDeviance;
        internal readonly float railCrossLength;
        internal readonly float railCrossHalfWidth;
        internal readonly float railIronScale;
        internal readonly FactionSubTypes railCorp;
        internal readonly Vector2 railIronTexPos;
        /// <summary> The multiplier in relation to the turn angle between rail points in RailSegment</summary>
        internal readonly float bankLevel;
        /// <summary> The maximum angle before we try elevating the track off the ground with steeper bank angles </summary>
        internal readonly float minBankAngle;
        /// <summary> The highest angle we can bank to </summary>
        internal readonly float maxBankAngle;
        /// <summary> The max number of tracks spent on a long curve to turn the rails. Minimum 2.</summary>
        internal readonly int maxEndCurveTracks;
        internal Dictionary<RailTieType, RailTieVisual> RailTies;


        internal RailTypeStats(RailType type, FactionSubTypes Corp, 
            float railGauge, float turnRate, float minimumHeight, float idealHeightDev, float RailCrossSpacing, float hWidth,
            float ironScale, Vector2 pos, float BankLevel, float minBankLevel,
            float maxBankLevel, int numEndCurveTracks)
        {
            Type = type;
            RailGauge = railGauge;
            railTurnRate = turnRate;
            railMiniHeight = minimumHeight;
            MaxIdealHeightDeviance = idealHeightDev;
            railCrossLength = RailCrossSpacing;
            railCrossHalfWidth = hWidth;
            railIronScale = ironScale;
            railCorp = Corp;
            railIronTexPos = pos;
            bankLevel = BankLevel;
            minBankAngle = minBankLevel;
            maxBankAngle = maxBankLevel;
            maxEndCurveTracks = Mathf.Max(2, numEndCurveTracks);
            RailTies = new Dictionary<RailTieType, RailTieVisual>();
        }
    }

    internal class RailTypeStats<T> : RailTypeStats where T : RailSegment
    {
        internal RailTypeStats(ModContainer MC, RailType type, string Name, string ModelNameNoExt, FactionSubTypes Corp, 
            float railGauge, float turnRate, float minimumHeight, float idealHeightDev, float RailCrossSpacing, float hWidth,
            float ironScale, Vector2 pos, float BankLevel, float minBankLevel, 
            float maxBankLevel, int numEndCurveTracks) : base(type, Corp, railGauge,
                turnRate, minimumHeight, idealHeightDev, RailCrossSpacing, hWidth, ironScale, pos, BankLevel,
                minBankLevel, maxBankLevel, numEndCurveTracks)
        {
            foreach (var item in Enum.GetValues(typeof(RailTieType)))
            {
                RailTies.Add((RailTieType)item, new RailTieVisual<T>(AssembleSegmentGroundInstance(MC, type, Name,
                    ModelNameNoExt, Corp, out var light), light));
            }
        }


        internal static Transform AssembleSegmentGroundInstance(ModContainer MC, RailType Type, string Name, string ModelNameNoExt, FactionSubTypes faction, out Transform lightInst)
        {
            DebugRandAddi.Log("Making Track for " + Name);
            GameObject GO = UnityEngine.Object.Instantiate(new GameObject(Name + "_Seg"), null);
            RailSegmentGround RS = GO.AddComponent<RailSegmentGround>();
            RS.BaseInit();
            Transform Trans = RS.transform;
            Trans.CreatePool(RailMeshBuilder.segmentPoolInitSize);
            ManRails.prefabTracks[Type] = Trans;
            GO.SetActive(false);

            DebugRandAddi.Log("Making Track Cross for " + Name);
            Mesh mesh = ResourcesHelper.GetMeshFromModAssetBundle(MC, ModelNameNoExt);
            if (mesh == null)
            {
                DebugRandAddi.Assert(ModelNameNoExt + " - Unable to make track cross visual for world rail");
                //return;
            }
            if (!ManTechMaterialSwap.inst.m_FinalCorpMaterials.TryGetValue((int)faction, out Material mat))
            {
                DebugRandAddi.Assert(faction.ToString() + "_Main could not be found!  unable to load track cross visual texture");
                //return;
            }

            GameObject prefab = new GameObject(Name);
            var MF = prefab.AddComponent<MeshFilter>();
            MF.sharedMesh = mesh;
            var MR = prefab.AddComponent<MeshRenderer>();
            MR.sharedMaterial = mat;
            Transform transC = prefab.transform;
            transC.CreatePool(RailMeshBuilder.crossPoolInitSize);
            prefab.SetActive(false);

            mesh = ResourcesHelper.GetMeshFromModAssetBundle(MC, ModelNameNoExt + "_light");
            if (mesh == null)
            {
                DebugRandAddi.Log(ModelNameNoExt + "_light - Unable to make track cross visual for light rail");
                lightInst = transC;
                return transC;
            }

            GameObject prefab2 = new GameObject(Name);
            MF = prefab2.AddComponent<MeshFilter>();
            MF.sharedMesh = mesh;
            MR = prefab2.AddComponent<MeshRenderer>();
            MR.sharedMaterial = mat;
            Transform transL = prefab2.transform;
            transL.CreatePool(RailMeshBuilder.crossPoolInitSize);
            prefab2.SetActive(false);
            lightInst = transL;

            return transC;
        }
    }

    internal interface RailTieVisual
    {
        Transform prefabWorld { get; }
        Transform prefabLight { get; }
    }

    internal struct RailTieVisual<T> : RailTieVisual where T : RailSegment
    {
        private Transform railPrefabWorld;
        private Transform railPrefabLight;
        public Transform prefabWorld => railPrefabWorld;
        public Transform prefabLight => railPrefabLight;
        internal RailTieVisual(Transform toPrefab, Transform toPrefabLight)
        {
            railPrefabWorld = toPrefab;
            railPrefabLight = toPrefabLight;
        }
    }
}
