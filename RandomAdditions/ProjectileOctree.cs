using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class OctreeProj
    {
        private const int MaxWorldHalf = 512; // Power of 2  [alt 256]
        private const int maxVecOcT = 16;//8
        private const int minVecOcT = 4;//8
        private const int maxDepth = 32;
        internal IntVector3 Origin = IntVector3.zero;//Singleton.cameraTrans.position;
        private static List<KeyValuePair<OctreeBranch, Projectile>> Projectiles = new List<KeyValuePair<OctreeBranch, Projectile>>();
        internal OctreeBranch Trunk;
        private bool updatedThisFrame = false;

        public OctreeProj()
        {
            Trunk = new OctreeBranch(this, null, MaxWorldHalf, 0);
        }

        public void PostFramePrep()
        {
            updatedThisFrame = false;
        }

        public bool NavigateOctree(Vector3 position, float range, out List<KeyValuePair<float, Projectile>> projectiles, int searchNum = 10)
        {
            //Debug.Log("RandomAdditions: OctreeProj(NavigateOctree) - CALLED");
            UpdatePos();
            projectiles = new List<KeyValuePair<float, Projectile>>();
            float rangeAlt = range + MaxWorldHalf;
            Vector3 posMin = new Vector3(position.x - rangeAlt, position.y - rangeAlt, position.z - rangeAlt);
            Vector3 posMax = new Vector3(position.x + rangeAlt, position.y + rangeAlt, position.z + rangeAlt);
            if (!Trunk.NavigateOctant(position, range, posMin, posMax, searchNum, out List<Projectile> found))
                return false;
            float rangeSqr = range * range;
            int count = found.Count;
            for (int step = 0; step < count;)
            {
                Projectile proj = found.ElementAt(step);
                if (!(bool)proj?.rbody)
                {
                    DebugRandAddi.Log("RandomAdditions: OctreeProj(NavigateOctree) - error - RBODY is NULL");
                    Remove(proj);
                    found.RemoveAt(step);
                    count--;
                    continue;
                }
                if (!(bool)proj.Shooter)
                {
                    //Debug.Log("RandomAdditions: OctreeProj(NavigateOctree) - Shooter null");
                    Remove(proj);
                    found.RemoveAt(step);
                    count--;
                    continue;
                }
                float dist = (proj.rbody.position - position).sqrMagnitude;
                if (dist <= rangeSqr)
                {
                    //Rigidbody rbodyC = proj.rbody;
                    projectiles.Add(new KeyValuePair<float, Projectile>(dist, proj));
                }
                step++;
            }
            if (Projectiles.Count() == 0)
            {
                //Debug.Log("RandomAdditions: OctreeProj(NavigateOctree) - EMPTY");
                return false;
            }
            return projectiles.Count > 0;
        }

        public bool Reassign(Projectile proj, OctreeBranch bran)
        {
            //Debug.Log("RandomAdditions: OctreeProj(Remove) - CALLED");
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                KeyValuePair<OctreeBranch, Projectile> pair = Projectiles.ElementAt(step);
                if (pair.Value == proj)
                {
                    Projectiles.RemoveAt(step);
                    KeyValuePair<OctreeBranch, Projectile> pair2 = new KeyValuePair<OctreeBranch, Projectile>(bran, proj);
                    Projectiles.Add(pair2);
                    return true;
                }
                step++;
            }
            return false;
        }
        public bool Remove(Projectile proj)
        {
            //Debug.Log("RandomAdditions: OctreeProj(Remove) - CALLED");
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                KeyValuePair<OctreeBranch, Projectile> pair = Projectiles.ElementAt(step);
                if (pair.Value == proj)
                {
                    if (pair.Key == null)
                    {
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    if (!pair.Key.RemoveProj(proj))
                    {
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    Projectiles.RemoveAt(step);
                    return true;
                }
                step++;
            }
            return false;
        }
        public void Add(Projectile proj)
        {
            //Debug.Log("RandomAdditions: OctreeProj(Add) - CALLED");
            KeyValuePair<OctreeBranch, Projectile> pair = new KeyValuePair<OctreeBranch, Projectile>(Trunk.AddProjectile(proj), proj);
            Projectiles.Add(pair);
        }
        public void UpdatePos()
        {
            if (updatedThisFrame)
                return;
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                KeyValuePair<OctreeBranch, Projectile> pair = Projectiles.ElementAt(step);
                Projectile proj = pair.Value;
                if (pair.Key == null)
                {
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                if (!(bool)proj)
                {
                    pair.Key.RemoveProj(proj);
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                if (!(bool)proj.Shooter)
                {
                    pair.Key.RemoveProj(proj);
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                if (pair.Key.NotWithinCube(proj.rbody.position))
                {   // Rennovate to new box
                    pair.Key.RemoveProj(proj);
                    Projectiles.RemoveAt(step);
                    Add(proj);
                    count--;
                    continue;
                }
                step++;
            }
        }
        public void UpdateWorldPos(IntVector3 change)
        {
            Origin += change;
            foreach (KeyValuePair<OctreeBranch, Projectile> pair in Projectiles)
            {
                pair.Key.UpdateBranch();
            }
        }

        // Utilities
        private byte GetOctIndex(Vector3 position)
        {
            if (position.y - Origin.y >= 0)
            { // up
                if (position.x - Origin.x >= 0)
                {   // right
                    if (position.z - Origin.z >= 0)
                    {   // north
                        return 0;
                    }
                    else
                    {   // south
                        return 1;
                    }
                }
                else
                {   // left
                    if (position.z - Origin.z >= 0)
                    {   // north
                        return 2;
                    }
                    else
                    {   // south
                        return 3;
                    }
                }
            }
            else
            {   // down
                if (position.x - Origin.x >= 0)
                {   // right
                    if (position.z - Origin.z >= 0)
                    {   // north
                        return 4;
                    }
                    else
                    {   // south
                        return 5;
                    }
                }
                else
                {   // left
                    if (position.z - Origin.z >= 0)
                    {   // north
                        return 6;
                    }
                    else
                    {   // south
                        return 7;
                    }
                }
            }
        }


        public class OctreeBranch
        {
            /*
             *  Here's how it goes:
             *  0: up right north   [URN]
             *  1: up right south   [URS]
             *  2: up left north    [ULN]
             *  3: up left south    [ULS]
             *  4: down right north [SRN]
             *  5: down right south [SRS]
             *  6: down left north  [SLN]
             *  7: down left south  [SLS]
             */
            internal static Vector3[] spaceMap = new Vector3[8]{
            Vector3.one,
            new Vector3(1,1,-1),
            new Vector3(-1,1,1),
            new Vector3(-1,1,-1),
            new Vector3(1,-1,1),
            new Vector3(1,-1,-1),
            new Vector3(-1,-1,1),
            -1 * Vector3.one,
        };
            internal static byte[][] extMap = new byte[8][] {
            new byte[8] { //[URN]
                0,1,2,4,3,5,6,7,
            },
            new byte[8] { //[URS]
                1,0,3,5,2,4,7,6,
            },
            new byte[8] { //[ULN]
                2,3,6,0,1,4,7,5,
            },
            new byte[8] { //[ULS]
                3,1,2,7,0,5,6,4,
            },
            new byte[8] {
                4,0,5,6,1,2,7,3,
            },
            new byte[8] {
                5,1,4,7,3,6,0,2,
            },
            new byte[8] {
                6,2,4,7,0,3,5,1,
            },
            new byte[8] {
                7,3,5,6,1,2,4,0,
            },
        };

            //internal bool active = false;
            internal bool IsTrunk = false;
            internal byte listDir = 0;
            internal int scale = 4;
            internal OctreeBranch[] branches = null;
            internal OctreeBranch hostBranch = null;
            internal Vector3 WorldPosOffset = Vector3.zero;
            internal Vector3 BoundsMax = Vector3.zero;
            internal Vector3 BoundsMin = Vector3.zero;
            internal List<Projectile> Projectiles;
            internal OctreeProj main;
            internal Vector3 origin { get { return main.Origin; } }
            internal bool hasBranches { get { return branches != null; } }
            internal bool hasProjectiles { get { return Projectiles != null; } }

            public OctreeBranch(OctreeProj root, OctreeBranch parent, int rescaled, byte octIndex)
            {
                main = root;
                scale = rescaled;
                BoundsMin = new Vector3(WorldPosOffset.x - scale, WorldPosOffset.y - scale, WorldPosOffset.z - scale);
                BoundsMax = new Vector3(WorldPosOffset.x + scale, WorldPosOffset.y + scale, WorldPosOffset.z + scale);
                listDir = octIndex;
                hostBranch = parent;
                IsTrunk = hostBranch == null;
                UpdateBranch();
            }

            public void UpdateBranch()
            {
                if (IsTrunk)
                {
                    WorldPosOffset = origin;
                    return;
                }
                WorldPosOffset = hostBranch.WorldPosOffset + (spaceMap[listDir] * scale);
            }

            //internal byte RemoveCountdown = 25;

            // NAVIGATION of Octants (directional)
            /// <summary>
            /// Gets the projectile in relation to where they are - grabs the minCount for each direction
            /// </summary>
            /// <param name="pos">Position to get the closest of</param>
            /// <param name="dist">Max rough distance to search (CUBE)</param>
            /// <param name="minCount">How many to search for before we give up</param>
            /// <param name="found">Targets found</param>
            /// <returns>If it hit anything</returns>
            internal bool NavigateOctant(Vector3 pos, float range, Vector3 posMin, Vector3 posMax, int minCount, out List<Projectile> found)
            {
                found = null;
                if (hasProjectiles)
                {
                    found = Projectiles;
                    return true;
                }
                if (!hasBranches)
                {
                    DebugRandAddi.Log("RandomAdditions: OctreeBranch(NavigateOctant) - Trunk HAS NO BRANCHES OR PROJECTILES!!!");
                    return false;
                }
                if (NotWithinCubeInv(posMin, posMax))
                    return false; // outta range
                float rangeAlt = range + scale;
                posMin = new Vector3(pos.x - rangeAlt, pos.y - rangeAlt, pos.z - rangeAlt);
                posMax = new Vector3(pos.x + rangeAlt, pos.y + rangeAlt, pos.z + rangeAlt);
                int deepest = 0;
                for (byte step = 0; step < 8; step++)
                {
                    bool Done = false;
                    byte index = extMap[GetOctIndex(pos)][step];
                    if (branches[index] == null)
                        continue;
                    for (byte step2 = 0; step2 < 8; step2++)
                    {   // Depth
                        int depth = branches[index].NaviOctant(pos, step2, range, posMax, posMin, minCount, out List<Projectile> foundC, ref Done);
                        if (depth != 0 && foundC != null)
                        {   // recursive
                            if (found == null)
                                found = new List<Projectile>();
                            found.AddRange(foundC);

                            if (depth > deepest)
                                deepest = depth + 1;
                            if (found.Count() >= minCount)
                                Done = true;
                        }
                    }
                }
                return deepest > 0;
            }

            /// <summary>
            /// (INTERNAL) Gets the projectile in relation to where they are
            /// </summary>
            /// <param name="pos">Position to get the closest of</param>
            /// <param name="depthLevel">The Octant to search based on dist</param>
            /// <param name="dist">Max rough distance to search (CUBE)</param>
            /// <param name="minCount">How many to search for before we give up</param>
            /// <param name="found">Targets found</param>
            /// <param name="Done">Finished searching</param>
            /// <returns>Deepest Depth of the target hit</returns>
            private int NaviOctant(Vector3 pos, byte depthLevel, float range, Vector3 posMin, Vector3 posMax, int minCount, out List<Projectile> found, ref bool Done)
            {
                found = null;
                if (hasProjectiles)
                {
                    found = Projectiles;
                    return 1;
                }
                if (!hasBranches)
                {
                    DebugRandAddi.Log("RandomAdditions: OctreeBranch(NaviOctant) - Illegal call");
                    return 0;
                }
                if (NotWithinCubeInv(posMin, posMax))
                    return 0; // outta range
                float rangeAlt = range + scale;
                posMin = new Vector3(pos.x - rangeAlt, pos.y - rangeAlt, pos.z - rangeAlt);
                posMax = new Vector3(pos.x + rangeAlt, pos.y + rangeAlt, pos.z + rangeAlt);
                byte index = extMap[GetOctIndex(pos)][depthLevel];
                if (branches[index] == null)
                    return 0;
                int deepest = 0;
                for (byte step = 0; step < 8; step++)
                {
                    if (Done)
                        return deepest;
                    int depth = branches[index].NaviOctant(pos, step, range, posMin, posMax, minCount, out List<Projectile> foundC, ref Done);
                    if (depth != 0 && foundC != null)
                    {   // recursive
                        if (found == null)
                            found = new List<Projectile>();
                        found.AddRange(foundC);

                        if (depth > deepest)
                            deepest = depth + 1;
                        if (found.Count() >= minCount)
                            Done = true;
                    }
                }
                return deepest;
            }

            public bool RemoveProj(Projectile proj)
            {   // CALLED FROM WHERE IT IS
                if (!hasProjectiles)
                {
                    //Debug.Log("RandomAdditions: OctreeBranch(RemoveProj) - Illegal call " + StackTraceUtility.ExtractStackTrace());
                    return false;
                }
                if (Projectiles.Remove(proj))
                    CheckRemove();
                else
                {
                    DebugRandAddi.Log("RandomAdditions: OctreeBranch(RemoveProj) - Remove request failed " + StackTraceUtility.ExtractStackTrace());
                }
                return true;
            }
            public void CheckRemove()
            {
                if (IsTrunk)
                    return; // cannot destroy trunk
                if (hasBranches)
                {
                    if (branches.All(delegate (OctreeBranch cand) { return cand == null; }))
                    {
                        branches = null;
                        hostBranch.TryRetractBranches();
                    }
                    else
                    {
                        //Debug.Log("RandomAdditions: OctreeBranch(CheckRemove) - Illegal call");
                        return;
                    }
                }
                else if (hasProjectiles)
                {
                    if (Projectiles.Count() == 0)
                    {
                        hostBranch.branches[listDir] = null;
                        hostBranch.TryRetractBranches();
                    }
                    else if (Projectiles.Count() <= minVecOcT)
                    {
                        hostBranch.TryRetractBranches();
                    }
                    else
                        return;
                }
                hostBranch.CheckRemove();

                //Debug.Log("RandomAdditions: OctreeBranch(CheckRemove) - The host branch");
            }
            private bool TryRetractBranches()
            {
                try
                {
                    //Debug.Log("RandomAdditions: OctreeProj(ExtendBranches) - CALLED");
                    int totCount = 0;

                    List<Projectile> toTransfer = new List<Projectile>();
                    foreach (OctreeBranch branch in branches)
                    {
                        if (branch == null)
                            continue;
                        if (branch.hasBranches)
                            return false;
                        if (branch.hasProjectiles)
                        {
                            totCount += branch.Projectiles.Count;
                            toTransfer.AddRange(branch.Projectiles);
                            if (totCount >= maxVecOcT)
                                return false;
                        }
                    }
                    Projectiles = toTransfer;
                    foreach (Projectile proj in Projectiles)
                    {
                        main.Reassign(proj, this);
                    }
                    branches = null;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// Add a projectile to the tree.
            /// </summary>
            /// <param name="proj">The Projectile to add</param>
            /// <param name="branch">The branch where it was assigned</param>
            /// <returns>if it worked or not</returns>
            public OctreeBranch AddProjectile(Projectile proj)
            {
                if (!hasBranches)
                {
                    //Debug.Log("RandomAdditions: OctreeBranch(AddProjectile) - CALLED oct " + listDir + " lv " + scale);
                    if (!hasProjectiles)
                        Projectiles = new List<Projectile>();
                    Projectiles.Add(proj);
                    try
                    {
                        if (Projectiles.Count() > maxVecOcT)
                        {
                            if (ExtendBranches(out OctreeBranch branch))
                                return branch;
                        }
                    }
                    catch (Exception e)
                    {
                        DebugRandAddi.Log("RandomAdditions: OctreeProj(ExtendBranches)! - error " + e);
                    }
                    return this;
                }
                else
                {
                    //Debug.Log("RandomAdditions: OctreeBranch(AddProjectile) - CALLED [Extend] oct " + listDir + " lv " + scale);
                    byte index = GetOctIndex(proj.trans.position);
                    if (branches[index] == null)
                        return ExtendBranch(index, proj);
                    return branches[index].AddProjectile(proj);
                }
            }
            private bool ExtendBranches(out OctreeBranch branch)
            {
                //Debug.Log("RandomAdditions: OctreeProj(ExtendBranches) - CALLED");
                branch = null;
                if (scale <= maxDepth)
                    return false;
                branches = new OctreeBranch[8] { 
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                };
                int rescale = scale / 2;
                byte octIndex;
                foreach (Projectile projC in Projectiles)
                {
                    Vector3 position = projC.trans.position;
                    octIndex = GetOctIndex(position);
                    OctreeBranch bran = branches[octIndex];
                    if (bran == null)
                    {
                        branches[octIndex] = new OctreeBranch(main, this, rescale, octIndex);
                    }
                    branch = branches[octIndex].AddProjectile(projC);
                    main.Reassign(projC, bran);
                }
                Projectiles = null;
                return true;
            }
            private OctreeBranch ExtendBranch(byte index, Projectile toAdd)
            {
                if (branches[index] != null)
                {
                    DebugRandAddi.Log("RandomAdditions: OctreeBranch(ExtendBranch) - Called when branch already exists");
                }
                branches[index] = new OctreeBranch(main, this, scale / 2, index);
                OctreeBranch bran = branches[index].AddProjectile(toAdd);
                main.Reassign(toAdd, bran);
                return bran;
            }

            // Utilities
            public bool NotWithinCube(Vector3 pos)
            {
                return pos.x > BoundsMax.x || pos.y > BoundsMax.y || pos.z > BoundsMax.z ||
                    pos.x < BoundsMin.x || pos.y < BoundsMin.y || pos.z < BoundsMin.z;
            }
            private bool NotWithinCubeInv(Vector3 posMin, Vector3 posMax)
            {
                return posMin.x > WorldPosOffset.x || posMin.y > WorldPosOffset.y || posMin.z > WorldPosOffset.z ||
                    posMax.x < WorldPosOffset.x || posMax.y < WorldPosOffset.y || posMax.z < WorldPosOffset.z;
            }
            private byte GetOctIndex(Vector3 position)
            {
                if (position.y >= WorldPosOffset.y)
                { // up
                    if (position.x >= WorldPosOffset.x )
                    {   // right
                        if (position.z >= WorldPosOffset.z)
                        {   // north
                            return 0;
                        }
                        else
                        {   // south
                            return 1;
                        }
                    }
                    else
                    {   // left
                        if (position.z >= WorldPosOffset.z)
                        {   // north
                            return 2;
                        }
                        else
                        {   // south
                            return 3;
                        }
                    }
                }
                else
                {   // down
                    if (position.x >= WorldPosOffset.x)
                    {   // right
                        if (position.z >= WorldPosOffset.z)
                        {   // north
                            return 4;
                        }
                        else
                        {   // south
                            return 5;
                        }
                    }
                    else
                    {   // left
                        if (position.z >= WorldPosOffset.z)
                        {   // north
                            return 6;
                        }
                        else
                        {   // south
                            return 7;
                        }
                    }
                }
            }
        }
    }
}
