using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    public class TankDistraction : MonoBehaviour
    {
        public static bool forceDisable = false;
        private Tank tank;
        private MirageType type = MirageType.Circle;
        private List<ModuleMirage> ModuleMirages = new List<ModuleMirage>();
        internal List<MirageTank> Mirages = new List<MirageTank>();
        private EnergyRegulator reg;

        // GLOBAL Stats
        private const int SmallTechMaxVol = 48;
        private const int MediumTechMaxVol = 128;
        private const float DispersionMulti = 0.075f;
        private const float BaseMirageLifetime = 24f;

        // Local Stats
        private float LastDistractTime = 0;
        private float AllPower = 0;
        private int Strength = 0;
        private int Strain = 0;
        private int MirageCount = 0;
        private float Potentency = 0;
        internal float Dispersion = 0;
        private bool dirty = false;

        public static void HandleAddition(Tank tank, ModuleMirage mirage)
        {
            if (forceDisable)
                return;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction(HandleAddition) - TANK IS NULL");
                return;
            }
            var dis = tank.GetComponent<TankDistraction>();
            if (!(bool)dis)
            {
                dis = tank.gameObject.AddComponent<TankDistraction>();
                dis.tank = tank;
                dis.reg = tank.EnergyRegulator;
            }

            if (!dis.ModuleMirages.Contains(mirage))
            {
                dis.ModuleMirages.Add(mirage);
                dis.dirty = true;
            }
            else
                DebugRandAddi.Log("RandomAdditions: TankDistraction - ModuleMirage of " + mirage.name + " was already added to " + tank.name + " but an add request was given?!?");
            mirage.distraction = dis;
        }
        public static void HandleRemoval(Tank tank, ModuleMirage mirage)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction(HandleRemoval) - TANK IS NULL");
                return;
            }

            var dis = tank.GetComponent<TankDistraction>();
            if (!(bool)dis)
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction - Got request to remove for tech " + tank.name + " but there's no TankDistraction assigned?!?");
                return;
            }
            if (!dis.ModuleMirages.Remove(mirage))
                DebugRandAddi.Log("RandomAdditions: TankDistraction - ModuleMirage of " + mirage.name + " requested removal from " + tank.name + " but no such ModuleMirage is assigned.");
            else
            {
                dis.dirty = true;
            }
            mirage.distraction = null;

            if (dis.ModuleMirages.Count() == 0)
            {
                foreach (var item in dis.Mirages)
                {
                    item.Destroy();
                }
                dis.Mirages.Clear();
                Destroy(dis);
            }
        }

        private void Update()
        {
            if (ManPauseGame.inst.IsPaused)
                return;
            if (dirty)
            {
                RecalcStats();
            }
            if (ManWorld.inst.GetTerrainHeight(tank.WorldCenterOfMass, out float height))
            {
                float sped;
                float spedRaw;
                bool high = tank.WorldCenterOfMass.y - height > 48;
                if (tank.rbody)
                {
                    spedRaw = tank.rbody.velocity.magnitude;
                    sped = spedRaw * Time.deltaTime;
                }
                else
                {
                    spedRaw = 0;
                    sped = 0;
                }

                foreach (var item in Mirages)
                {
                    try
                    {
                        item.UpdateThis(high, height, sped, spedRaw);
                    }
                    catch { }
                }
            }
        }


        public Vector3 GetPosDistract(Vector3 pos)
        {
            MirageTank lastDistraction = null;
            float best = float.MaxValue;
            foreach (var item in Mirages)
            {
                if (item.GetPosition(out Vector3 dis))
                {
                    float dist = (dis - pos).sqrMagnitude;
                    if (dist < best)
                    {
                        best = dist;
                        lastDistraction = item;
                    }
                }
            }
            if (lastDistraction && lastDistraction.GetPosition(out Vector3 dis2))
                return dis2;
            return pos;
        }


        private void RecalcStats()
        {
            foreach (var item in Mirages)
            {
                item.Destroy();
            }
            Mirages.Clear();
            Strength = 0;
            Strain = 0;
            MirageCount = 0;

            Dictionary<MirageType, int> types = new Dictionary<MirageType, int>();
            foreach (MirageType item in Enum.GetValues(typeof(MirageType)))
            {
                int count = 0;
                foreach (var item2 in ModuleMirages)
                {
                    if (item2.MirageType == item)
                        count++;
                }
                types.Add(item, count);
            }
            type = types.OrderByDescending(x => x.Value).First().Key;

            foreach (var item in ModuleMirages)
            {
                Strength += item.block.filledCells.Length;
                AllPower += item.MiragePower;
            }
            foreach (var item in tank.blockman.IterateBlocks())
            {
                Strain += item.filledCells.Length;
            }
            Potentency = (float)Strength / (float)Strain;
            Dispersion = (1 - Potentency) * DispersionMulti;
            MirageCount = (int)(AllPower / Strain) + 1;

            int MaxMirages = 8;
            if (Strain > MediumTechMaxVol)
                MaxMirages = 2;
            else if (Strain > SmallTechMaxVol)
                MaxMirages = 4;

            MirageCount = Mathf.Min(MirageCount, MaxMirages);
            for (int step = 0; step < MirageCount; step++)
            {
                Mirages.Add(MakeMirage());
            }
            dirty = false;
        }

        internal void InitMirage(MirageTank MT, Vector3 offset)
        {
            MT.Init(tank, this, offset, Mathf.Max(tank.blockman.blockCount, 2) / 2);
        }

        internal Vector3 GetOffset(int positionIndex)
        {
            Vector3 offset;
            float radMag = (tank.blockBounds.extents.magnitude * 2.5f) + 2;
            switch (type)
            {
                case MirageType.XForm:
                    switch (positionIndex % 4)
                    {
                        case 0:
                            offset = new Vector3(-0.7071f, 0.7071f, 0.7071f) * (1 + (Mirages.Count / 4));
                            break;
                        case 1:
                            offset = new Vector3(0.7071f, 0.7071f, 0.7071f) * (1 + (Mirages.Count / 4));
                            break;
                        case 2:
                            offset = new Vector3(-0.7071f, -0.7071f, -0.7071f) * (1 + (Mirages.Count / 4));
                            break;
                        default:
                            offset = new Vector3(0.7071f, -0.7071f, -0.7071f) * (1 + (Mirages.Count / 4));
                            break;
                    }
                    break;
                case MirageType.Chevron:
                    switch (positionIndex % 2)
                    {
                        case 0:
                            offset = new Vector3(-0.7071f, 0, -0.7071f) * (1 + (Mirages.Count / 2));
                            break;
                        default:
                            offset = new Vector3(0.7071f, 0, -0.7071f) * (1 + (Mirages.Count / 2));
                            break;
                    }
                    break;
                case MirageType.ChevronLead:
                    switch (positionIndex % 2)
                    {
                        case 0:
                            offset = new Vector3(-0.7071f, 0, -0.7071f) * (1 + (Mirages.Count / 2));
                            offset.x += 0.2071f;
                            break;
                        default:
                            offset = new Vector3(0.7071f, 0, -0.7071f) * (1 + (Mirages.Count / 2));
                            offset.x += -0.2071f;
                            break;
                    }
                    offset.z += 1.6f;
                    break;
                case MirageType.ChevronInverse:
                    switch (positionIndex % 2)
                    {
                        case 0:
                            offset = new Vector3(-0.7071f, 0, 0.7071f) * (1 + (Mirages.Count / 2));
                            break;
                        default:
                            offset = new Vector3(0.7071f, 0, 0.7071f) * (1 + (Mirages.Count / 2));
                            break;
                    }
                    break;
                case MirageType.LineLead:
                    switch (positionIndex % 2)
                    {
                        case 0:
                            offset = Vector3.left * (1 + (Mirages.Count / 2));
                            offset.x += 0.5f;
                            break;
                        default:
                            offset = Vector3.right * (1 + (Mirages.Count / 2));
                            offset.x += -0.5f;
                            break;
                    }
                    offset.z += 0.7071f;
                    break;
                case MirageType.Line:
                    switch (positionIndex % 2)
                    {
                        case 0:
                            offset = Vector3.left * (1 + (Mirages.Count / 2));
                            break;
                        default:
                            offset = Vector3.right * (1 + (Mirages.Count / 2));
                            break;
                    }
                    break;
                default:
                    switch (positionIndex % 4)
                    {
                        case 0:
                            offset = Vector3.left * (1 + (Mirages.Count / 4));
                            break;
                        case 1:
                            offset = Vector3.right * (1 + (Mirages.Count / 4));
                            break;
                        case 2:
                            offset = Vector3.forward * (1 + (Mirages.Count / 4));
                            break;
                        default:
                            offset = Vector3.back * (1 + (Mirages.Count / 4));
                            break;
                    }
                    break;
            }
            return offset * radMag;
        }

        private MirageTank MakeMirage()
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction(HandleRemoval) - TANK IS NULL");
                return null;
            }
            Vector3 offset = GetOffset(Mirages.Count);

            GameObject GO = MakeCopyMirageTank(tank.gameObject, tank.trans.position + offset, tank.trans.rotation);
            MirageTank MT = GO.AddComponent<MirageTank>();
            InitMirage(MT, offset);
            MT.transform.localScale = Vector3.zero;
            return MT;
        }


        // MIRAGE MAKER
        private GameObject MakeCopyMirageTank(GameObject GO, Vector3 worldPos, Quaternion rot)
        {
            GameObject GOo = Instantiate(new GameObject(GO.name + "_MirageClone"), worldPos, rot, null);
            RecursiveCopyStart(GO.transform, GOo);
            return GOo;
        }
        internal void RecursiveCopyStart(Transform transOG, GameObject toAddTo)
        {
            int children = transOG.childCount;
            Vector3 centerOffset = tank.CenterOfMass;
            for (int step = 0; step < children; step++)
            {
                Transform child = transOG.GetChild(step);
                if (child.GetComponent<HoverBeam>())
                    continue;
                GameObject newChild = Instantiate(new GameObject(child.name + "_MCOPY"), null);
                Transform newTrans = newChild.transform;
                newTrans.parent = toAddTo.transform;
                newTrans.localPosition = child.localPosition - centerOffset;
                newTrans.localRotation = child.localRotation;
                newTrans.localScale = child.localScale;
                MakeCopyMeshAll(child, newChild);
                MakeCopyMiragePart(child.gameObject, newChild);
                RecursiveCopySimple(child, newChild);
                MakeCopyMiragePartPost(child.gameObject, newChild);
            }
        }
        private void RecursiveCopySimple(Transform transOG, GameObject toAddTo)
        {
            int children = transOG.childCount;
            for (int step = 0; step < children; step++)
            {
                Transform child = transOG.GetChild(step);
                if (child.GetComponent<HoverBeam>())
                    continue;
                GameObject newChild = Instantiate(new GameObject(child.name + "_MCOPY"), null);
                Transform newTrans = newChild.transform;
                newTrans.parent = toAddTo.transform;
                newTrans.localPosition = child.localPosition;
                newTrans.localRotation = child.localRotation;
                newTrans.localScale = child.localScale;
                MakeCopyMeshAll(child, newChild);
                MakeCopyMiragePart(child.gameObject, newChild);
                RecursiveCopySimple(child, newChild);
                MakeCopyMiragePartPost(child.gameObject, newChild);
            }
        }

        private bool MakeCopyMeshAll(Transform child, GameObject toAddTo)
        {
            MeshFilter MF = child.GetComponent<MeshFilter>();
            if (MF == null || !MF.sharedMesh)
                return false;
            MeshRenderer MR = child.GetComponent<MeshRenderer>();
            if (MR == null)
                return false;
            MeshFilter added = toAddTo.AddComponent<MeshFilter>();
            added.sharedMesh = MF.sharedMesh;
            MeshRenderer added2 = toAddTo.AddComponent<MeshRenderer>();
            added2.sharedMaterial = MR.sharedMaterial;
            added2.rendererPriority = MR.rendererPriority;
            added2.sortingOrder = MR.sortingOrder;
            return true;
        }

        private void MakeCopyMiragePart(GameObject OG, GameObject toAddTo)
        {
            if (OG == null)
                return;

            var part = OG.GetComponent<ParticleSystem>();
            var trail = OG.GetComponent<TrailRenderer>();
            var smoke = OG.GetComponent<SmokeTrail>();
            if (part || trail || smoke)
            {
                GameObject toAddToOld = toAddTo;
                toAddTo = Instantiate(OG, toAddToOld.transform.parent);
                Destroy(toAddToOld);
                int children = toAddTo.transform.childCount;
                List<Transform> toErad = new List<Transform>();
                for (int step = 0; step < children; step++)
                {
                    toErad.Add(toAddTo.transform.GetChild(step));
                }
                toAddTo.transform.DetachChildren();
                try
                {
                    for (int step = 0; step < children; step++)
                    {
                        Destroy(toErad[step].gameObject);
                    }
                }
                catch { }
                toAddTo.transform.localPosition = OG.transform.localPosition;
                toAddTo.transform.localRotation = OG.transform.localRotation;
                toAddTo.transform.localScale = OG.transform.localScale;
                toAddTo.AddComponent<MimicParticles>().Init(part, trail, smoke);
            }
            var barrel = OG.GetComponent<CannonBarrel>();
            if (barrel)
            {
                MimicBarrel MB = toAddTo.AddComponent<MimicBarrel>();
                MB.CB = barrel;
            }
            var gimbal = OG.GetComponent<GimbalAimer>();
            if (gimbal)
            {
                toAddTo.transform.localRotation = Quaternion.identity;
                MimicGimbalAimer MGA = toAddTo.AddComponent<MimicGimbalAimer>();
                MGA.AimRestrictions = gimbal.rotationLimits;
                switch (gimbal.rotationAxis)
                {
                    case GimbalAimer.AxisConstraint.X:
                        MGA.Axis = ExtGimbalAxis.Y;
                        break;
                    case GimbalAimer.AxisConstraint.Y:
                        MGA.Axis = ExtGimbalAxis.X;
                        break;
                    default:
                        MGA.Axis = ExtGimbalAxis.Free;
                        break;
                }
                return;
            }
            var child = OG.GetComponent<ChildModuleWeapon>();
            var wheel = OG.GetComponent<ModuleWheels>();
            var wing = OG.GetComponent<ModuleWing>();
            var drill = OG.GetComponent<ModuleDrill>();
            var boost = OG.GetComponent<ModuleBooster>();
            var shield = OG.GetComponent<ModuleShieldGenerator>();
            if (child || barrel || wheel || wing || drill || boost || shield)
            {
                MimicHierachy MH = toAddTo.AddComponent<MimicHierachy>();
                MH.Init(OG.transform);
            }
        }
        private void MakeCopyMiragePartPost(GameObject OG, GameObject toAddTo)
        {
            if (OG == null)
                return;
            var swap = OG.GetComponent<MaterialSwapper>();
            if (swap)
            {
                Renderer[] matsO = OG.GetComponentsInChildren<Renderer>();
                Renderer[] mats = toAddTo.GetComponentsInChildren<Renderer>();
                if (mats != null && matsO != null && mats.Length > 0 && matsO.Length > 0)
                {
                    MaterialPropertyBlock MPB = new MaterialPropertyBlock();
                    int count = mats.Length;
                    try
                    {
                        matsO[0].GetPropertyBlock(MPB);
                        for (int step = 0; step < count; step++)
                        {
                            mats[step].SetPropertyBlock(MPB);
                        }
                        toAddTo.GetComponent<Renderer>().SetPropertyBlock(MPB);
                    }
                    catch { }
                }
            }
            if (toAddTo.GetComponent<MimicHierachy>())
                return;
            var WeapAim = OG.GetComponent<TargetAimer>();
            if (WeapAim)
            {
                var Weap = OG.GetComponent<ModuleWeapon>();
                var MWG = OG.GetComponent<ModuleWeaponGun>();
                if (Weap && MWG)
                {
                    MimicAimer MA = toAddTo.AddComponent<MimicAimer>();

                    bool seek = MWG.m_SeekingRounds;
                    MA.Init(OG.GetComponent<TankBlock>(), OG.GetComponent<FireData>(), Weap, MWG, Weap.RotateSpeed, seek);
                }
            }
        }


        //--------------------------------------------
        //   The below holds only facade components
        //--------------------------------------------

        internal class MirageTank : MonoBehaviour
        {
            private Tank tech;
            private TankDistraction controller;
            private Transform[] allTrans;
            private MimicAimer[] aimers;
            private MimicHierachy[] mimics;
            private MimicParticles[] parts;
            private Quaternion OGRotation = Quaternion.identity;
            private Vector3 offset = Vector3.one;
            private Vector3 offsetLocal = Vector3.zero;
            private Vector3 offsetRand = Vector3.zero;
            private float lastDistractTime = 0;
            private float lastRandTime = 0;
            private float offsetYVelo = 0;
            private float radius = 0;
            private int accuracyCalc = 1;
            private int animState = 1;


            private float restTime = 0;
            private float restTimeNeeded = 0;
            private bool distractActive => animState == 0;
            private bool distracting = false;

            public void Init(Tank tank, TankDistraction control, Vector3 Offset, int accuracyCalced)
            {
                tech = tank;
                offset = Offset;
                accuracyCalc = accuracyCalced;
                radius = tank.blockBounds.extents.magnitude;
                aimers = gameObject.GetComponentsInChildren<MimicAimer>();
                mimics = gameObject.GetComponentsInChildren<MimicHierachy>();
                parts = gameObject.GetComponentsInChildren<MimicParticles>();
                restTimeNeeded = UnityEngine.Random.Range(0.75f, 1.65f) * BaseMirageLifetime;
                controller = control;
            }

            public bool GetPosition(out Vector3 pos)
            {
                try
                {
                    if (distractActive)
                    {
                        if (lastDistractTime < Time.time)
                        {
                            if (UnityEngine.Random.Range(0, 1f) < controller.Potentency + 0.1f)
                            {
                                distracting = true;
                                pos = transform.position;
                                lastDistractTime = Time.time + 4f;
                                return true;
                            }
                            else
                                distracting = false;
                            lastDistractTime = Time.time + 4f;
                        }
                        else
                        {
                            if (distracting)
                            {
                                pos = transform.position;
                                return true;
                            }
                        }
                    }
                }
                catch { }
                pos = Vector3.zero;
                return false;
            }

            // UPDATE
            public void UpdateThis(bool high, float heightMain, float sped, float rawSped)
            {
                try
                {
                    if (allTrans == null)
                    {
                        Transform[] unfiltered = gameObject.GetComponentsInChildren<Transform>();
                        if (unfiltered == null)
                        {
                            DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank HAS NO CHILDREN!!!");
                            return;
                        }
                        List<Transform> transChecked = new List<Transform>();
                        foreach (Transform trans in unfiltered)
                        {
                            try
                            {
                                if (trans.IsNotNull())
                                    transChecked.Add(trans);
                            }
                            catch { }
                        }
                        if (transChecked.Count == 0)
                        {
                            DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank HAS NO VALID CHILDREN!!!");
                            return;
                        }
                        allTrans = transChecked.ToArray();
                    }
                    switch (animState)
                    {
                        case 1:
                            if (transform.localScale.x.Approximately(1, 0.001f) &&
                                transform.localScale.y.Approximately(1, 0.001f))
                            {
                                transform.localScale = Vector3.one;
                                animState = 0;
                            }
                            else
                            {
                                Grow();
                            }
                            break;
                        case -1:
                            if (transform.localScale.x.Approximately(0, 0.001f))
                            {
                                if (parts != null)
                                {
                                    foreach (var item in parts)
                                    {
                                        item.StopVis();
                                    }
                                }
                                transform.localScale = Vector3.zero;
                                animState = -2;
                            }
                            else
                            {
                                Shrink();
                            }
                            break;
                        case -2:
                            restTime -= Time.deltaTime;
                            if (restTime <= 0)
                            {
                                RefreshVis();
                                animState = 1;
                            }
                            break;
                        default:
                            restTime += Time.deltaTime * (1.1f - controller.Potentency);
                            if (restTime > restTimeNeeded)
                                animState = -1;
                            break;
                    }

                    Vector3 pos;
                    if (high)
                    {
                        if (sped > 0)
                        {
                            if (lastRandTime < Time.time)
                            {
                                offsetRand = UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0f, 2f);
                                lastRandTime = Time.time + UnityEngine.Random.Range(0.25f, 0.75f);
                            }
                            Vector3 cRot = (OGRotation * Vector3.forward).normalized;
                            Vector3 forward = tech.trans.forward;
                            Vector3 Up = tech.trans.up;
                            forward.y = forward.y / 2;
                            forward.Normalize();
                            Up.y = Up.y / 2;
                            Up.Normalize();
                            float spedC = sped * Mathf.Min(1.1f - Vector3.Dot(cRot, forward), 1);
                            float spedD = spedC / 24;
                            offsetLocal.x = Mathf.Lerp(offsetLocal.x, offsetRand.x, 0.05f);
                            offsetLocal.z = Mathf.Lerp(offsetLocal.z, offsetRand.z, 0.05f);
                            offsetLocal.y = Mathf.Lerp(offsetLocal.y, offsetRand.y, 0.05f);
                            OGRotation = Quaternion.RotateTowards(OGRotation, Quaternion.LookRotation(forward.normalized, Up), spedC);
                        }
                        offsetYVelo = 0;
                        pos = tech.WorldCenterOfMass + (OGRotation * offset) + offsetLocal;
                    }
                    else
                    {
                        if (sped > 0)
                        {
                            Vector2 cRot = (OGRotation * Vector3.forward).ToVector2XZ().normalized;
                            Vector2 forwardFlat = tech.trans.forward.ToVector2XZ().normalized;
                            float spedC = sped * Mathf.Min(1.1f - Vector2.Dot(cRot, forwardFlat), 1);
                            OGRotation = Quaternion.RotateTowards(OGRotation, Quaternion.LookRotation(forwardFlat.ToVector3XZ(), Vector3.up), spedC);

                            offsetLocal.x = Mathf.Lerp(offsetLocal.x, 0, 0.1f);
                            offsetLocal.z = Mathf.Lerp(offsetLocal.z, 0, 0.1f);


                            pos = tech.WorldCenterOfMass + (OGRotation * offset);
                            ManWorld.inst.GetTerrainHeight(pos, out float height);
                            float deltaGround = height - heightMain - offset.y;
                            bool rBody = tech.rbody;
                            if (height + radius > transform.position.y)
                            {
                                if (deltaGround > offsetLocal.y || !rBody)
                                {
                                    float pastY = offsetLocal.y;
                                    offsetLocal.y = deltaGround;
                                    offsetYVelo = offsetLocal.y - pastY;
                                }
                                else
                                {
                                    float veloy = tech.rbody.velocity.y;
                                    offsetYVelo = offsetYVelo + (Physics.gravity.y * (tech.GetGravityScale() * Time.deltaTime));
                                    float move = offsetLocal.y + ((offsetYVelo - veloy) * Time.deltaTime);
                                    if (move < deltaGround)
                                    {
                                        offsetYVelo = 0;
                                        offsetLocal.y = deltaGround;
                                    }
                                    else
                                    {
                                        offsetLocal.y = move;
                                    }
                                }
                            }
                            else
                            {
                                if (rBody)
                                {
                                    float veloy = tech.rbody.velocity.y;
                                    offsetYVelo = offsetYVelo + (Physics.gravity.y * (tech.GetGravityScale() * Time.deltaTime));
                                    float move = offsetLocal.y + ((offsetYVelo - veloy) * Time.deltaTime);
                                    if (move < deltaGround)
                                    {
                                        offsetYVelo = 0;
                                        offsetLocal.y = deltaGround;
                                    }
                                    else
                                    {
                                        offsetLocal.y = move;
                                    }
                                }
                                else
                                {
                                    float pastY = offsetLocal.y;
                                    offsetLocal.y = Mathf.Lerp(offsetLocal.y, deltaGround, spedC / 6);
                                    offsetYVelo = offsetLocal.y - pastY;
                                }
                            }
                            pos += offsetLocal;
                        }
                        else
                        {
                            offsetYVelo = 0;
                            pos = tech.WorldCenterOfMass + (OGRotation * offset) + (Vector3.up * offsetLocal.y);
                        }
                    }
                    transform.position = pos;
                    transform.rotation = tech.trans.rotation;

                    if (animState == 0)
                        UpdateDistortion();
                    if (animState != -1)
                        UpdateAllMimics();
                    if (animState == 0)
                    {
                        UpdateAllAimers();
                        UpdateAllEffects(rawSped);
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank.UpdateThis has errored: " + e);
                }
            }

            public void UpdateDistortion()
            {
                try
                {
                    Transform randTran = allTrans.GetRandomEntry();
                    if (randTran == null)
                    {
                        //Debug.Log("RandomAdditions: MirageTank has encountered null transforms, cleaning up...");
                        List<Transform> transChecked = new List<Transform>();
                        foreach (Transform trans in allTrans)
                        {
                            try
                            {
                                if (trans.IsNotNull())
                                    transChecked.Add(trans);
                            }
                            catch { }
                        }
                        if (transChecked.Count == 0)
                        {
                            DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank HAS NO VALID CHILDREN!!!");
                            return;
                        }
                        allTrans = transChecked.ToArray();
                        randTran = allTrans.GetRandomEntry();
                        if (randTran == null)
                        {
                            //Debug.Log("RandomAdditions: MirageTank has encountered stubborn transforms, ignoring...");
                            return;
                        }
                    }
                    randTran.position = randTran.position + (UnityEngine.Random.insideUnitSphere * controller.Dispersion);
                    randTran.rotation = Quaternion.RotateTowards(randTran.rotation, UnityEngine.Random.rotation, controller.Dispersion * 30);
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank.UpdateDistortion has errored: " + e);
                }
            }
            public void UpdateAllMimics()
            {
                try
                {
                    if (mimics != null)
                    {
                        mimics.Shuffle();
                        int steps = accuracyCalc;
                        foreach (var item in mimics)
                        {
                            item.UpdateVis();
                            steps--;
                            if (steps <= 0)
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank.UpdateAllMimics has errored: " + e);
                }
            }
            public void UpdateAllAimers()
            {
                try
                {
                    if (aimers != null)
                    {
                        Visible targ = tech.Vision.GetFirstVisibleTechIsEnemy(tech.Team);
                        foreach (var item in aimers)
                        {
                            item.UpdateVis(targ);
                        }
                        if (tech.control.FireControl)
                        {
                            foreach (var item in aimers)
                            {
                                item.ChainFire();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank.UpdateAllAimers has errored: " + e);
                }
            }
            public void UpdateAllEffects(float rawSped)
            {
                try
                {
                    if (parts != null)
                    {
                        foreach (var item in parts)
                        {
                            item.UpdateVis(rawSped);
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("RandomAdditions: TankDistraction.MirageTank.UpdateAllEffects has errored: " + e);
                }
            }

            // "Animations"
            public void Grow()
            {
                Vector3 scaleOp = transform.localScale;
                if (scaleOp.y > 0.75f || scaleOp.x > 0.25f)
                {
                    if (scaleOp.x > 0.25f)
                    {
                        if (scaleOp.x > 0.75f)
                            scaleOp.y = Mathf.Lerp(scaleOp.y, 1, 0.1f);
                        else
                            scaleOp.y = Mathf.Lerp(scaleOp.y, 0.5f, 0.1f);
                    }
                    else
                        scaleOp.y = Mathf.Lerp(scaleOp.y, 0.25f, 0.05f);

                    scaleOp.x = Mathf.Lerp(scaleOp.x, 1, 0.1f);
                    scaleOp.z = scaleOp.x;
                }
                else
                {
                    scaleOp.y = Mathf.Lerp(scaleOp.y, 0.9f, 0.1f);
                    scaleOp.x = Mathf.Lerp(scaleOp.x, 0.1f, 0.1f);
                    scaleOp.z = scaleOp.x;
                }
                transform.localScale = scaleOp;
            }
            public void Shrink()
            {
                Vector3 scaleOp = transform.localScale;
                if (scaleOp.y < 0.25f || scaleOp.x < 0.9f)
                {
                    if (scaleOp.x < 0.25f)
                    {
                        if (scaleOp.x < 0.1f)
                            scaleOp.y = Mathf.Lerp(scaleOp.y, 0, 0.05f);
                        else
                            scaleOp.y = Mathf.Lerp(scaleOp.y, 0.5f, 0.1f);
                    }
                    else
                        scaleOp.y = Mathf.Lerp(scaleOp.y, 0.9f, 0.05f);

                    scaleOp.x = Mathf.Lerp(scaleOp.x, 0, 0.2f);
                    scaleOp.z = scaleOp.x;
                }
                else
                {
                    scaleOp.x = Mathf.Lerp(scaleOp.x, 0.9f, 0.1f);
                    scaleOp.z = scaleOp.x;
                    scaleOp.y = Mathf.Lerp(scaleOp.y, 0.1f, 0.1f);
                }
                transform.localScale = scaleOp;
            }

            private void RefreshVis()
            {
                DestroyAllChildren();
                controller.RecursiveCopyStart(tech.trans, gameObject);
                controller.InitMirage(this, controller.GetOffset(controller.Mirages.IndexOf(this)));
            }
            private void DestroyAllChildren()
            {
                int children = transform.childCount;
                List<Transform> toErad = new List<Transform>();
                for (int step = 0; step < children; step++)
                {
                    toErad.Add(transform.GetChild(step));
                }
                transform.DetachChildren();
                try
                {
                    for (int step = 0; step < children; step++)
                    {
                        Destroy(toErad[step].gameObject);
                    }
                }
                catch { }
            }


            // Removal
            public void UpdateDetached()
            {
                if (ManWorld.inst.GetTerrainHeight(transform.position, out float height))
                {
                    Vector3 scaleOp = transform.localScale;
                    if (scaleOp.x.Approximately(0, 0.001f))
                    {
                        CancelInvoke();
                        Destroy(gameObject);
                    }
                    else
                    {
                        Shrink();
                    }
                    if (tech)
                    {
                        Vector3 pos = tech.WorldCenterOfMass + (OGRotation * offset);
                        pos.y = transform.position.y;
                        transform.position = pos;
                        transform.rotation = tech.trans.rotation;
                    }
                }
                else
                {
                    CancelInvoke();
                    Destroy(gameObject);
                }
            }
            public void Destroy()
            {
                animState = -1;
                InvokeRepeating("UpdateDetached", 0.001f, Time.deltaTime);
            }
        }

        internal class MimicHierachy : MonoBehaviour
        {
            Transform transToCopy;

            public void Init(Transform trans)
            {
                transToCopy = trans;
            }
            public void UpdateVis()
            {
                try
                {
                    UpdateVisRecursive(transToCopy, transform);
                }
                catch { }
            }
            private static void UpdateVisRecursive(Transform transOG, Transform toUpdate)
            {
                int children = transOG.childCount;
                for (int step = 0; step < children; step++)
                {
                    Transform child = transOG.GetChild(step);
                    Transform newTrans = toUpdate.GetChild(step);
                    newTrans.localPosition = child.localPosition;
                    newTrans.localRotation = child.localRotation;
                    newTrans.localScale = child.localScale;
                    newTrans.gameObject.SetActive(child.gameObject.activeSelf);
                    UpdateVisRecursive(child, newTrans);
                }
            }
        }


        internal class MimicParticles : MonoBehaviour
        {
            ParticleSystem main;
            ParticleSystem local;
            TrailRenderer mainT;
            TrailRenderer localT;
            SmokeTrail mainS;
            SmokeTrail localS;
            float speed = 0;
            bool lastState;
            bool lastStateP;
            private static MethodBase pool = typeof(SmokeTrail).GetMethod("OnPool", BindingFlags.NonPublic | BindingFlags.Instance);

            public void Init(ParticleSystem MAin, TrailRenderer trailer, SmokeTrail smokes)
            {
                main = MAin;
                local = GetComponent<ParticleSystem>();
                mainT = trailer;
                localT = GetComponent<TrailRenderer>();
                mainS = smokes;
                localS = GetComponent<SmokeTrail>();
                if (local)
                {
                    lastState = local.isPlaying;
                    lastStateP = local.isPaused;
                    var c = local.collision;
                    c.enabled = false;
                }
                if (smokes)
                {
                    pool.Invoke(localS, new object[] { });
                    localS.UpdateAlphaFn = mainS.UpdateAlphaFn;
                }
            }
            public void UpdateVis(float sped)
            {
                if (localS)
                {
                    if (localS.enabled != mainS.enabled)
                    {
                        if (mainS.enabled)
                        {
                            localS.enabled = true;
                        }
                        else
                        {
                            localS.Reset();
                            localS.enabled = false;
                        }
                        localS.enabled = mainS.enabled;
                    }
                }
                if (localT)
                {
                    localT.emitting = mainT.emitting;
                }
                if (local)
                {
                    if (lastState != main.isPlaying)
                    {
                        if (main.isPlaying)
                        {
                            local.Play();
                        }
                        else
                        {
                            local.Stop();
                        }
                        lastState = main.isPlaying;
                    }
                    else if (lastStateP != main.isPaused)
                    {
                        if (main.isPaused)
                        {
                            local.Pause();
                        }
                        else
                        {
                            local.Play();
                        }
                        lastStateP = main.isPaused;
                    }
                }
            }
            public void StopVis()
            {
                if (localS)
                {
                    if (localS.enabled)
                    {
                        localS.Reset();
                        localS.enabled = false;
                    }
                }
                if (localT)
                {
                    localT.emitting = false;
                }
                if (local)
                {
                    if (lastState)
                    {
                        local.Stop();
                        lastState = false;
                    }
                }
            }
        }

        internal class MimicBarrel : MonoBehaviour
        {
            Tank tank;
            bool seeking = false;
            MimicAimer MA;
            Transform bulletTrans;
            internal CannonBarrel CB;

            public void Init(MimicAimer MAin, Tank tankIn, bool seek)
            {
                tank = tankIn;
                seeking = seek;
                MA = MAin;

                bulletTrans = KickStart.HeavyObjectSearch(transform, "_bulletSpawn_MCOPY");
                if (!bulletTrans)
                {
                    bulletTrans = KickStart.HeavyObjectSearch(transform, "_spawnBullet_MCOPY");
                    if (!bulletTrans)
                    {
                        bulletTrans = transform;
                    }
                }
            }
            public void TryFire()
            {
                Fire();
            }
            private static MethodBase pool = typeof(Projectile).GetMethod("OnPool", BindingFlags.NonPublic | BindingFlags.Instance);
            private static MethodBase spawn = typeof(Projectile).GetMethod("OnSpawn", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo ded = typeof(Projectile).GetField("m_HasSetCollisionDeathDelay", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo time = typeof(Projectile).GetField("m_DestroyTimeout", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo time2 = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);
            private void Fire()
            {
                WeaponRound WR = MA.fireData.m_BulletPrefab.Spawn(Singleton.dynamicContainer, bulletTrans.position, bulletTrans.rotation);
                WR.Fire(bulletTrans.forward, MA.fireData, MA.MW, tank, seeking, false);
                /*
                WeaponRound WR = MA.fireData.m_BulletPrefab.transform.UnpooledSpawnWithLocalTransform(null, bulletTrans.position, Quaternion.identity).GetComponent<WeaponRound>();
                WR.transform.rotation = bulletTrans.rotation;
                WR.gameObject.SetActive(true);
                var proj = WR.GetComponent<Projectile>();
                pool.Invoke(proj, new object[]{});
                spawn.Invoke(proj, new object[] { });
                ded.SetValue(proj, false);
                time.SetValue(proj, -1);
                TechWeapon.RegisterWeaponRound(WR, int.MinValue);
                InterceptProjectile.deals.SetValue(WR, 0);
                InterceptProjectile.explode.SetValue(proj, null);
                WR.enabled = true;
                proj.enabled = true;
                WR.Fire(bulletTrans.forward, MA.fireData, MA.MW, tank, seeking, false);
                WR.gameObject.SetActive(true);
                Destroy(WR.gameObject, (float)time2.GetValue(proj));
                */
            }
        }

        public class MimicGimbalAimer : ExtGimbal
        {
            private MimicAimer MA;

            internal void Setup(MimicAimer MAin)
            {
                if (MA)
                    return;
                transform.localRotation = Quaternion.identity;
                MA = MAin;
                base.Setup(MA);
                OnPool();
            }


            internal void AimAt(Vector3 worldPos)
            {
                Vector3 directed = transform.parent.InverseTransformPoint(worldPos);
                forwardsAim = directed.normalized;
                UpdateAim(MA.rotSpeed * Time.deltaTime);
            }
            internal bool AimBack()
            {
                forwardsAim = startRotLocal * Vector3.forward;
                UpdateAim(MA.rotSpeed * Time.deltaTime);
                return (transform.localRotation * Vector3.forward).Approximately(forwardsAim, 0.01f);
            }
        }


        internal class MimicAimer : MonoBehaviour, IExtGimbalControl
        {
            TankBlock blockToCopy;
            ModuleWeaponGun weaponHint;
            internal ModuleWeapon MW;
            internal FireData fireData;
            MimicGimbalAimer[] aimers;
            MimicBarrel[] barrels;
            internal float rotSpeed = 0;
            int barrelNum = 0;
            int barrelOn = 0;
            int barrelsFired = 0;
            int burstCount = 0;
            float cooldown = 0;
            bool settled = false;

            public bool Linear()
            {
                return false;
            }

            public void Init(TankBlock block, FireData FD, ModuleWeapon MWin, ModuleWeaponGun hint, float aimSpeed, bool seeking)
            {
                blockToCopy = block;
                rotSpeed = aimSpeed;
                fireData = FD;
                MW = MWin;
                aimers = GetComponentsInChildren<MimicGimbalAimer>();
                barrels = GetComponentsInChildren<MimicBarrel>();
                weaponHint = hint;


                foreach (var item in barrels)
                {
                    item.Init(this, block.tank, seeking);
                }
                barrelNum = barrels.Length;
                foreach (var item in aimers)
                {
                    item.Setup(this);
                }
            }
            public void UpdateVis(Visible target)
            {
                if (cooldown > 0)
                {
                    cooldown -= Time.deltaTime;
                }
                barrelsFired = 0;
                AimHandle(target);
            }
            private void AimHandle(Visible targ)
            {
                if (targ)
                {
                    settled = false;
                    Vector3 aimPosFinal = targ.GetAimPoint(transform.position);
                    foreach (MimicGimbalAimer gimbal in aimers)
                    {
                        gimbal.AimAt(aimPosFinal);
                    }
                }
                else
                {
                    if (settled)
                        return;
                    settled = true;
                    foreach (MimicGimbalAimer gimbal in aimers)
                    {
                        if (!gimbal.AimBack())
                            settled = false;
                    }
                }
            }
            public void ChainFire()
            {
                if (cooldown > 0 || !weaponHint.ReadyToFire())
                {
                    return;
                }
                if (weaponHint.m_FireControlMode == ModuleWeaponGun.FireControlMode.AllAtOnce)
                {
                    for (int step = 0; step < barrelNum; step++)
                    {
                        if (LockOnFireQueueBarrel(step))
                            barrelsFired++;
                    }
                }
                else
                {
                    if (LockOnFireQueueBarrel(barrelOn))
                        barrelsFired++;
                    if (barrelOn == barrelNum - 1)
                        barrelOn = 0;
                    else
                        barrelOn++;
                }
                if (barrelsFired > 0)
                {
                    if (weaponHint.m_BurstShotCount > 0)
                    {
                        burstCount -= barrelsFired;
                        if (burstCount <= 0)
                        {
                            cooldown = weaponHint.m_BurstCooldown;
                            burstCount = weaponHint.m_BurstShotCount;
                        }
                        else
                            cooldown = weaponHint.m_ShotCooldown;
                    }
                    else
                        cooldown = weaponHint.m_ShotCooldown;
                }
            }
            private bool LockOnFireQueueBarrel(int barrelNum)
            {
                MimicBarrel barry = barrels[barrelNum];
                if (barry.CB.HasClearLineOfFire())
                {
                    barry.TryFire();
                    return true;
                }
                return false;
            }
        }
    }
}
