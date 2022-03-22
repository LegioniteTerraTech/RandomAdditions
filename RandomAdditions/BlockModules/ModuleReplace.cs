using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ModuleReplace : RandomAdditions.ModuleReplace { };

namespace RandomAdditions
{
    /*
    "RandomAdditions.ModuleReplace": { // Replaces the specified block types on enemy tech loading with the block containing this module
        "Uniform": false,       // Should this be applied uniformly?
        "ReplaceGrade": 0,      // The minimum grade the player must be before encountering this
        "WeightedChance": 100,  // The chance this will spawn in relation to other blocks [1 - 2500]
        "CanReplace": [         // What Blocktype this replaces - can also accept ints
            // ONLY Supports replacement of vanilla blocks! Do not use to replace modded blocks!
            "GSOMGunFixed_111",
            "GSOCannonTurret_111",
        ],
        "ReplaceCondition": "Any", // What terrain to replace based on
        // Other options:
        // "Any"
        // "Land"
        // "Sea"
        
        // Offset your block to match the other block
        "ReplaceOffsetPosition": { "x":0, "y":0, "z":0 },  //The offset position this will take when replacing
        "ReplaceOffsetRotationF": {"x":0, "y":0, "z":0 },  //The offset rotation heading this will take (Forwards!) when replacing
        "ReplaceOffsetRotationT": {"x":0, "y":0, "z":0 },  //The offset rotation heading this will take (Top!) when replacing
    },
     * 
     */
    public class ModuleReplace : MonoBehaviour
    {
        private const float weightTolerence = 5;

        internal TankBlock block;
        public bool Uniform = false;
        public int ReplaceGrade = 0;
        public short WeightedChance = 100;
        public Vector3 ReplaceOffsetPosition = Vector3.zero;
        public Vector3 ReplaceOffsetRotationF = Vector3.forward;
        public Vector3 ReplaceOffsetRotationT = Vector3.up;
        public List<BlockTypes> CanReplace = new List<BlockTypes>();
        public ReplaceCondition ReplaceCondition = ReplaceCondition.Any;

        internal Quaternion offsetRot = default;
        //private BlockTypes typeM;
        internal BlockTypes type = BlockTypes.GSOAIController_111;

