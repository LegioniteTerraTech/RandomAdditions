using System;
using System.Collections.Generic;
using System.Linq;
using SafeSaves;
using UnityEngine;
using TerraTechETCUtil;


public class ModuleTechTether : RandomAdditions.PhysicsTethers.ManTethers.ModuleTechTether { }
namespace RandomAdditions.PhysicsTethers
{
    public static class ManTethersExtended
    {
        public static List<Tank> GetAllConnectedTechs(this Tank tank)
        {
            foreach (var item in tank.blockman.IterateBlocks())
            {
                ModuleTechTether MTT = item.GetComponent<ModuleTechTether>();
                if (MTT)
                    return MTT.GetAllConnectedTechs();
            }
            return new List<Tank>();
        }

        public static Dictionary<Tank, Quaternion> GetAllConnectedTechsRelativeRotation(this Tank tank)
        {
            foreach (var item in tank.blockman.IterateBlocks())
            {
                ModuleTechTether MTT = item.GetComponent<ModuleTechTether>();
                if (MTT)
                    return MTT.GetAllConnectedTethersRelativeRotation();
            }
            return new Dictionary<Tank, Quaternion>();
        }

    }
    public class ManTethers
    {
        private const float BumpLinkReconnectIgnoreTime = 2;


        public static Event<bool, Tank, Tank> ConnectionTethersUpdate = new Event<bool, Tank, Tank>();

