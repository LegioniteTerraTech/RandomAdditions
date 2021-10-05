using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    public class ProjectileCubetree
    {   // Dirty cheap octree that's not an octree but a coordinate
        internal const int CubeSize = 30;
        //internal bool useCheap = false;
        private static List<Projectile> Projectiles = new List<Projectile>();
        internal List<CubeBranch> CubeBranches = new List<CubeBranch>();
        private bool updatedThisFrame = false;

        public void PostFramePrep()
        {
            updatedThisFrame = false;
        }
        public void PurgeAll()
        {
            //Projectiles = new List<Projectile>();
            //CubeBranches = new List<CubeBranch>();
        }
        public bool Remove(Projectile proj)
        {
            if (proj.IsNull())
            {
                Debug.Log("RandomAdditions: ProjectileCubetree - Was told to remove NULL");
                return false;
            }
            if (!Projectiles.Remove(proj))
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree - Was told to remove something not in the list?!?");
                //PurgeAll();
                return false;
            }
            IntVector3 CBp = CubeBranch.GetCBPos(proj.trans.position);
            int count = CubeBranches.Count;
            for (int step = 0; step < count; step++)
            {
                CubeBranch CBc = CubeBranches.ElementAt(step);
                if (CBc.WorldPosition == CBp)
                {
                    if (CBc.Remove(proj))
                        CubeBranches.RemoveAt(step);
                    return true;
                }
            }
            return false;
        }
        public void Add(Projectile proj)
        {
            if (Projectiles.Contains(proj))
                return;
            if (Projectiles.Count > 2000)
            {
                Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max projectiles");
                return;
            }
            Projectiles.Add(proj);
            IntVector3 CBp = CubeBranch.GetCBPos(proj.trans.position);
            foreach (CubeBranch CBc in CubeBranches)
            {
                if (CBc.WorldPosition == CBp)
                {
                    CBc.Add(proj);
                    return;
                }
            }
            if (CubeBranches.Count > 200)
            {
                Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max");
                return;
            }
            CubeBranch CB = new CubeBranch();
            CB.WorldPosition = CBp;
            CB.tree = this;
            CB.Add(proj);
            CubeBranches.Add(CB);
        }
        public void ManageCubeBranch(Projectile proj, IntVector3 pos)
        {
            if (!Projectiles.Contains(proj))
            {
                Debug.Log("RandomAdditions: ProjectileCubetree(ManageCubeBranch) - invalid call");
                return;
            }
            foreach (CubeBranch CBc in CubeBranches)
            {
                if (CBc.WorldPosition == pos)
                {
                    CBc.Add(proj);
                    return;
                }
            }
            if (CubeBranches.Count > 200)
            {
                Debug.Log("RandomAdditions: ProjectileCubetree(ManageCubeBranch) - exceeded max");
                return;
            }
            CubeBranch CB = new CubeBranch();
            CB.WorldPosition = pos;
            CB.tree = this;
            CB.Add(proj);
            CubeBranches.Add(CB);
        }
        public void UpdatePos()
        {
            if (updatedThisFrame)
                return;
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                Projectile proj = Projectiles.ElementAt(step);
                if (proj.IsNull())
                {
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                else if (proj.Shooter.IsNull())
                {
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                step++;
            }

            count = CubeBranches.Count;
            for (int step = 0; step < count; )
            {
                if (!CubeBranches.ElementAt(step).UpdateCubeBranch())
                {
                    CubeBranches.RemoveAt(step);
                    count--;
                    continue;
                }
                step++;
            }
            updatedThisFrame = true;
        }
        public void UpdateWorldPos(IntVector3 move)
        {
        }

        /// <summary>
        /// Returns RAW UNSORTED information!
        /// </summary>
        /// <param name="position">Position to search around</param>
        /// <param name="range">The range</param>
        /// <param name="projectiles">The projectiles it can reach</param>
        /// <returns></returns>
        public bool NavigateOctree(Vector3 position, float range, out List<Projectile> projectiles)
        {
            UpdatePos();
            projectiles = new List<Projectile>();
            float CubeHalf = CubeSize / 2;
            float rangeEdge = range + CubeHalf;
            int count = CubeBranches.Count;
            Vector3 boundsMin = new Vector3(position.x - rangeEdge, position.y - rangeEdge, position.z - rangeEdge);
            Vector3 boundsMax = new Vector3(position.x + rangeEdge, position.y + rangeEdge, position.z + rangeEdge);
            for (int step = 0; step < count;)
            {
                CubeBranch CubeB = CubeBranches.ElementAt(step);
                try
                {
                    if (!NotWithinBox(CubeB.WorldPosition, boundsMin, boundsMax))
                    {
                        if (CubeB.GetProjectiles(out List<Projectile> proj))
                            projectiles.AddRange(proj);
                        else
                        {
                            count--;
                            continue;
                        }
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: GetClosestProjectile - error");
                }
                step++;
            }
            return projectiles.Count > 0;
        }

        public bool NotWithinBox(Vector3 pos, Vector3 BoundsMin, Vector3 BoundsMax)
        {
            return pos.x > BoundsMax.x || pos.y > BoundsMax.y || pos.z > BoundsMax.z ||
                pos.x < BoundsMin.x || pos.y < BoundsMin.y || pos.z < BoundsMin.z;
        }

        internal class CubeBranch
        {
            internal IntVector3 WorldPosition;
            internal ProjectileCubetree tree;
            internal List<Projectile> Projectiles = new List<Projectile>();

            public static IntVector3 GetCBPos(Vector3 pos)
            {
                return new IntVector3((int)(pos.x / CubeSize) * CubeSize, (int)(pos.y / CubeSize) * CubeSize, (int)(pos.z / CubeSize) * CubeSize);
            }
            public bool GetProjectiles(out List<Projectile> projO)
            {
                projO = new List<Projectile>();
                int count = Projectiles.Count;
                for (int step = 0; step < count; )
                {
                    Projectile proj = Projectiles.ElementAt(step);
                    if (!(bool)proj.rbody)
                    {
                        Debug.Log("RandomAdditions: CubeBranch(GetProjectiles) - error - RBODY is NULL");
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    if (!(bool)proj.Shooter)
                    {
                        //Debug.Log("RandomAdditions:  CubeBranch(GetProjectiles) - Shooter null");
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    projO.Add(proj);
                    step++;
                }
                if (Projectiles.Count == 0)
                {
                    //Debug.Log("RandomAdditions:  CubeBranch(GetProjectiles) - EMPTY");
                    tree.CubeBranches.Remove(this);
                    return false;
                }
                return true;
            }
            public void Add(Projectile proj)
            {
                if (Projectiles.Count > 2000)
                {
                    Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max projectiles");
                    return;
                }
                if (!Projectiles.Contains(proj))
                    Projectiles.Add(proj);
            }
            public bool Remove(Projectile proj)
            {
                Projectiles.Remove(proj);
                if (Projectiles.Count == 0)
                    return true;
                return false;
            }
            internal bool UpdateCubeBranch()
            {
                int count = Projectiles.Count;
                for (int step = 0; step < count; )
                {
                    Projectile proj = Projectiles.ElementAt(step);
                    if (proj.IsNull())
                    {
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    else if (proj.Shooter.IsNull())
                    {
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    IntVector3 pos = GetCBPos(proj.trans.position);
                    if (WorldPosition != pos)
                    {
                        tree.ManageCubeBranch(proj, pos);
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    step++;
                }
                if (Projectiles.Count == 0)
                    return false;
                return true;
            }
        }
    }

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
                if (!(bool)proj.rbody)
                {
                    Debug.Log("RandomAdditions: OctreeProj(NavigateOctree) - error - RBODY is NULL");
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
                float dist = (proj.trans.position - position).sqrMagnitude;
                if (dist <= rangeSqr)
                {
                    //Rigidbody rbodyC = proj.rbody;
                    projectiles.Add(new KeyValuePair<float, Projectile>(dist, proj));
                }
                step++;
            }
            if (Projectiles.Count == 0)
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
                if (pair.Key.NotWithinBox(proj.trans.position))
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
                    Debug.Log("RandomAdditions: OctreeBranch(NavigateOctant) - Trunk HAS NO BRANCHES OR PROJECTILES!!!");
                    return false;
                }
                if (NotWithinBoxInv(posMin, posMax))
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
                    Debug.Log("RandomAdditions: OctreeBranch(NaviOctant) - Illegal call");
                    return 0;
                }
                if (NotWithinBoxInv(posMin, posMax))
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
                    Debug.Log("RandomAdditions: OctreeBranch(RemoveProj) - Remove request failed " + StackTraceUtility.ExtractStackTrace());
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
                        Debug.Log("RandomAdditions: OctreeProj(ExtendBranches)! - error " + e);
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
                    Debug.Log("RandomAdditions: OctreeBranch(ExtendBranch) - Called when branch already exists");
                }
                branches[index] = new OctreeBranch(main, this, scale / 2, index);
                OctreeBranch bran = branches[index].AddProjectile(toAdd);
                main.Reassign(toAdd, bran);
                return bran;
            }

            // Utilities
            public bool NotWithinBox(Vector3 pos)
            {
                return pos.x > BoundsMax.x || pos.y > BoundsMax.y || pos.z > BoundsMax.z ||
                    pos.x < BoundsMin.x || pos.y < BoundsMin.y || pos.z < BoundsMin.z;
            }
            private bool NotWithinBoxInv(Vector3 posMin, Vector3 posMax)
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