        internal void Init(TankBlock blockC)
        {
            Debug.Log("RandomAdditions: ModuleReplace - Init for " + blockC.name);
            TankBlock block;
            ModuleReplace typeM;
            try
            {
                block = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(blockC.BlockType);
                typeM = block.GetComponent<ModuleReplace>();
                if (ReplaceManager.HasBlock(typeM))
                    return;
                typeM.type = block.BlockType;
                if (typeM.type == BlockTypes.GSOAIController_111)
                    Debug.Log("RandomAdditions: ModuleReplace - " + block.name + " FAILIURE IN SETTING TYPE");
                Debug.Log("RandomAdditions: ModuleReplace - " + block.BlockType.ToString());
            }
            catch
            {
                //Debug.Log("RandomAdditions: ModuleReplace - TankBlock FAILED at it's job");
                return;
            }
            //if (ReplaceManager.HasBlock(typeM))
            //    ReplaceManager.RemoveBlock(typeM);

            if (ReplaceOffsetRotationF.ApproxZero())
            {
                Debug.Log("RandomAdditions: ModuleReplace - " + block.name + ": ReplaceOffsetRotationF not set");
                ReplaceOffsetRotationF = Vector3.forward;
            }
            if (ReplaceOffsetRotationT.ApproxZero())
            {
                Debug.Log("RandomAdditions: ModuleReplace - " + block.name + ": ReplaceOffsetRotationT not set");
                ReplaceOffsetRotationT = Vector3.up;
            }
            offsetRot.SetLookRotation(ReplaceOffsetRotationF, ReplaceOffsetRotationT);
            Debug.Log("RandomAdditions: ModuleReplace - " + block.name + " offsetRot now " + (offsetRot * Vector3.forward));

            if (WeightedChance > 2500)
            {
                LogHandler.ThrowWarning("ModuleReplace: Block " + block.name + " has too high of a WeightedChance - 2500 is max");
                WeightedChance = 2500;
            }
            if (WeightedChance < 1)
            {
                LogHandler.ThrowWarning("ModuleReplace: Block " + block.name + " has too low of a WeightedChance - 1 is minimum");
                WeightedChance = 1;
            }
            List<BlockTypes> check = CanReplace.Distinct().ToList();
            if (check.Count < CanReplace.Count)
            {
                LogHandler.ThrowWarning("ModuleReplace: Block " + block.name + " has duplicate Blocktypes in CanReplace!");
                CanReplace = check;
            }
            Debug.Log("ModuleReplace: " + offsetRot.ToString() + " | " + (offsetRot * Vector3.forward).ToString());
            if (CheckIfValid())
                ReplaceManager.AddBlock(typeM);
            else
                Debug.Log("RandomAdditions: ModuleReplace - Could not add " + block.name + " - failed critical checks.");
        }
        private void OnPool()
        {
            //Debug.Log("RandomAdditions: ModuleReplace - Pooling for " + name);
            try
            {
                block = GetComponent<TankBlock>();
                type = block.BlockType;
                if (type == BlockTypes.GSOAIController_111)
                    Debug.Log("RandomAdditions: ModuleReplace - " + block.name + " FAILIURE IN SETTING TYPE");
                //Debug.Log("RandomAdditions: ModuleReplace - " + type.ToString());
                if (ReplaceOffsetRotationF.ApproxZero())
                {
                    Debug.Log("RandomAdditions: ModuleReplace - " + block.name + ": ReplaceOffsetRotationF not set");
                    ReplaceOffsetRotationF = Vector3.forward;
                }
                if (ReplaceOffsetRotationT.ApproxZero())
                {
                    Debug.Log("RandomAdditions: ModuleReplace - " + block.name + ": ReplaceOffsetRotationT not set");
                    ReplaceOffsetRotationT = Vector3.up;
                }
                offsetRot.SetLookRotation(ReplaceOffsetRotationF, ReplaceOffsetRotationT);
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: ModuleReplace - Could not fetch tankblock info " + e);
                return;
            }
        }
        public bool CheckIfValid()
        {
            bool valid = true;
            TankBlock blockO = GetComponent<TankBlock>();
            if (!(bool)blockO) 
            {
                LogHandler.ThrowWarning("ModuleReplace: Instance is not attached to a valid block!");
            }
            foreach (BlockTypes bloc in CanReplace)
            {
                TankBlock blockCompare =  Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(bloc);
                //OrthoRotation oRot = new OrthoRotation(offsetRot);
                List <IntVector3> rotCells = new List<IntVector3>();
                foreach (IntVector3 vec in blockO.filledCells.ToList())
                {
                    rotCells.Add((offsetRot * vec) + ReplaceOffsetPosition);
                }
                List<Vector3> rotAPs = new List<Vector3>();
                StringBuilder loader = new StringBuilder();
                foreach (Vector3 vec in blockO.attachPoints.ToList())
                {
                    rotAPs.Add((offsetRot * vec) + ReplaceOffsetPosition);
                }
                if (!Compare(blockCompare.filledCells.ToList(), rotCells))
                {
                    foreach (IntVector3 vect in blockCompare.filledCells)
                    {
                        loader.Append(vect.ToString() + " | ");
                    }
                    string batch1 = loader.ToString();
                    loader.Clear();
                    foreach (IntVector3 vect in rotCells)
                    {
                        loader.Append(vect.ToString() + " | ");
                    }
                    string batch2 = loader.ToString();
                    LogHandler.ThrowWarning("ModuleReplace: Filled cells for block " + blockO.name + " do not match " + blockCompare.name + "! \n" + blockCompare.filledCells.ToString() + "! | " + blockO.filledCells.ToString());
                    valid = false;
                }
                if (!Contains(blockCompare.attachPoints.ToList(), rotAPs))
                {
                    loader.Clear();
                    foreach (Vector3 vect in blockCompare.attachPoints)
                    {
                        loader.Append(vect.ToString() + " | ");
                    }
                    string batch1 = loader.ToString();
                    loader.Clear();
                    foreach (Vector3 vect in rotAPs)
                    {
                        loader.Append(vect.ToString() + " | ");
                    }
                    string batch2 = loader.ToString();
                    LogHandler.ThrowWarning("ModuleReplace: Attachment points for block " + blockO.name + " do not match " + blockCompare.name + "! \n" + batch1 + "! | " + batch2);
                    valid = false;
                }
                if (blockCompare.m_DefaultMass > blockO.m_DefaultMass + weightTolerence || blockCompare.m_DefaultMass < blockO.m_DefaultMass - weightTolerence)
                {
                    LogHandler.ThrowWarning("ModuleReplace: Weight for block " + blockO.name + " is not close enough [" + (blockCompare.m_DefaultMass - weightTolerence) + "~" + (blockCompare.m_DefaultMass + weightTolerence) + "] to " + blockCompare.name + "!");
                    valid = false;
                }
            }
            return valid;
        }
        public static bool Contains(List<Vector3> case1, List<Vector3> case2)
        {
            foreach (Vector3 vec in case1)
            {
                if (!case2.Exists(delegate (Vector3 cand)
                {
                    //Debug.Log("ModuleReplace: " + vec.ToString() + " | " + cand.ToString());
                    return vec.x.Approximately(cand.x) && vec.y.Approximately(cand.y) && vec.z.Approximately(cand.z); 
                }))
                    return false;
            }
            return true;
        }
        public static bool Compare(List<IntVector3> case1, List<IntVector3> case2)
        {
            if (case1.Count != case2.Count)
                return false;
            foreach (IntVector3 vec in case1)
            {
                if (!case2.Contains(vec))
                    return false;
            }
            return true;
        }
    }