        private static List<ModuleTechTether> ActiveTethers;
        private static ModuleTechTether SelectedTether;
        private static ModuleTechTether hoveringOver;
        private static HashSet<TetherPair> LinkedTethers;
        private static List<TetherPair> DeLinkedTethers = new List<TetherPair>();
        public static void Init()
        {
            ActiveTethers = new List<ModuleTechTether>();
            LinkedTethers = new HashSet<TetherPair>();
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(OnFixedUpdatePre), 85);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(OnFixedUpdatePost), -85);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, new Action(OnUpdate), -9001);
        }
        public static void DeInit()
        {
            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.Last, new Action(OnUpdate));
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(OnFixedUpdatePost));
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(OnFixedUpdatePre));
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(OnClick);
            LinkedTethers = null;
            ActiveTethers = null;
        }
        public static void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (mEvent == ManPointer.Event.RMB)
            {
                var targVis = Singleton.Manager<ManPointer>.inst.targetVisible?.block;
                if (targVis)
                {
                    if (down)
                    {
                        var tether = targVis.GetComponent<ModuleTechTether>();
                        if (tether)
                            hoveringOver = tether;
                    }
                    else
                    {
                        if (SelectedTether && SelectedTether.block.visible.isActive
                            && SelectedTether.block.tank && targVis.tank)
                        {
                            var tether = targVis.GetComponent<ModuleTechTether>();
                            if (tether && tether == hoveringOver && tether != SelectedTether)
                            {
                                if (SelectedTether.IsConnected)
                                    SelectedTether.Connection.Disconnect(false);
                                if (tether.IsConnected)
                                    tether.Connection.Disconnect(false);
                                LinkTethers(SelectedTether, tether, true);
                            }
                        }
                        else if (targVis.tank)
                        {
                            var tether = targVis.GetComponent<ModuleTechTether>();
                            if (tether && tether == hoveringOver)
                            {
                                if (tether.IsConnected)
                                {
                                    tether.Connection.Disconnect(true);
                                }
                                else
                                {
                                    SelectedTether = tether;
                                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                                    foreach (var item in ActiveTethers)
                                    {
                                        if (item.tank != SelectedTether.tank)
                                        {
                                            item.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                                        }
                                    }
                                    hoveringOver = null;
                                    return;
                                }
                            }
                        }
                        foreach (var item in ActiveTethers)
                        {
                            item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                        }
                        hoveringOver = null;
                        SelectedTether = null;
                    }
                }
                else
                {
                    foreach (var item in ActiveTethers)
                    {
                        item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                    }
                    hoveringOver = null;
                    SelectedTether = null;
                }
            }
        }

        public static void LinkTethers(ModuleTechTether Main, ModuleTechTether Link, bool playSound)
        {
            if (!Main.IsConnected && !Link.IsConnected)
            {
                if (Main == Link)
                {
                    DebugRandAddi.Log("ModuleTechTether " + Main.name + " attempted to connect to itself");
                    return;
                }
                TetherPair NewConnection;
                if (Main.tank == Link.tank)
                {
                    NewConnection = new TetherPair(Main, Link, 0, Main.MaxDistance + Link.MaxDistance, 
                        Main.LooseDistance + Link.LooseDistance, Main.MinDistance + Link.MinDistance);
                    DebugRandAddi.Log("ModuleTechTether " + Main.name + " Is linked to another on the same Tech.  This will have no physics impact.");
                }
                else
                    NewConnection = new TetherPair(Main, Link, Main.SpringTensionForce + Link.SpringTensionForce,
                    Main.MaxDistance + Link.MaxDistance, Main.LooseDistance + Link.LooseDistance, Main.MinDistance + Link.MinDistance);
                LinkedTethers.Add(NewConnection);
                Main.Connection = NewConnection;
                Link.Connection = NewConnection;
                Main.BeamSide = true;
                if (Main.AC)
                    Main.AC.RunBool(true);
                if (Link.AC)
                    Link.AC.RunBool(true);
                ConnectionTethersUpdate.Send(true, Main.tank, Link.tank);

                if (playSound && (Main.block.trans.position - Singleton.playerPos).Approximately(Vector3.zero, 100))
                    Main.FireConnectionSound(true);

                DebugRandAddi.Info("ModuleTechTether attached");
            }
        }


        /// <summary>
        /// Update the tether physics
        /// </summary>
        private static void OnFixedUpdatePre()
        {
            foreach (var item in LinkedTethers)
            {
                if (!item.CalcPhysicsImpact())
                    DeLinkedTethers.Add(item);
            }
            foreach (var item in DeLinkedTethers)
            {
                item.Disconnect(true);
            }
            DeLinkedTethers.Clear();
        }

        private static void OnFixedUpdatePost()
        {
            foreach (var item in LinkedTethers)
            {
                if (item.main.tank.rbody)
                    item.main.tank.rbody.AddForceAtPosition(-item.forceNormal * item.forcesThisFrame, item.main.tetherPoint.position, ForceMode.Force);
                if (item.link.tank.rbody)
                    item.link.tank.rbody.AddForceAtPosition(item.forceNormal * item.forcesThisFrame, item.link.tetherPoint.position, ForceMode.Force);
            }
        }

        private static void OnUpdate()
        {
            foreach (var item in LinkedTethers)
            {
                if (item.main?.tank && item.link?.tank)
                {
                    float Dist = (item.main.tetherEnd.position - item.link.tetherEnd.position).magnitude / 2;
                    item.main.UpdateTether(Dist);
                    item.link.UpdateTether(Dist);
                }
            }
        }



        public class TetherPair
        {
            public readonly ModuleTechTether main;
            public readonly ModuleTechTether link;

            public readonly float combinedTension;
            public readonly float combinedMaxDist;
            public readonly float combinedLooseDist;
            public readonly float combinedMinDist;

            public float forcesThisFrame { get; private set; }
            public Vector3 forceNormal { get; private set; }

            internal TetherPair(ModuleTechTether thisSide, ModuleTechTether otherSide,
                float tension, float maxDist, float looseDist, float minDist)
            {
                main = thisSide;
                link = otherSide;
                combinedTension = tension / 2;
                combinedLooseDist = Mathf.Pow(looseDist, 2);
                combinedMaxDist = Mathf.Pow(maxDist, 2);
                combinedMinDist = Mathf.Pow(minDist, 2);
            }

            internal ModuleTechTether GetOpposingSide(ModuleTechTether thisSide)
            {
                if (thisSide == main)
                    return link;
                return main;
            }
            internal void Disconnect(bool playSound = false)
            {
                if (main == null || link == null)
                {
                    DebugRandAddi.Assert(true, "ManTethers.TetherPair has unset tether relations.  This should not be possible.");
                }
                if (playSound && main && (main.block.trans.position - Singleton.playerPos).Approximately(Vector3.zero, 100))
                    main.FireConnectionSound(false);

                if (main?.tank)
                    main.Delink();
                if (link?.tank)
                    link.Delink();

                ConnectionTethersUpdate.Send(false, main.tank, link.tank);
                LinkedTethers.Remove(this);
            }


            internal bool CalcPhysicsImpact()
            {
                if (main?.tank && link?.tank)
                {
                    Vector3 spacing = main.tetherEnd.position - link.tetherEnd.position;
                    float Dist = spacing.sqrMagnitude;
                    if (Dist > combinedMaxDist)
                        return false;

                    if (Dist > combinedLooseDist)
                    {
                        forcesThisFrame = combinedTension * Mathf.Min(Mathf.InverseLerp(combinedLooseDist, combinedMaxDist, Dist), 1);
                        forceNormal = spacing.normalized;
                    }
                    else if (Dist < combinedMinDist)
                    {
                        forcesThisFrame = -combinedTension * Mathf.Min(Mathf.InverseLerp(combinedMinDist, 0, Dist), 1);
                        forceNormal = spacing.normalized;
                    }
                    else
                    {   // no force applied 
                        forcesThisFrame = 0;
                        forceNormal = spacing.normalized;
                    }
                    return true;
                }
                return false;
            }
        }


        // Note that this module is merely a suggestion - Can break if enough force is applied 
        [AutoSaveComponent]
        public class ModuleTechTether : ExtModule, TechAudio.IModuleAudioProvider
        {
            public float SpringTensionForce = 750;
            public float MaxDistance = 8;
            public float MinDistance = 0;
            public float LooseDistance = 4;
            public float TetherScale = 0.4f;
            public bool RelayControls = false;
            public bool UseLineTether = true;

            public Material BeamMaterial = null;
            public Color BeamColorStart = new Color(0.05f, 0.1f, 1f, 0.8f);
            public Color BeamColorEnd = new Color(0.05f, 0.1f, 1f, 0.8f);

            // Audio
            public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
            public TechAudio.SFXType m_ConnectSFXType = TechAudio.SFXType.Anchored;
            public TechAudio.SFXType SFXType => m_ConnectSFXType;

            // Tethering
            public TetherPair Connection;
            public bool IsConnected => Connection != null;

            [SSaveField]
            public int lastConnectedID = -1;
            [SSaveField]
            public int lastConnectedBlockPos = -1;

            internal Transform tetherPoint;
            internal Transform tetherEnd;
            internal AnimetteController AC;


            private Transform tetherUpright;
            private Transform tetherConnector;
            private Transform tetherConnectorEnd;
            private float tetherConnectorStartLocal = 1;
            private Collider bumpLinkCollider;
            private float bumpLinkReconnectDelay = 0;


            protected override void Pool()
            {
                enabled = true;
                try
                {
                    tetherPoint = KickStart.HeavyObjectSearch(transform, "_Tether");
                }
                catch { }
                if (tetherPoint == null)
                {
                    tetherPoint = this.transform;
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_Tether\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                }
                try
                {
                    tetherUpright = KickStart.HeavyObjectSearch(transform, "_TetherUp");
                    if (tetherUpright)
                    {
                        tetherConnector = KickStart.HeavyObjectSearch(transform, "_TetherConnector");
                        if (tetherConnector == null)
                            LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_TetherConnector\" if \"_TetherUp\" is present!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        tetherConnectorEnd = KickStart.HeavyObjectSearch(transform, "_TetherConnectorEnd");
                        if (tetherConnectorEnd == null)
                            LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_TetherConnectorEnd\" if \"_TetherUp\" is present!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        tetherConnectorStartLocal = tetherConnectorEnd.localPosition.z;
                    }
                }
                catch { tetherUpright = null; }

                try
                {
                    Transform bumpTrans = KickStart.HeavyObjectSearch(transform, "_TetherBumpCollider");
                    if (bumpTrans)
                    {
                        bumpLinkCollider = bumpTrans.GetComponent<SphereCollider>();
                        if (bumpLinkCollider == null)
                            LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a SphereCollider component for GameObject \"_TetherBumpCollider\" in hierarchy named if it is present!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                    }
                }
                catch { bumpLinkCollider = null; }

                try
                {
                    tetherEnd = KickStart.HeavyObjectSearch(transform, "_Target");
                }
                catch { }
                if (tetherEnd != null)
                {
                    if (UseLineTether || !tetherUpright)
                        InitTracBeam();
                }
                else
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_Target\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                AC = KickStart.FetchAnimette(transform, "_Tether", AnimCondition.Tether);
                block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }

            public override void OnAttach()
            {
                ExtUsageHint.ShowExistingHint(4010);
                ActiveTethers.Add(this);
                bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                tank.TechAudio.AddModule(this);
                tank.CollisionEvent.Subscribe(OnCollisionTank);
                tank.control.explosiveBoltDetonateEvents[0].Subscribe(DisconnectX);
            }
            public override void OnDetach()
            {
                tank.control.explosiveBoltDetonateEvents[0].Unsubscribe(DisconnectX);
                tank.CollisionEvent.Unsubscribe(OnCollisionTank);
                tank.TechAudio.RemoveModule(this);
                if (Connection != null)
                    Connection.Disconnect(true);
                UpdateTether(tetherConnectorStartLocal);
                ActiveTethers.Remove(this);
                block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            }

            private void OnCollisionTank(Tank.CollisionInfo col, Tank.CollisionInfo.Event eventC)
            {
                if (bumpLinkReconnectDelay < Time.time && eventC == Tank.CollisionInfo.Event.Enter 
                    && col.b.block && col.a.collider == bumpLinkCollider)
                {
                    var MTT = col.b.block.GetComponent<ModuleTechTether>();
                    if (MTT && MTT.bumpLinkCollider == col.b.collider)
                    {
                        bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                        LinkTethers(this, MTT, true);
                    }
                }
            }



            internal void DisconnectX(TechSplitNamer unused)
            {
                if (IsConnected)
                {
                    Connection.Disconnect(true);
                }
            }
            internal void FireConnectionSound(bool connected)
            {
                try
                {
                    if (connected)
                        m_ConnectSFXType = TechAudio.SFXType.Anchored;
                    else
                        m_ConnectSFXType = TechAudio.SFXType.UnAnchored;
                    if (OnAudioTickUpdate != null)
                    {
                        TechAudio.AudioTickData audioTickData = default;
                        audioTickData.module = block.damage;
                        audioTickData.provider = this;
                        audioTickData.sfxType = m_ConnectSFXType;
                        audioTickData.numTriggered = 1;
                        audioTickData.triggerCooldown = 1;
                        audioTickData.isNoteOn = true;
                        audioTickData.adsrTime01 = 1;//doSpool ? 1 : 0;
                        TechAudio.AudioTickData value = audioTickData;
                        OnAudioTickUpdate.Send(value, null);
                    }
                }
                catch { }
            }


            /// <summary>
            /// Only to be called from TetherPair!
            /// </summary>
            internal void Delink()
            {
                if (IsConnected)
                {
                    BeamSide = false;
                    Connection = null;
                    bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                    bumpLinkCollider.gameObject.SetActive(true);
                    if (AC)
                        AC.RunBool(false);
                    ResetTether();
                }
            }
            public Tank GetOtherSideTech()
            {
                if (Connection != null)
                {
                    ModuleTechTether MTT = Connection.GetOpposingSide(this);
                    if (MTT && MTT.tank)
                    {
                        return MTT.tank;
                    }
                }
                return null;
            }
            public Tank GetOtherSideTechWithQuat(out Quaternion offset)
            {
                if (Connection != null)
                {
                    ModuleTechTether MTT = Connection.GetOpposingSide(this);
                    if (MTT && MTT.tank)
                    {
                        Quaternion main = block.cachedLocalRotation;
                        Quaternion other = MTT.block.cachedLocalRotation;
                        offset = main * Quaternion.AngleAxis(180, Vector3.up) * other;
                        return MTT.tank;
                    }
                }
                offset = Quaternion.identity;
                return null;
            }
            public ModuleTechTether GetOtherSideTether()
            {
                if (Connection != null)
                {
                    ModuleTechTether MTT = Connection.GetOpposingSide(this);
                    if (MTT && MTT.tank)
                    {
                        return MTT;
                    }
                }
                return null;
            }
            public ModuleTechTether GetOtherSideTetherWithQuat(out Quaternion offset)
            {
                if (Connection != null)
                {
                    ModuleTechTether MTT = Connection.GetOpposingSide(this);
                    if (MTT && MTT.tank)
                    {
                        Quaternion main = block.trans.localRotation;
                        Quaternion other = MTT.block.trans.localRotation;
                        offset = Quaternion.LookRotation(Quaternion.FromToRotation(main * Vector3.forward, other * Vector3.back) * Vector3.forward);
                        return MTT;
                    }
                }
                offset = Quaternion.identity;
                return null;
            }


            public List<Tank> GetAllConnectedTechs()
            {
                HashSet<Tank> tethered = new HashSet<Tank>();
                foreach (var item in GetAllTethersAcrossNetwork())
                {
                    if (item && item.tank && !tethered.Contains(item.tank))
                    {
                        DebugRandAddi.Log("TankLocomotive: GetAllConnectedTechs - Registered ID: " + item.tank.visible.ID);
                        tethered.Add(item.tank);
                    }
                }
                return tethered.ToList();
            }

            public List<ModuleTechTether> GetAllTethersAcrossNetwork()
            {
                HashSet<ModuleTechTether> tethers = new HashSet<ModuleTechTether>();
                GetAllTethersRecurse(tethers);
                return tethers.ToList();
            }
            private void GetAllTethersRecurse(HashSet<ModuleTechTether> list)
            {
                foreach (var item in tank.blockman.IterateBlocks())
                {
                    ModuleTechTether MTT = item.GetComponent<ModuleTechTether>();
                    if (MTT && !list.Contains(MTT))
                    {
                        list.Add(MTT);
                        ModuleTechTether nextTech = MTT.GetOtherSideTether();
                        if (nextTech)
                            nextTech.GetAllTethersRecurse(list);
                    }
                }
            }

            public Dictionary<Tank, Quaternion> GetAllConnectedTethersRelativeRotation()
            {
                Dictionary<Tank, Quaternion> tethers = new Dictionary<Tank, Quaternion>();
                GetAllRecurseTethersRotation(tethers, Quaternion.identity);
                return tethers;
            }
            private void GetAllRecurseTethersRotation(Dictionary<Tank, Quaternion> list, Quaternion prev)
            {
                list.Add(tank, prev);
                foreach (var item in tank.blockman.IterateBlocks())
                {
                    ModuleTechTether MTT = item.GetComponent<ModuleTechTether>();
                    if (MTT)
                    {
                        ModuleTechTether nextTech = MTT.GetOtherSideTetherWithQuat(out Quaternion nextQuat);
                        if (nextTech && !list.TryGetValue(nextTech.tank, out _))
                        {
                            nextTech.GetAllRecurseTethersRotation(list, prev * nextQuat);
                        }
                    }
                }
            }


            private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
            {
                try
                {
                    if (saving)
                    {   // On Save (non-snap)
                        lastConnectedID = -1;
                        lastConnectedBlockPos = -1;
                        if (!Singleton.Manager<ManScreenshot>.inst.TakingSnapshot)
                        {   // Only save on world save
                            if (IsConnected)
                            {
                                var ConnectedOther = Connection.GetOpposingSide(this);
                                lastConnectedID = ConnectedOther.tank.visible.ID;
                                int countPos = 0;
                                foreach (var item in ConnectedOther.tank.blockman.IterateBlocks())
                                {
                                    var comp = item.GetComponent<ModuleTechTether>();
                                    if (comp && comp == ConnectedOther)
                                    {
                                        lastConnectedBlockPos = countPos;
                                        this.SerializeToSafe();
                                        break;
                                    }
                                    countPos++;
                                }
                            }
                        }
                    }
                    else
                    {   //Load from Save
                        try
                        {
                            //ManUndo.inst.UndoInProgress
                            if (!block.tank.FirstUpdateAfterSpawn)
                                return; // we ignore undo / swap
                            lastConnectedID = -1;
                            lastConnectedBlockPos = -1;
                            if (!IsConnected && this.DeserializeFromSafe())
                            {
                                Invoke("DelayedLink", 0.01f);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            internal void DelayedLink()
            {
                if (!IsConnected && lastConnectedID != -1)
                {
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        if (item.visible.ID == lastConnectedID)
                        {
                            int countPos = 0;
                            foreach (var item2 in item.blockman.IterateBlocks())
                            {
                                if (countPos == lastConnectedBlockPos)
                                {
                                    var comp = item2.GetComponent<ModuleTechTether>();
                                    if (comp)
                                    {
                                        LinkTethers(this, comp, false);
                                    }
                                    break;
                                }
                                countPos++;
                            }
                            break;
                        }
                    }
                }
            }


            /// <summary>
            /// Update the tether effect
            /// </summary>
            internal void UpdateTether(float linkDistance)
            {
                if (IsConnected)
                {
                    var ConnectedOther = Connection.GetOpposingSide(this);
                    if (tetherUpright)
                    {
                        tetherUpright.localRotation = Quaternion.LookRotation(Vector3.forward, tetherUpright.parent.InverseTransformDirection(Vector3.up));
                        Vector3 lookVec = tetherConnector.parent.InverseTransformDirection(ConnectedOther.tetherEnd.position - tetherEnd.position);
                        tetherConnector.localRotation = Quaternion.LookRotation(lookVec, Vector3.up);
                        tetherConnectorEnd.localPosition = tetherConnectorEnd.localPosition.SetZ(linkDistance);
                    }
                    if (canUseBeam)
                    {
                        if (BeamSide)
                        {
                            StartBeam();
                            UpdateTracBeam();
                        }
                        else
                            StopBeam();
                    }
                }
                else
                {
                    if (tetherUpright)
                    {
                        tetherUpright.localRotation = Quaternion.identity;
                        tetherConnector.localRotation = Quaternion.Lerp(tetherConnector.localRotation, Quaternion.identity, 0.25f);
                        tetherConnectorEnd.localPosition = tetherConnectorEnd.localPosition.SetZ(
                            Mathf.Lerp(tetherConnectorEnd.localPosition.z, tetherConnectorStartLocal, 0.25f));
                    }
                    if (canUseBeam)
                        StopBeam();
                }

            }

            internal void ResetTether()
            {
                if (tetherUpright)
                {
                    tetherUpright.localRotation = Quaternion.identity;
                    tetherConnector.localRotation = Quaternion.identity;
                    tetherConnectorEnd.localPosition = tetherConnectorEnd.localPosition.SetZ(tetherConnectorStartLocal);
                }
                if (canUseBeam)
                {
                    StopBeam();
                }
            }


            private LineRenderer TracBeamVis;
            private bool canUseBeam = false;
            internal bool BeamSide = false;
            private void InitTracBeam()
            {
                Transform TO = transform.Find("TracLine");
                GameObject gO = null;
                if ((bool)TO)
                    gO = TO.gameObject;
                if (!(bool)gO)
                {
                    gO = Instantiate(new GameObject("TracLine"), transform, false);
                    gO.transform.localPosition = Vector3.zero;
                    gO.transform.localRotation = Quaternion.identity;
                }
                //}
                //else
                //    gO = line.gameObject;

                var lr = gO.GetComponent<LineRenderer>();
                if (!(bool)lr)
                {
                    lr = gO.AddComponent<LineRenderer>();
                    if (BeamMaterial != null)
                        lr.material = BeamMaterial;
                    else
                        lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.positionCount = 2;
                    lr.endWidth = TetherScale;
                    lr.startWidth = TetherScale;
                    lr.useWorldSpace = true;
                    lr.startColor = BeamColorStart;
                    lr.endColor = BeamColorEnd;
                    lr.numCapVertices = 8;
                    lr.SetPositions(new Vector3[2] { new Vector3(0, 0, -1), Vector3.zero });
                }
                TracBeamVis = lr;
                TracBeamVis.gameObject.SetActive(false);
                canUseBeam = true;
            }
            private void UpdateTracBeam()
            {
                var ConnectedOther = Connection.GetOpposingSide(this);
                TracBeamVis.startColor = new Color(0.25f, 1, 0.25f, 0.9f);
                TracBeamVis.endColor = new Color(0.1f, 1, 0.1f, 0.9f);
                TracBeamVis.positionCount = 2;
                TracBeamVis.SetPositions(new Vector3[2] { tetherEnd.position, ConnectedOther.tetherEnd.position });
            }
            private void StartBeam()
            {
                if (!TracBeamVis.gameObject.activeSelf)
                {
                    TracBeamVis.gameObject.SetActive(true);
                }
            }
            private void StopBeam()
            {
                if (TracBeamVis.gameObject.activeSelf)
                {
                    TracBeamVis.gameObject.SetActive(false);
                }
            }
        }
    }
}
