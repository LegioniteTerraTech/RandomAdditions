using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    public class ProjectileCubeArray
    {   // Dirty cheap octree that's not an octree but a coordinate
        internal const int CubeSize = 32;
        //internal bool useCheap = false;
        internal List<KeyValuePair<Projectile, CubeBranch>> Projectiles = new List<KeyValuePair<Projectile, CubeBranch>>();
        internal List<CubeBranch> CubeBranches = new List<CubeBranch>();
        private bool updatedThisFrame = false;
        private bool bloated = false;

        private static int MaxProjectiles = 5000;
        private static int MaxCubeBranches = 250;

        public void PostFramePrep()
        {
            updatedThisFrame = false;
        }
        public void PurgeAll()
        {
            Projectiles.Clear();
            CubeBranches.Clear();
        }
        /// <summary>
        /// Does only half
        /// </summary>
        public void PruneHALFCubeBranches()
        {
            bloated = true;
            for (int count = CubeBranches.Count / 2; 0 < count; count--)
            {
                CubeBranch CB = CubeBranches.ElementAt(0);
                if (CB != null)
                {
                    foreach (Projectile proj in CB.Projectiles)
                    {
                        int index = Projectiles.FindIndex(delegate (KeyValuePair<Projectile, CubeBranch> cand) { return cand.Key == proj; });
                        if (index != -1)
                        {
                            Projectiles.RemoveAt(index);
                        }
                    }
                }
                CubeBranches.RemoveAt(0);
            }
            bloated = false;
        }
        public void PruneALLCubeBranches()
        {
            bloated = true;
            for (int count = CubeBranches.Count; 0 < count; count--)
            {
                CubeBranches.RemoveAt(0);
            }
            bloated = false;
        }
        public bool Remove(Projectile proj)
        {
            //UpdatePos();
            if (proj.IsNull())
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree(Remove) - Was told to remove NULL");
                return false;
            }
            int index = Projectiles.FindIndex(delegate (KeyValuePair<Projectile, CubeBranch> cand) { return cand.Key == proj; });
            if (index == -1)
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree(Remove) - Was told to remove ID: " + proj.ShortlivedUID + ", not in the list?!? " + StackTraceUtility.ExtractStackTrace());
                //PurgeAll();
                return false;
            }
            KeyValuePair<Projectile, CubeBranch> pair = Projectiles[index];
            Projectiles.RemoveAt(index);
            if (!CubeBranches.Remove(pair.Value))
                Debug.Log("RandomAdditions: ProjectileCubetree(Remove) - Projectile was removed from list but not from cube branches " + StackTraceUtility.ExtractStackTrace());
            else
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree(Remove) - Projectile was removed successfully ID: " + proj.ShortlivedUID + ", " + StackTraceUtility.ExtractStackTrace());
                return true;
            }
            return false;
        }
        private bool Remove(KeyValuePair<Projectile, CubeBranch> proj)
        {
            //UpdatePos();
            if (proj.Key.IsNull())
            {
                Debug.Log("RandomAdditions: ProjectileCubetree(PAIR) - Was told to remove NULL");
                return false;
            }
            int index = Projectiles.IndexOf(proj);
            if (index == -1)
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree(PAIR) - Was told to remove something not in the list?!? " + StackTraceUtility.ExtractStackTrace());
                //PurgeAll();
                return false;
            }
            Projectiles.RemoveAt(index);
            if (!CubeBranches.Remove(proj.Value))
                Debug.Log("RandomAdditions: ProjectileCubetree(PAIR) - Projectile was removed from list but not from cube branches " + StackTraceUtility.ExtractStackTrace());
            else
                return true;
            return false;
        }
        public void Add(Projectile proj)
        {
            if (Projectiles.Exists(delegate (KeyValuePair<Projectile, CubeBranch> cand) { return cand.Key == proj; }))
                return;
            if (Projectiles.Count > MaxProjectiles)
            {
                Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max projectiles, pruning");

                // Prune some
                for (int count = Projectiles.Count / 2; 0 < count; count--)
                {
                    Remove(Projectiles.ElementAt(0));
                }
                //return;
            }
            IntVector3 CBp = CubeBranch.ToCBPosition(proj.rbody.position);
            foreach (CubeBranch CBc in CubeBranches)
            {
                if (CBc.CBPosition == CBp)
                {
                    CBc.Add(proj);
                    break;
                }
            }
            Projectiles.Add(new KeyValuePair<Projectile, CubeBranch>(proj, AddCubeBranch(proj, CBp)));
        }
        internal CubeBranch ManageCubeBranch(Projectile proj, IntVector3 pos)
        {
            if (!Projectiles.Exists(delegate (KeyValuePair<Projectile, CubeBranch> cand) { return cand.Key == proj; }))
            {
                //Debug.Log("RandomAdditions: ProjectileCubetree(ManageCubeBranch) - invalid call");
                return null;
            }
            foreach (CubeBranch CBc in CubeBranches)
            {
                if (CBc.CBPosition == pos)
                {
                    //Debug.Log("RandomAdditions: ProjectileCubetree(ManageCubeBranch) - Migrating projectile to existing " + pos);
                    CBc.Add(proj);
                    return CBc;
                }
            }
            //Debug.Log("RandomAdditions: ProjectileCubetree(ManageCubeBranch) - Migrating projectile to " + pos);
            return AddCubeBranch(proj, pos);
        }
        private CubeBranch AddCubeBranch(Projectile proj, IntVector3 pos)
        {
            if (CubeBranches.Count > MaxCubeBranches)
            {
                Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max, pruning half");
                PruneHALFCubeBranches();
                return null;
            }
            CubeBranch CB = new CubeBranch();
            CB.CBPosition = pos;
            CB.tree = this;
            CB.Add(proj);
            //Debug.Log("RandomAdditions: ProjectileCubetree - New branch at " + pos + " in relation to projectile " + proj.rbody.position);
            CubeBranches.Add(CB);
            return CB;
        }
        public void UpdatePos()
        {
            if (updatedThisFrame)
                return;
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                KeyValuePair<Projectile, CubeBranch> proj = Projectiles.ElementAt(step);

                if (!(bool)proj.Key?.rbody)
                {
                    if (proj.Value.Remove(proj.Key))
                        CubeBranches.Remove(proj.Value);
                    //Debug.Log("RandomAdditions: UpdatePos - ID: " + proj.Key.ShortlivedUID + " ProjectileRemoveReason: Null PairEntry/Rbody");
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                else if (!(bool)proj.Key?.Shooter)
                {
                    if (proj.Value.Remove(proj.Key))
                        CubeBranches.Remove(proj.Value);
                    //Debug.Log("RandomAdditions: UpdatePos - ID: " + proj.Key.ShortlivedUID + " ProjectileRemoveReason: Null Shooter");
                    Projectiles.RemoveAt(step);
                    count--;
                    continue;
                }
                else
                {
                    if (!CubeBranches.ElementAt(step).UpdateCubeBranch(proj.Key, out CubeBranch newCB))
                    {
                        CubeBranches.RemoveAt(step);
                        count--;
                    }
                    if (newCB != null)
                    {
                        //Debug.Log("RandomAdditions: UpdateCubeBranch - Migrated Projectile ID: " + proj.Key.ShortlivedUID + ".");
                        Projectiles.RemoveAt(step);
                        Projectiles.Insert(step, new KeyValuePair<Projectile, CubeBranch>(proj.Key, newCB));
                    }
                }
                step++;
            }
            updatedThisFrame = true;
        }
        public void UpdateWorldPos(IntVector3 move)
        {
            PruneALLCubeBranches();
            List<Projectile> projTemp = new List<Projectile>();

            foreach (KeyValuePair<Projectile, CubeBranch> proj in Projectiles)
                projTemp.Add(proj.Key);

            foreach (Projectile proj in projTemp)
                Add(proj);
        }

        private List<Projectile> projsCacheSend = new List<Projectile>();
        /// <summary>
        /// Returns RAW UNSORTED information!
        /// </summary>
        /// <param name="position">Position to search around</param>
        /// <param name="range">The range</param>
        /// <param name="projectiles">The projectiles it can reach</param>
        /// <returns></returns>
        public bool NavigateOctree(Vector3 ScenePos, float range, out List<Projectile> projectiles)
        {
            UpdatePos();
            projsCacheSend.Clear();
            //float CubeHalf = CubeSize;
            IntVector3 scenePos = new IntVector3(ScenePos / CubeSize);
            int rangeEdge = (int)(range / CubeSize) + 2;
            IntVector3 boundsMin = new IntVector3(scenePos.x - rangeEdge, scenePos.y - rangeEdge, scenePos.z - rangeEdge);
            IntVector3 boundsMax = new IntVector3(scenePos.x + rangeEdge, scenePos.y + rangeEdge, scenePos.z + rangeEdge);
            /*
            Vector3 Syncron = new Vector3(0.75f, 0.25f, 0.5f);
            Debug.Log("RandomAdditions: NavigateOctree - Init search at position " + scenePos);
            IntVector3 posCheck = CubeBranch.ToCBPosition(Syncron);
            Debug.Log("RandomAdditions: NavigateOctree - Testing.. " + Syncron + " | " + posCheck + " | " + CubeBranch.FromCBPosition(Syncron));
            */
            //Debug.Log("RandomAdditions: NavigateOctree - Bounds are " + boundsMin + " | " + boundsMax);

            int count = CubeBranches.Count;
            for (int step = 0; step < count;)
            {
                CubeBranch CubeB = CubeBranches.ElementAt(step);
                try
                {
                    bool withinCube = !NotWithinCube(CubeB.CBPosition, boundsMin, boundsMax);

                    //Debug.Log("RandomAdditions: NavigateOctree - searching cube at " + CubeB.CBPosition + " position " + CubeB.GetCBPosition() + " is within search cube " + withinCube);
                    if (withinCube)
                    {
                        if (CubeB.GetProjectiles(out List<Projectile> proj))
                            projsCacheSend.AddRange(proj);
                        else
                        {
                            count--;
                            continue;
                        }
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: NavigateOctree - error");
                }
                step++;
            }
            projectiles = projsCacheSend;
            // Leave for checking sig changes
            /*
            Debug.Log("RandomAdditions: NavigateOctree - Projectiles found " + projsCacheSend.Count + " | Projectiles active: " + Projectiles.Count);

            Debug.Log("RandomAdditions: NavigateOctree - CubeBranches " + CubeBranches.Count);
            if (CompareDistancesSLOW(scenePos, range, out Projectile pro, out float distF))
            {
                //foreach (CubeBranch CB in CubeBranches)
                //{
                //    Debug.Log("RandomAdditions: NavigateOctree - CB " + CB.GetCBPosition);
                //}
                bool worked = projsCacheSend.Contains(pro);
                Debug.Log("RandomAdditions: NavigateOctree - There is a projectile obviously in range " + pro.rbody.position + " and it's distance is " + distF);
                Debug.Log("RandomAdditions: NavigateOctree - Did the tree find it? Answer is: " + worked);
                if (!worked)
                {
                }
            }*/

            return projsCacheSend.Count > 0;
        }
        public bool CompareDistancesSLOW(Vector3 scenePos, float range, out Projectile proj, out float distF)
        {
            proj = null;
            distF = 0;
            float bestDistSq = range * range;
            int count = Projectiles.Count;
            for (int step = 0; step < count;)
            {
                KeyValuePair<Projectile, CubeBranch> projC = Projectiles.ElementAt(step);
                if (!(bool)projC.Key?.Shooter || !projC.Key?.rbody)
                {
                    //Projectiles.RemoveAt(step);
                    //count--;
                    step++;
                    continue;
                }
                float dist = (projC.Key.rbody.position - scenePos).sqrMagnitude;
                if (bestDistSq > dist)
                {
                    proj = projC.Key;
                    bestDistSq = dist;
                }
                step++;
            }
            if (!proj)
                return false;
            distF = Mathf.Sqrt(bestDistSq);
            return true;
        }

        /// <summary>
        /// Handle in CB POSITION
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="BoundsMin"></param>
        /// <param name="BoundsMax"></param>
        /// <returns></returns>
        public bool NotWithinCube(IntVector3 pos, IntVector3 BoundsMin, IntVector3 BoundsMax)
        {
            return pos.x > BoundsMax.x || pos.y > BoundsMax.y || pos.z > BoundsMax.z ||
                pos.x < BoundsMin.x || pos.y < BoundsMin.y || pos.z < BoundsMin.z;
        }

        internal class CubeBranch
        {
            internal IntVector3 CBPosition;
            internal ProjectileCubeArray tree;
            internal List<Projectile> Projectiles = new List<Projectile>();

            public Vector3 GetCBPosition()
            {
                return new Vector3(CBPosition.x * CubeSize, CBPosition.y * CubeSize, CBPosition.z * CubeSize);
            }
            public static IntVector3 ToCBPosition(Vector3 pos)
            {
                return new IntVector3(pos / CubeSize);
            }
            public static Vector3 FromCBPosition(IntVector3 pos)
            {
                return new Vector3(pos.x * CubeSize, pos.y * CubeSize, pos.z * CubeSize);
            }
            public bool GetProjectiles(out List<Projectile> projO)
            {
                projO = new List<Projectile>();
                int count = Projectiles.Count;
                for (int step = 0; step < count;)
                {
                    Projectile proj = Projectiles.ElementAt(step);
                    if (!(bool)proj?.rbody)
                    {
                        Debug.Log("RandomAdditions: CubeBranch(GetProjectiles) - error - RBODY is NULL");
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    if (!(bool)proj.Shooter)
                    {
                        //Debug.Log("RandomAdditions: CubeBranch(GetProjectiles) - Shooter null");
                        Projectiles.RemoveAt(step);
                        count--;
                        continue;
                    }
                    projO.Add(proj);
                    step++;
                }
                if (Projectiles.Count() == 0)
                {
                    //Debug.Log("RandomAdditions:  CubeBranch(GetProjectiles) - EMPTY");
                    tree.CubeBranches.Remove(this);
                    return false;
                }
                return true;
            }
            public void Add(Projectile proj)
            {
                if (Projectiles.Count > MaxProjectiles)
                {
                    Debug.Log("RandomAdditions: ProjectileCubetree - exceeded max projectiles, pruning");

                    // Prune some
                    for (int count = Projectiles.Count / 2; 0 < count; count--)
                    {
                        Remove(Projectiles.ElementAt(0));
                    }
                    //return;
                }
                if (!Projectiles.Contains(proj))
                    Projectiles.Add(proj);
            }
            public bool Remove(Projectile proj)
            {
                if (Projectiles.Remove(proj))
                {
                    if (Projectiles.Count() == 0)
                        return true;
                }
                else
                    Debug.Log("RandomAdditions: UpdatePos - Projectile could not be found in CubeTree!  ID: " + proj.ShortlivedUID);
                return false;
            }
            /// <summary>
            /// Returns false when the cube branche should be removed (no projectiles)
            /// </summary>
            /// <param name="proj"></param>
            /// <param name="newCB"></param>
            /// <returns></returns>
            internal bool UpdateCubeBranch(Projectile proj, out CubeBranch newCB)
            {
                newCB = null;
                if (!proj?.rbody)
                {
                    Debug.Log("RandomAdditions: UpdateCubeBranch - Removed Projectile?  This should have been checked beforehand.");
                    Projectiles.Remove(proj);
                }
                else if (proj.Shooter.IsNull())
                {
                    Debug.Log("RandomAdditions: UpdateCubeBranch(NULL SHOOTER) - Removed Projectile?  This should have been checked beforehand.");
                    Projectiles.Remove(proj);
                }
                else
                {
                    IntVector3 pos = ToCBPosition(proj.rbody.position);
                    if (CBPosition != pos)
                    {
                        newCB = tree.ManageCubeBranch(proj, pos);
                        Projectiles.Remove(proj);
                    }
                }
                if (Projectiles.Count() == 0)
                    return false;
                return true;
            }
        }
    }

}