    // Handles replace operations
    internal class ReplaceManager
    {
        private static List<ModuleReplace> Replacables = new List<ModuleReplace>();
        private static Dictionary<BlockTypes, List<ModuleReplace>> Supported = new Dictionary<BlockTypes, List<ModuleReplace>>();

        public static bool HasBlock(ModuleReplace rp)
        {
            return Replacables.Contains(rp);
        }
        public static void AddBlock(ModuleReplace rp)
        {
            if (!Replacables.Contains(rp))
            {
                Replacables.Add(rp);
                Debug.Log("RandomAdditions: ReplaceManager - Registered " + rp.name);
                foreach (BlockTypes add in rp.CanReplace)
                {
                    if (Supported.TryGetValue(add, out List<ModuleReplace> listRp))
                    {
                        listRp.Add(rp);
                    }
                    else
                        Supported.Add(add, new List<ModuleReplace> { rp });
                }
            }
            else
            {
                foreach (BlockTypes add in rp.CanReplace)
                {
                    if (Supported.TryGetValue(add, out List<ModuleReplace> listRp))
                    {
                        listRp.Add(rp);
                    }
                    else
                        Supported.Add(add, new List<ModuleReplace> { rp });
                }
            }
        }
        public static void RemoveBlock(ModuleReplace rp)
        {
            if (Replacables.Contains(rp))
            {
                Replacables.Remove(rp);
                List<KeyValuePair<BlockTypes, List<ModuleReplace>>> paired = Supported.ToList();
                Supported.Clear();
                for (int step = 0; step < paired.Count(); step++)
                {
                    KeyValuePair<BlockTypes, List<ModuleReplace>> pair = paired.ElementAt(step);
                    if (pair.Value.Contains(rp))
                    {
                        if (pair.Value.Count == 1)
                            paired.RemoveAt(step);
                        else
                            pair.Value.Remove(rp);
                        step--;
                    }
                    Supported.Add(pair.Key, pair.Value);
                }
            }
        }
        public static void RemoveAllBlocks()
        {
            Supported.Clear();
            Replacables.Clear();
        }
        public static void TryReplaceBlocks(Tank tank)
        {
            if (!ManNetwork.IsHost && ManNetwork.IsNetworked)
                return;
            int attempts = 0;
            List<TankBlock> blocks = tank.blockman.IterateBlocks().ToList();
            List<ModuleReplace> ignore = new List<ModuleReplace>();
            int bCount = blocks.Count();
            Debug.Log("RandomAdditions: ReplaceManager - Launched for Tech " + tank.name + ", total blocks " + bCount);

            for (int step = 0; step < bCount; step++)
            {
                //Debug.Log("RandomAdditions: ReplaceManager - " + step + " | array: " + blocks.Count + " | est: "+ bCount);
                TankBlock block = blocks.ElementAt(step);
                if (!(bool)block)
                {
                    Debug.Log("RandomAdditions: ReplaceManager - BLOCK IS NULL");
                    continue;
                }
                if (!Supported.TryGetValue(block.BlockType, out List<ModuleReplace> replaced))
                    continue;
                attempts++;
                if (replaced.Count == 0)
                {
                    Debug.Log("RandomAdditions: ReplaceManager - BlockType " + block.name + " has an entry but nothing in it!?  How?");
                    continue;
                }
                try
                {
                    if (ManGameMode.inst.CanEarnXp())
                        replaced = replaced.FindAll(delegate (ModuleReplace cand) { return Singleton.Manager<ManLicenses>.inst.GetCurrentLevel(Singleton.Manager<ManSpawn>.inst.GetCorporation(cand.GetComponent<TankBlock>().BlockType)) <= cand.ReplaceGrade; });
                }
                catch //(Exception e)
                {
                    Debug.Log("RandomAdditions: ReplaceManager - XP fired but instance was not valid");
                    //Debug.Log("RandomAdditions: ReplaceManager - Unhandleable error " + e);
                }
                ModuleReplace replace = WeightedRAND(replaced, ref ignore, AboveTheSea(tank.transform.position));
                if (!(bool)replace)
                {
                    continue;
                }
                if (!(bool)replace.GetComponent<TankBlock>())
                {
                    Debug.Log("RandomAdditions: ReplaceManager - replace.blockO FAILED at it's job");
                    continue;
                }
                try { replace.type.ToString(); }
                catch
                {
                    Debug.Log("RandomAdditions: ReplaceManager - Visible type was not set!");
                    continue;
                }
                if (replace.Uniform)
                {
                    //Debug.Log("RandomAdditions: ReplaceManager - UniformReplace...");
                    bool massRecolor = Singleton.Manager<ManSpawn>.inst.GetCorporation(block.BlockType) == Singleton.Manager<ManSpawn>.inst.GetCorporation(replace.type);
                    BlockTypes type = block.BlockType;
                    if (ReplaceBlock(tank, block, replace, massRecolor))
                    {
                        blocks.RemoveAt(step);
                        step--;
                    }
                    List<TankBlock> blocksSame = blocks.FindAll(delegate (TankBlock cand) { return cand.BlockType == type; });
                    int bCount2 = blocksSame.Count;
                    for (int step2 = 0; step2 < bCount2; step2++)
                    {
                        //Debug.Log("RandomAdditions: ReplaceManager - u" + step2);
                        TankBlock block2 = blocksSame.ElementAt(step2);
                        if (ReplaceBlock(tank, block2, replace, massRecolor))
                        {
                            blocksSame.RemoveAt(step2);
                            bCount2--;
                            step2--;
                        }
                    }
                    blocks = tank.blockman.IterateBlocks().ToList();
                    bCount = blocks.Count;
                }
                else
                {
                    //Debug.Log("RandomAdditions: ReplaceManager - BasicReplace");
                    bool recolor = Singleton.Manager<ManSpawn>.inst.GetCorporation(block.BlockType) == Singleton.Manager<ManSpawn>.inst.GetCorporation(replace.type);
                    try
                    {
                        if (ReplaceBlock(tank, block, replace, recolor))
                        {
                            blocks.RemoveAt(step);
                            bCount--;
                            step--;
                        }
                    }
                    catch
                    {
                        Debug.Log("RandomAdditions: ReplaceManager - NULL ReplaceBlock");
                    }
                }
            }
            Debug.Log("RandomAdditions: ReplaceManager - Attempted on a total of " + attempts + " blocks.");
        }

