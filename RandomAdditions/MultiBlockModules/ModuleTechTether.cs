using System;
using System.Collections.Generic;
using System.Linq;
using SafeSaves;
using UnityEngine;
using TerraTechETCUtil;


public class ModuleTechTether : RandomAdditions.ManTethers.ModuleTechTether { }
namespace RandomAdditions
{
    public class ManTethers : MonoBehaviour
    {
        public class TetherPair
        {
            public ModuleTechTether main;
            public ModuleTechTether link;

            public float combinedTension;
            public float combinedMaxDist;
            public float combinedLooseDist;

            internal void Disconnect(bool playSound = false)
            {
                if (main == null)
                {
                    DebugRandAddi.Assert(true, "ManTethers.TetherPair has unset tether relations.  This should not be possible.");
                    return;
                }
                if (playSound)
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimCrateUnlock);
                int error = 0;
                try
                {
                    main.Delink();
                    error++;
                    link.Delink();
                    error++;
                    var inst = main;
                    LinkedTethers.RemoveAll(x => x.main == inst);
                }
                catch
                {
                    DebugRandAddi.Log("ManTethers.TetherPair - error " + error);
                }
            }
        }


        internal static ManTethers inst;
        internal static TetherPair nullPair = new TetherPair();
        private static List<ModuleTechTether> ActiveTethers;
        private static ModuleTechTether SelectedTether;
        private static List<TetherPair> LinkedTethers;
        private static List<TetherPair> DeLinkedTethers = new List<TetherPair>();
        public static void Init()
        {
            if (inst)
                return;
            inst = Instantiate(new GameObject("ManTethers"), null).AddComponent<ManTethers>();
            ActiveTethers = new List<ModuleTechTether>();
            LinkedTethers = new List<TetherPair>();
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(inst.OnClick);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Singleton.Manager<ManPointer>.inst.MouseEvent.Unsubscribe(inst.OnClick);
            LinkedTethers = null;
            ActiveTethers = null;
            Destroy(inst);
            inst = null;
        }
        public void OnClick(ManPointer.Event mEvent, bool down, bool clicked)
        {
            if (mEvent == ManPointer.Event.LMB && down)
            {
                var targVis = Singleton.Manager<ManPointer>.inst.targetVisible?.block;
                if (SelectedTether)
                {
                    if (SelectedTether.block.IsAttached)
                    {
                        if (targVis && targVis.IsAttached)
                        {
                            var tether = Singleton.Manager<ManPointer>.inst.targetVisible.block.GetComponent<ModuleTechTether>();
                            if (tether && tether != SelectedTether)
                            {
                                SelectedTether.ConnectToOther(tether);
                                ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.AnimGSODeliCannonMob);
                            }
                        }
                    }
                    foreach (var item in ActiveTethers)
                    {
                        item.block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                    }
                    SelectedTether = null;
                }
                else if (targVis && targVis.IsAttached)
                {
                    var tether = Singleton.Manager<ManPointer>.inst.targetVisible.block.GetComponent<ModuleTechTether>();
                    if (tether)
                    {
                        if (tether.IsConnected)
                        {
                            tether.Connection.Disconnect(true);
                            return;
                        }
                        SelectedTether = tether;
                        foreach (var item in ActiveTethers)
                        {
                            if (item.tank != SelectedTether.tank)
                            {
                                item.block.visible.EnableOutlineGlow(true, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update the tether physics
        /// </summary>
        private void FixedUpdate()
        {
            foreach (var item in LinkedTethers)
            {
                if (item.main?.tank && item.link?.tank)
                {
                    Vector3 spacing = item.main.tetherEnd.position - item.link.tetherEnd.position;
                    float Dist = spacing.sqrMagnitude;
                    if (Dist > item.combinedMaxDist)
                    {
                        DeLinkedTethers.Add(item);
                    }
                    else if (Dist > item.combinedLooseDist)
                    {
                        float pullForce = item.combinedTension * Mathf.InverseLerp(item.combinedLooseDist, item.combinedMaxDist, Dist);

                        if (item.main.tank.rbody)
                            item.main.tank.rbody.AddForceAtPosition(-spacing.normalized * pullForce, item.main.tetherPoint.position, ForceMode.Acceleration);
                        if (item.link.tank.rbody)
                            item.link.tank.rbody.AddForceAtPosition(spacing.normalized * pullForce, item.link.tetherPoint.position, ForceMode.Acceleration);
                    }
                    // no force applied
                }
                else
                {
                    DebugRandAddi.Log("Invalid entry in LinkedTethers!");
                    DeLinkedTethers.Add(item);
                }
            }
            foreach (var item in DeLinkedTethers)
            {
                item.Disconnect(true);
            }
            DeLinkedTethers.Clear();
        }



        // Note that this module is merely a suggestion - Can break if enough force is applied 
        [AutoSaveComponent]
        public class ModuleTechTether : ExtModule
        {
            public float SpringTensionForce = 750;
            public float MaxDistance = 8;
            public float LooseDistance = 4;
            public float TetherScale = 0.4f;
            public bool RelayControls = false;

            public Material BeamMaterial = null;
            public Color BeamColorStart = new Color(0.05f, 0.1f, 1f, 0.8f);
            public Color BeamColorEnd = new Color(0.05f, 0.1f, 1f, 0.8f);

            private ModuleTechTether Connected;

            public TetherPair Connection;
            public bool otherConnector => Connected;
            public bool IsConnected => Connected;

            [SSaveField]
            public int lastConnectedID = -1;
            [SSaveField]
            public int lastConnectedBlockPos = -1;

            internal Transform tetherPoint;
            internal Transform tetherEnd;
            private TargetAimer TargetAimer;

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
                    tetherEnd = KickStart.HeavyObjectSearch(transform, "_Target");
                }
                catch { }
                if (tetherEnd != null)
                {
                    TargetAimer = GetComponent<TargetAimer>();
                    if (TargetAimer == null)
                    {
                        //LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a valid TargetAimer in hierarchy!\nCause of error - Block " + gameObject.name);
                        //return;
                    }
                    InitTracBeam();
                }
                else
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTechTether NEEDS a GameObject in hierarchy named \"_Target\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }

            public override void OnAttach()
            {
                ExtUsageHint.ShowExistingHint(4010);
                ActiveTethers.Add(this);
            }
            public override void OnDetach()
            {
                ActiveTethers.Remove(this);
                block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
                if (Connection != null)
                    Connection.Disconnect(true);
            }

            internal void ConnectToOther(ModuleTechTether tether)
            {
                if (!Connected)
                {
                    if (tether == this)
                    {
                        DebugRandAddi.Log("ModuleTechTether attempted to connect to itself");
                        return;
                    }
                    if (!tether.Connected)
                    {
                        tether.Connected = this;
                        Connected = tether;
                        BeamSide = true;
                        TetherPair NewConnection = new TetherPair
                        {
                            main = this,
                            link = tether,
                            combinedTension = (SpringTensionForce + tether.SpringTensionForce) / 2,
                            combinedLooseDist = Mathf.Pow(LooseDistance + tether.LooseDistance, 2),
                            combinedMaxDist = Mathf.Pow(MaxDistance + tether.MaxDistance, 2)
                        };
                        LinkedTethers.Add(NewConnection);
                        Connection = NewConnection;
                        tether.Connection = NewConnection;
                        DebugRandAddi.Info("ModuleTechTether attached");
                    }
                }
            }
            internal void Delink()
            {
                if (Connected)
                {
                    BeamSide = false;
                    Connected = null;
                    Connection = nullPair;
                    StopBeam();
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
                            lastConnectedID = Connected.tank.visible.ID;
                            int countPos = 0;
                            foreach (var item in Connected.tank.blockman.IterateBlocks())
                            {
                                var comp = item.GetComponent<ModuleTechTether>();
                                if (comp && comp == Connected)
                                {
                                    lastConnectedBlockPos = countPos;
                                    this.SerializeToSafe();
                                    break;
                                }
                                countPos++;
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
                            if (!Connected && this.DeserializeFromSafe())
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
                if (!Connected && lastConnectedID != -1)
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
                                        ConnectToOther(comp);
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
            private void Update()
            {
                if (canUseBeam)
                {
                    if (Connected)
                    {
                        if (TargetAimer)
                            TargetAimer.AimAtWorldPos(Connected.tetherEnd.position, 360);
                        if (BeamSide)
                        {
                            StartBeam();
                            UpdateTracBeam();
                        }
                        else
                        {
                            StopBeam();
                        }
                    }
                    else
                    {
                        StopBeam();
                        if (TargetAimer)
                        {
                            Vector3 defaultAim = tetherEnd.position + (25 * block.trans.forward);
                            TargetAimer.AimAtWorldPos(defaultAim, 360);
                        }
                    }
                }
            }


            private LineRenderer TracBeamVis;
            private bool canUseBeam = false;
            private bool BeamSide = false;
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
                TracBeamVis.startColor = new Color(0.25f, 1, 0.25f, 0.9f);
                TracBeamVis.endColor = new Color(0.1f, 1, 0.1f, 0.9f);
                TracBeamVis.positionCount = 2;
                TracBeamVis.SetPositions(new Vector3[2] { tetherEnd.position, Connected.tetherEnd.position });
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
