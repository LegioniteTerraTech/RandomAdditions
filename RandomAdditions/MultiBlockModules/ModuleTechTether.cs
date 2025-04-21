using System;
using System.Collections.Generic;
using System.Linq;
using SafeSaves;
using UnityEngine;
using UnityEngine.Networking;
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
        public class TetherMessage : MessageBase
        {
            public TetherMessage() { }
            public TetherMessage(ModuleTechTether main, ModuleTechTether link, bool connected, bool playSound)
            {
                BlockIndex = main.block.GetBlockIndexAndTechNetID(out TechID);
                OtherBlockIndex = link.block.GetBlockIndexAndTechNetID(out TechOther);
                this.connected = connected;
                this.playSound = playSound;
            }
            
            public uint TechID;
            public int BlockIndex;
            public uint TechOther;
            public int OtherBlockIndex;
            public bool connected;
            public bool playSound;
        }
        private static NetworkHook<TetherMessage> netHook = new NetworkHook<TetherMessage>(OnReceiveTetherRequest, NetMessageType.FromClientToServerThenClients);

        private static bool OnReceiveTetherRequest(TetherMessage command, bool isServer)
        {
            //command.GetBlockModuleOnTech<ModuleTechTether>(command.TechID, command.BlockIndex, out var MTT)
            NetTech NT = ManNetTechs.inst.FindTech(command.TechID);
            NetTech target = ManNetTechs.inst.FindTech(command.TechOther);
            if (NT?.tech && target?.tech)
            {
                TankBlock TB = NT.tech.blockman.GetBlockWithIndex(command.BlockIndex);
                TankBlock TB2 = target.tech.blockman.GetBlockWithIndex(command.OtherBlockIndex);
                if (TB && TB2)
                {
                    ModuleTechTether MTT = TB.GetComponent<ModuleTechTether>();
                    ModuleTechTether MTT2 = TB2.GetComponent<ModuleTechTether>();
                    if (MTT && MTT2)
                    {
                        if (command.connected)
                        {
                            DoLinkTethers(MTT, MTT2, command.playSound);
                            return true;
                        }
                        else
                        {
                            for (int step = 0; step < LinkedTethers.Count; step++)
                            {
                                var item = LinkedTethers.ElementAt(step);
                                if (item.main == MTT && item.link == MTT2)
                                {
                                    item.DoDisconnect(command.playSound);
                                    LinkedTethers.Remove(item);
                                    return true;
                                }
                            }
                            DebugRandAddi.Assert("OnReceiveTetherRequest - Request for disconnect has no matches!");
                        }
                        DebugRandAddi.Assert("OnReceiveTetherRequest - Failed layer 3!");
                        return false;
                    }
                    DebugRandAddi.Assert("OnReceiveTetherRequest - Failed layer 2!");
                    return false;
                }
                DebugRandAddi.Assert("OnReceiveTetherRequest - Failed layer 1!");
                return false;
            }
            DebugRandAddi.Assert("OnReceiveTetherRequest - Failed layer 0!");
            // Else we cannot link it!
            return false;
        }

        private const float BumpLinkReconnectIgnoreTime = 2;


        public static Event<bool, Tank, Tank> ConnectionTethersUpdate = new Event<bool, Tank, Tank>();

        private static List<ModuleTechTether> ActiveTethers;
        private static ModuleTechTether SelectedTether;
        private static ModuleTechTether hoveringOver;
        private static HashSet<TetherPair> LinkedTethers;
        private static List<TetherPair> DeLinkedTethers = new List<TetherPair>();
        private static bool UIClicked = false;

        public static void Init()
        {
            ActiveTethers = new List<ModuleTechTether>();
            LinkedTethers = new HashSet<TetherPair>();
            //Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(OnFixedUpdatePre), 85);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(OnFixedUpdatePost), -85);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, new Action(OnUpdate), -9001);
            netHook.Register();
        }
        public static void DeInit()
        {
            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.Last, new Action(OnUpdate));
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(OnFixedUpdatePost));
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, new Action(OnFixedUpdatePre));
            //Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(OnClick);
            LinkedTethers = null;
            ActiveTethers = null;
        }
        public static void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (mEvent == ManPointer.Event.RMB)
            {
                if (!UIClicked)
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
                                    TryLinkTethers(SelectedTether, tether, true);
                                }
                            }
                            else if (targVis.tank)
                            {
                                /*
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
                                }*/
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

                UIClicked = false;
            }
        }

        public static bool OnAttachRequest(ModuleTechTether tether)
        {
            if (SelectedTether && SelectedTether.block.visible.isActive
                && SelectedTether.block.tank && tether != SelectedTether && tether.tank && CanLinkTethers(SelectedTether, tether))
            {
                TryLinkTethers(SelectedTether, tether, true);
                return true;
            }
            return false;
        }


        public static bool CanLinkTethers(ModuleTechTether Main, ModuleTechTether Link)
        {
            if (!Main.IsConnected && !Link.IsConnected)
            {
                float combinedMaxDist = Mathf.Pow(Main.MaxDistance + Link.MaxDistance, 2);
                Vector3 spacing = Main.tetherEnd.position - Link.tetherEnd.position;
                float Dist = spacing.sqrMagnitude;
                if (Dist <= combinedMaxDist)
                    return true;
            }
            return false;
        }
        public static void TryLinkTethers(ModuleTechTether Main, ModuleTechTether Link, bool playSound)
        {
            if (netHook.CanBroadcast())
                netHook.TryBroadcast(new TetherMessage(Main, Link, true, playSound));
            else
                DoLinkTethers(Main, Link, playSound);
        }
        public static void DoLinkTethers(ModuleTechTether Main, ModuleTechTether Link, bool playSound)
        {
            if (Main.IsConnected)
                Main.Connection.DoDisconnect(false);
            if (Link.IsConnected)
                Link.Connection.DoDisconnect(false);
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
                if (Main.animette != null)
                    Main.animette.RunBool(true);
                if (Link.animette != null)
                    Link.animette.RunBool(true);
                if (ManNetwork.IsHost)
                {
                    Main.tank.control.explosiveBoltDetonateEvents[3].Subscribe(Main.DisconnectX);
                    Link.tank.control.explosiveBoltDetonateEvents[3].Subscribe(Link.DisconnectX);
                }
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
                    if (ManNetwork.IsHost)
                        DeLinkedTethers.Add(item);
            }
            foreach (var item in DeLinkedTethers)
            {
                item.TryDisconnect(true, true);
            }
            DeLinkedTethers.Clear();
        }

        private static void OnFixedUpdatePost()
        {
            foreach (var item in LinkedTethers)
            {
                if (item.main.tank.rbody && item.main.tank.CanRunPhysics())
                    item.main.tank.rbody.AddForceAtPosition(-item.forceNormal * item.forcesThisFrame, item.main.tetherPoint.position, ForceMode.Force);
                if (item.link.tank.rbody && item.link.tank.CanRunPhysics())
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


        internal class GUIManaged
        {
            private static bool display = false;
            private static bool displayLinked = false;

            public static void GUIGetTotalManaged()
            {
                GUILayout.Box("---- Physics Tethers --- ");
                display = AltUI.Toggle(display, "Show: ");
                if (display)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Active Tethers: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(ActiveTethers.Count.ToString());
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Linked Tethers: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(LinkedTethers.Count.ToString());
                    GUILayout.EndHorizontal();

                    displayLinked = AltUI.Toggle(displayLinked, "Show Linked: ");
                    if (displayLinked)
                    {
                        foreach (var item in LinkedTethers)
                        {
                            if (item != null)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label("Main: ");
                                GUILayout.Label(item.main?.tank ? "NULL" : item.main.tank.ToString());
                                GUILayout.FlexibleSpace();
                                GUILayout.Label("Other: ");
                                GUILayout.Label(item.link?.tank ? "NULL" : item.link.tank.ToString());
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
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
            internal void TryDisconnect(bool HostOnly, bool playSound)
            {
                if (!ManNetwork.IsNetworked)
                    DoDisconnect(playSound);
                else if (!HostOnly || ManNetwork.IsHost)
                    netHook.TryBroadcast(new TetherMessage(main, link, false, playSound));
            }
            internal void DoDisconnect(bool playSound)
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
        public class ModuleTechTether : ExtModule, TechAudio.IModuleAudioProvider, ICircuitDispensor
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

            // Logic
            public int DissconnectInputAPIndex = 0;
            public int[] InputsOnAPIndexes = new int[0];
            public int[] OutputsOnAPIndexes = new int[0];
            internal Dictionary<Vector3, int> OutputsCache = new Dictionary<Vector3, int>();

            // Audio
            public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
            public TechAudio.SFXType m_ConnectSFXType = TechAudio.SFXType.Anchored;
            public TechAudio.SFXType SFXType => m_ConnectSFXType;

            // Tethering
            public TetherPair Connection;
            public bool IsConnected => Connection != null;

            [SSaveField]
            public bool AutoLink = true;
            [SSaveField]
            public int lastConnectedID = -1;
            [SSaveField]
            public int lastConnectedBlockPos = -1;

            internal Transform tetherPoint;
            internal Transform tetherEnd;
            internal AnimetteController animette;


            private Transform tetherUpright;
            private Transform tetherConnector;
            private Transform tetherConnectorEnd;
            private float tetherConnectorStartLocal = 1;
            private Collider bumpLinkCollider;
            private float bumpLinkReconnectDelay = 0;

            private ModuleUIButtons buttonGUI;


            protected override void Pool()
            {
                enabled = true;
                try
                {
                    tetherPoint = KickStart.HeavyTransformSearch(transform, "_Tether");
                }
                catch { }
                if (tetherPoint == null)
                {
                    tetherPoint = this.transform;
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_Tether\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                }
                try
                {
                    tetherUpright = KickStart.HeavyTransformSearch(transform, "_TetherUp");
                    if (tetherUpright)
                    {
                        tetherConnector = KickStart.HeavyTransformSearch(transform, "_TetherConnector");
                        if (tetherConnector == null)
                            LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_TetherConnector\" if \"_TetherUp\" is present!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        tetherConnectorEnd = KickStart.HeavyTransformSearch(transform, "_TetherConnectorEnd");
                        if (tetherConnectorEnd == null)
                            LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_TetherConnectorEnd\" if \"_TetherUp\" is present!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                        tetherConnectorStartLocal = tetherConnectorEnd.localPosition.z;
                    }
                }
                catch { tetherUpright = null; }

                try
                {
                    Transform bumpTrans = KickStart.HeavyTransformSearch(transform, "_TetherBumpCollider");
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
                    tetherEnd = KickStart.HeavyTransformSearch(transform, "_Target");
                }
                catch { }
                if (tetherEnd != null)
                {
                    if (UseLineTether || !tetherUpright)
                        InitTracBeam();
                }
                else
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_Target\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                if (InputsOnAPIndexes.Length != OutputsOnAPIndexes.Length)
                {
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether every AP specified in " +
                      "InputsOnAPIndexes should have a OutputsOnAPIndexes counterpart." +
                      "\nThis operation cannot be handled " +
                      "automatically.\nCause of error - Block " + gameObject.name);
                }
                else
                {
                    if (InputsOnAPIndexes.Length > 0)
                    {
                        for (int step = 0; step < InputsOnAPIndexes.Length; step++)
                        {
                            OutputsCache[block.attachPoints[InputsOnAPIndexes[step]]] = 0;
                        }
                    }
                }
                animette = KickStart.FetchAnimette(transform, "_Tether", AnimCondition.Tether);
                if (animette != null)
                    animette.RunBool(false);
                block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                InsureGUI();
            }

            private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleTechTether",
                 AltUI.HighlightString("Couplers") + " connects to other " + AltUI.HighlightString("Couplers") + 
                " to keep " + AltUI.BlueString("Techs") + " together."); 
            public override void OnGrabbed()
            {
                hint.Show();
            }
            public override void OnAttach()
            {
                InsureGUI();
                ActiveTethers.Add(this);
                bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                tank.TechAudio.AddModule(this);
                tank.CollisionEvent.Subscribe(OnCollisionTank);
                if (block.CircuitNode?.Receiver)
                    ExtraExtensions.SubToLogicReceiverCircuitUpdate(this, OnRecCharge, false, true);
            }
            public override void OnDetach()
            {
                if (block.CircuitNode?.Receiver)
                    ExtraExtensions.SubToLogicReceiverCircuitUpdate(this, OnRecCharge, true, true);
                tank.CollisionEvent.Unsubscribe(OnCollisionTank);
                tank.TechAudio.RemoveModule(this);
                if (Connection != null)
                    Connection.TryDisconnect(true, true);

                if (animette != null)
                    animette.RunBool(false);
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
                    if (MTT && MTT.bumpLinkCollider == col.b.collider && CanLinkTethers(this, MTT))
                    {
                        bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                        TryLinkTethers(this, MTT, true);
                    }
                }
            }

            public void InsureGUI()
            {
                if (buttonGUI == null)
                {
                    buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Tether", true);
                    buttonGUI.AddElement(AutoLinkStatus, ToggleAutoLink, AutoLinkIcon);
                    buttonGUI.AddElement(ConnectionStatus, RequestConnection, ConnectionIcon);
                }
            }
            public string ConnectionStatus()
            {
                if (IsConnected)
                    return "Unlink";
                else
                    return "Link";
            }
            public Sprite ConnectionIcon()
            {
                ModContainer MC = ManMods.inst.FindMod("Random Additions");
                if (IsConnected)
                    return UIHelpersExt.GetIconFromBundle(MC, "GUI_Unlink");
                return UIHelpersExt.GetIconFromBundle(MC, "GUI_Link");
            }
            public float RequestConnection(float unused)
            {
                UIClicked = true;
                if (IsConnected)
                {
                    Connection.TryDisconnect(false, true);
                }
                else
                {
                    //ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                    if (SelectedTether != null)
                    {
                        OnAttachRequest(this);
                        SelectedTether = null;
                        foreach (var item in ActiveTethers)
                        {
                            item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                        }
                        return 0;
                    }
                    SelectedTether = this;
                    foreach (var item in ActiveTethers)
                    {
                        if (item.tank != SelectedTether.tank && CanLinkTethers(this, item))
                        {
                            item.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                        }
                    }
                    hoveringOver = null;
                    return 0;
                }
                return 0;
            }
            public string AutoLinkStatus()
            {
                if (AutoLink)
                    return "Auto Enabled";
                else
                    return "Auto Disabled";
            }
            public Sprite AutoLinkIcon()
            {
                ModContainer MC = ManMods.inst.FindMod("Random Additions");
                if (AutoLink)
                    return UIHelpersExt.GetIconFromBundle(MC, "GUI_Reset");
                return UIHelpersExt.GetIconFromBundle(MC, "GUI_Power");
            }
            public float ToggleAutoLink(float unused)
            {
                AutoLink = !AutoLink;
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.CheckBox);
                return AutoLink ? 1 : 0;
            }



            internal void DisconnectX()
            {
                if (IsConnected)
                {
                    Connection.TryDisconnect(true, true);
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
                    tank.TechAudio.PlayOneshot(TechAudio.AudioTickData.ConfigureOneshot(block, m_ConnectSFXType, 
                        0.3f, 1));
                    /*
                    if (OnAudioTickUpdate != null)
                    {
                        TechAudio.AudioTickData audioTickData = default;
                        audioTickData.block = block; // only need pos
                        audioTickData.provider = this;
                        audioTickData.sfxType = m_ConnectSFXType;
                        audioTickData.numTriggered = 1;
                        audioTickData.triggerCooldown = 1;
                        audioTickData.isNoteOn = true;
                        audioTickData.adsrTime01 = 1;//doSpool ? 1 : 0;
                        TechAudio.AudioTickData value = audioTickData;
                        tank.TechAudio.PlayOneshot(audioTickData);
                    }*/
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
                    if (ManNetwork.IsHost)
                        tank.control.explosiveBoltDetonateEvents[3].Unsubscribe(DisconnectX);
                    BeamSide = false;
                    Connection = null;
                    bumpLinkReconnectDelay = Time.time + BumpLinkReconnectIgnoreTime;
                    bumpLinkCollider.gameObject.SetActive(true);
                    if (animette != null)
                        animette.RunBool(false);
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
                        if (nextTech && !list.ContainsKey(nextTech.tank))
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
                                        DoLinkTethers(this, comp, false);
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

            // LOGIC interface

            public int GetDispensableCharge(Vector3 APOut)
            {
                if (IsConnected && Connection.GetOpposingSide(this).OutputsCache.TryGetValue(APOut, out int val))
                    return val;
                return 0;
            }

            public void OnRecCharge(Circuits.BlockChargeData charge)
            {
                //DebugRandAddi.Log("OnRecCharge " + charge);
                try
                {
                    foreach (var item in InputsOnAPIndexes)
                    {
                        OutputsCache[block.attachPoints[item]] = charge.AllChargeAPsAndCharges[block.attachPoints[item]];
                    }
                }
                catch { }
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