        public static bool ReplaceBlock(Tank tank, TankBlock blockToReplace, ModuleReplace prefabToReplaceWith, bool RecolorSame)
        {
            bool removedBlock = false;
            IntVector3 pos;
            OrthoRotation rot;
            TankBlock blockAdd;
            try
            {
                //Debug.Log("RandomAdditions: ReplaceBlock - Spawning");
                blockAdd = Singleton.Manager<ManSpawn>.inst.SpawnBlock(prefabToReplaceWith.type, tank.transform.position + (Vector3.up * 100), Quaternion.identity);
                //Debug.Log("RandomAdditions: ReplaceBlock - Spawning Done");
                if (blockAdd.IsNull())
                {
                    Debug.Log("RandomAdditions: ReplaceBlock - Could not spawn new block properly!!!");
                    return false;
                }
                var newFab = blockAdd.GetComponent<ModuleReplace>();
                if (!(bool)newFab)
                {
                    Debug.Log("RandomAdditions: ReplaceBlock - Could not find ModuleReplace");
                    return false;
                }
                pos = blockToReplace.cachedLocalPosition + newFab.ReplaceOffsetPosition;
                Quaternion qRot = newFab.offsetRot;
                Quaternion qRotM = blockToReplace.transform.localRotation;
                Quaternion qRot2 = default;
                Vector3 foA = qRotM * (qRot * Vector3.forward);
                Vector3 upA = qRotM * (qRot * Vector3.up);
                qRot2.SetLookRotation(foA, upA);
                rot = new OrthoRotation(qRot2);
                if (rot != qRot2)
                {
                    bool worked = false;
                    for (int step = 0; step < OrthoRotation.NumDistinctRotations; step++)
                    {
                        OrthoRotation rotT = new OrthoRotation(OrthoRotation.AllRotations[step]);
                        bool isForeMatch = rotT * Vector3.forward == qRot2 * Vector3.forward;
                        bool isUpMatch = rotT * Vector3.up == qRot2 * Vector3.up;
                        if (isForeMatch && isUpMatch)
                        {
                            rot = rotT;
                            worked = true;
                            break;
                        }
                    }
                    if (!worked)
                    {
                        Debug.Log("RandomAdditions: ReplaceBlock - Matching failed - OrthoRotation is incompetent");
                    }
                }
                //Debug.Log("RandomAdditions: ReplaceBlock - Rotation " + newFab.offsetRot + " | " + qRotM + " | " + qRot2 + " | " + rot.ToString());
                
                tank.blockman.Detach(blockToReplace, false, false, false);
                Singleton.Manager<ManLooseBlocks>.inst.RequestDespawnBlock(blockToReplace, DespawnReason.Host);
                removedBlock = true;
            }
            catch (Exception e)
            {
                Debug.Log("RandomAdditions: ReplaceBlock - Could not remove block properly!!!" + e);
                return false;
            }
            if (RecolorSame)
            {
                blockAdd.SetSkinIndex(blockToReplace.GetSkinIndex());
            }
            if ((bool)blockAdd)
            {
                if (tank.blockman.AddBlockToTech(blockAdd, pos, rot))
                {
                    //Debug.Log("RandomAdditions: ReplaceBlock - Replacing block " + blockToReplace.name + " = Success!");
                }
                else
                    Debug.Log("RandomAdditions: ReplaceBlock - Adding block " + prefabToReplaceWith.name + " failed - could not attach block!");
            }
            else
            {
                Debug.Log("RandomAdditions: ReplaceBlock - Adding block " + prefabToReplaceWith.name + " failed - could not fetch block!");
            }
            return removedBlock;
        }
        public static ModuleReplace WeightedRAND(List<ModuleReplace> replaced, ref List<ModuleReplace> ignored, bool TankIsOverSea)
        {
            float totalWeight = 0;
            List<ModuleReplace> checkBatch = new List<ModuleReplace>();
            bool caseBasic = true;
            bool caseLand = !TankIsOverSea;
            bool caseSea = TankIsOverSea;
            if (UnityEngine.Random.Range(0, 100) > KickStart.GlobalBlockReplaceChance)
            {
                caseBasic = false;
                if (!KickStart.MandateLandReplacement)
                    caseLand = false;
                if (!KickStart.MandateSeaReplacement)
                    caseSea = false;
            }

            foreach (ModuleReplace rep in replaced)
            {
                bool isSta = rep.ReplaceCondition == ReplaceCondition.Sea;
                bool isLand = rep.ReplaceCondition == ReplaceCondition.Land;
                bool isSea = rep.ReplaceCondition == ReplaceCondition.Sea;
                if (!ignored.Contains(rep) && ((isSea && caseSea) || (isLand && caseLand) || (isSta && caseBasic)))
                {
                    totalWeight += rep.WeightedChance;
                    checkBatch.Add(rep);
                }
            }
            if (checkBatch.Count == 0)
                return null;
            float RANDpick = UnityEngine.Random.Range(0, totalWeight);
            foreach (ModuleReplace rep in checkBatch)
            {
                RANDpick -= rep.WeightedChance;
                if (RANDpick <= 0)
                {
                    foreach (ModuleReplace rep2 in checkBatch)
                    {
                        if (rep2.Uniform)
                            ignored.Add(rep2);
                    }
                    return rep;
                }
            }
            Debug.Log("RandomAdditions: ReplaceManager - WeightedRAND failed");
            return replaced.GetRandomEntry();
        }
        
        public static bool AboveTheSea(Vector3 input)
        {
            if (!KickStart.isWaterModPresent)
                return false;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(input, out float height);
            if (terrain)
            {
                if (height < KickStart.WaterHeight)
                    return true;
            }
            else
                if (50 < KickStart.WaterHeight)
                return true;
            return false;
        }
    }

    public enum ReplaceCondition
    {
        Any,
        Land,
        Sea,
    }
}
