using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using TerraTechETCUtil;
using SafeSaves;

namespace RandomAdditions
{
    public enum PhysicsClampMode : int
    {
        Never = 0,
        AnyMovement = 1,
        OwnMovement = 2,
        AttachedMovement = 3,
    }

    public class ManPhysicsExt
    {
        public class ModulePhysicsSetMessage : MessageBase
        {
            public ModulePhysicsSetMessage() { }
            public ModulePhysicsSetMessage(ModulePhysicsExt main, int mode)
            {
                blockID = main.block.GetBlockIndexAndTechNetID(out techID);
                this.mode = mode;
            }

            public uint techID;
            public int blockID;
            public int mode;
        }
        internal static NetworkHook<ModulePhysicsSetMessage> netHook = new NetworkHook<ModulePhysicsSetMessage>(
                "RandAdd.ModulePhysicsSetMessage", OnReceiveSetRequest, NetMessageType.FromClientToServerThenClients);

        public class ModulePhysicsUnlockMessage : MessageBase
        {
            public ModulePhysicsUnlockMessage() { }
            public ModulePhysicsUnlockMessage(ModulePhysicsExt main)
            {
                blockID = main.block.GetBlockIndexAndTechNetID(out techID);
            }

            public uint techID;
            public int blockID;
        }
        internal static NetworkHook<ModulePhysicsUnlockMessage> netHookUnlock = new NetworkHook<ModulePhysicsUnlockMessage>(
                "RandAdd.ModulePhysicsUnlockMessage", OnReceiveUnlockRequest, NetMessageType.FromClientToServerThenClients);

        internal static readonly List<ModulePhysicsExt> veryLateUpdate = new List<ModulePhysicsExt>();

        private static bool ready = false;
        internal static void InsureInit()
        {
            if (ready)
                return;
            ready = true;
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(PrePostWheelCheck), -99);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, new Action(PostPostWheelCheck), -101);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, new Action(PostPostUpdate), -101);
        }

        private static bool OnReceiveSetRequest(ModulePhysicsSetMessage command, bool isServer)
        {
            if (command.GetBlockModuleOnTech<ModulePhysicsExt>(command.techID, command.blockID, out var module))
            {
                module.OnSetMode(command.mode);
                return true;
            }
            return false;
        }
        private static bool OnReceiveUnlockRequest(ModulePhysicsUnlockMessage command, bool isServer)
        {
            if (command.GetBlockModuleOnTech<ModulePhysicsExt>(command.techID, command.blockID, out var module))
            {
                module.DoUnlock();
                return true;
            }
            return false;
        }

        internal static void PrePostWheelCheck()
        {
            
        }
        internal static void PostPostWheelCheck()
        {
        }
        internal static void PostPostUpdate()
        {
            foreach (var item in veryLateUpdate)
            {
                item.UpdateLateVisuals();
            }
        }
    }
    [AutoSaveComponent]
    public class ModulePhysicsExt : ExtModule
    {
        internal static MethodInfo anc = typeof(TechAudio).GetMethod("OnTankAnchor", BindingFlags.NonPublic | BindingFlags.Instance);
        protected const float IgnoreImpulseBelow = 6;
        protected const float IgnoreAttachDelay = 1.4f;
        protected static PhysicsLock phyLock = new PhysicsLock();

        public float BreakingForce = 100000;
        public float BreakingTorque = 10000;

        private ModuleUIButtons buttonGUI = null;
        [SSaveField]
        public int LockMode = (int)PhysicsClampMode.Never;

        protected float IgnoreAttachTime = 0;
        protected HashSet<Collider> physLockCol = new HashSet<Collider>();
        protected bool LockedToTech = false;

        private Transform LockFootVisualTrans = null;
        private Transform LockFootTrans = null;
        private Tank lockedTech = null;

        // Logic
        private bool LogicConnected = false;

        protected override void Pool()
        {
            InsureGUI();
            LockFootVisualTrans = KickStart.HeavyTransformSearch(transform, "_anchorEndVisual");
            LockFootTrans = KickStart.HeavyTransformSearch(transform, "_anchorEnd");
            if (LockFootTrans == null)
                LockFootTrans = transform;
            phyLock.PistonBreak = BreakingForce;
            phyLock.RotorBreak = BreakingTorque;
        }

        private static LocExtStringMod LOC_LockSettings = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Lock Settings" },
            { LocalisationEnums.Languages.Japanese, "設定の編集をロックする" },
        });

        private static LocExtStringMod LOC_UnlockConnection = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Unlock" },
            { LocalisationEnums.Languages.Japanese, "ロックを解除する" },
        });

        public void InsureGUI()
        {
            if (buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Tech Physics Joint", true);
                buttonGUI.AddElement(LOC_LockSettings, OnSetMode, OnIconSet, GetMode);
                buttonGUI.AddElement(LOC_UnlockConnection, OnUnlock, OnIconUnlock);
            }
        }

        private Sprite OnIconUnlock()
        {
            return UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");
        }
        private float OnUnlock(float set)
        {
            DoUnlock();
            IgnoreAttachTime = IgnoreAttachDelay;
            return 1;
        }
        private Sprite OnIconSet()
        {
            return UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");
        }
        private float OnSetMode(float valueF)
        {
            if (float.IsNaN(valueF))
                return LockMode / Enum.GetValues(typeof(PhysicsClampMode)).Length;
            int value = Mathf.RoundToInt(valueF * (Enum.GetValues(typeof(PhysicsClampMode)).Length - 1));
            if (LockMode != value)
            {
                TrySetMode(value);
            }
            return (float)LockMode / Enum.GetValues(typeof(PhysicsClampMode)).Length;
        }

        private void TrySetMode(int value)
        {
            if (ManPhysicsExt.netHook.CanBroadcast())
                ManPhysicsExt.netHook.TryBroadcast(new ManPhysicsExt.ModulePhysicsSetMessage(this, value));
            else
                OnSetMode(value);
        }
        internal void OnSetMode(int value)
        {
            ResetSetMode(LockMode);
            LockMode = value;
            //DebugRandAddi.Log("ModulePhysicsExt set to " + value);
            AttachSetMode();
        }
        private void ResetSetMode(int previous)
        {
            switch (previous)
            {
                case (int)PhysicsClampMode.AnyMovement:
                    tank.control.driveControlEvent.Unsubscribe(OnInput);
                    if (lockedTech)
                        lockedTech.control.driveControlEvent.Unsubscribe(OnInput);
                    break;
                case (int)PhysicsClampMode.OwnMovement:
                    tank.control.driveControlEvent.Unsubscribe(OnInput);
                    break;
                case (int)PhysicsClampMode.AttachedMovement:
                    if (lockedTech)
                        lockedTech.control.driveControlEvent.Unsubscribe(OnInput);
                    break;
            }
        }
        private void AttachSetMode()
        {
            switch (LockMode)
            {
                case (int)PhysicsClampMode.AnyMovement:
                    tank.control.driveControlEvent.Subscribe(OnInput);
                    if (lockedTech)
                        lockedTech.control.driveControlEvent.Subscribe(OnInput);
                    break;
                case (int)PhysicsClampMode.OwnMovement:
                    tank.control.driveControlEvent.Subscribe(OnInput);
                    break;
                case (int)PhysicsClampMode.AttachedMovement:
                    if (lockedTech)
                        lockedTech.control.driveControlEvent.Subscribe(OnInput);
                    break;
            }
        }
        private void RefreshSetMode()
        {
            try
            {
                ResetSetMode(LockMode);
            }
            catch { }
            try
            {
                AttachSetMode();
            }
            catch { }
        }

        internal string GetMode()
        {
            return ((PhysicsClampMode)LockMode).ToString();
        }

        public override void OnAttach()
        {
            InsureGUI();
            //DebugRandAddi.Log("OnAttach");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, false, false);
                }
            }
            enabled = true;
            block.serializeEvent.Subscribe(OnSaveSerialization);
            block.serializeTextEvent.Subscribe(OnTechSnapSerialization);
            tank.CollisionEvent.Subscribe(OnCollision);
            tank.control.explosiveBoltDetonateEvents[3].Subscribe(OnBolt);
            phyLock.AttachedEvent.Subscribe(OnStick);
            phyLock.DetachedEvent.Subscribe(OnUnStick);
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach");
            phyLock.DetachedEvent.Unsubscribe(OnUnStick);
            phyLock.AttachedEvent.Unsubscribe(OnStick);
            block.serializeTextEvent.Unsubscribe(OnTechSnapSerialization);
            block.serializeEvent.Unsubscribe(OnSaveSerialization);
            enabled = false;
            tank.control.driveControlEvent.Unsubscribe(OnInput);
            tank.control.explosiveBoltDetonateEvents[3].Unsubscribe(OnBolt);
            if (LogicConnected)
                ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, true, false);
            LogicConnected = false;
            DoUnlock();
            tank.CollisionEvent.Unsubscribe(OnCollision);
        }

        public void OnInput(TankControl.ControlState control)
        {
            switch (LockMode)
            {
                case 0:
                    if (control.AnyMovementControl)
                    {
                        IgnoreAttachTime = Time.time + IgnoreAttachDelay;
                        if (phyLock.IsAttached)
                            DoUnlock();
                    }
                    break;
                default:
                    break;
            }
        }
        public void OnBolt()
        {
            IgnoreAttachTime = Time.time + IgnoreAttachDelay;
            DoUnlock();
        }

        public void AnchorsChanged(ModuleAnchor u, bool u2, bool u3)
        {
            DoUnlock();
            IgnoreAttachTime = 0;
        }
        public void OnRecCharge(Circuits.BlockChargeData charge)
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            try
            {
                if (charge.ChargeStrength > 0)
                    DoUnlock();
            }
            catch { }
        }



        public Vector3 GetAnchorPos(Transform trans, Vector3 offset)
        {
            return trans.root.InverseTransformPoint(trans.TransformPoint(offset));
        }
        public Quaternion GetAnchorRotWORLD(Transform trans, Quaternion offset)
        {
            return trans.rotation * offset;
        }

        public virtual void UpdateLateVisuals()
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            if (LockFootVisualTrans != null)
            {
                return;
            }
            LockFootVisualTrans.localPosition = Vector3.zero;
            LockFootVisualTrans.localRotation = Quaternion.identity;
        }

        protected void PlaySound(bool attached)
        {
            try
            {
                anc.Invoke(tank.TechAudio, new object[2] { attached, true });
            }
            catch { }
        }

        protected void OnStick(Transform transIn)
        {
            PlaySound(true);
            lockedTech = transIn.root.GetComponent<Tank>();
            RefreshSetMode();
        }
        protected void OnUnStick()
        {
            PlaySound(false);
            ResetSetMode(LockMode);
            lockedTech = null;
        }

        protected bool ShouldDetach()
        {
            switch (LockMode)
            {
                case 0:
                    return tank.control.TestAnyControl() || (lockedTech != null && lockedTech.control.TestAnyControl());
                default:
                    return false;
            }
        }

        protected void TryUnlock(Tank tankO = null)
        {
            if (ManPhysicsExt.netHookUnlock.CanBroadcast())
                ManPhysicsExt.netHookUnlock.TryBroadcast(new ManPhysicsExt.ModulePhysicsUnlockMessage(this));
            else
                DoUnlock();
        }

        public void OnCollision(Tank.CollisionInfo collide, Tank.CollisionInfo.Event whack)
        {
            try
            {
                if (whack == Tank.CollisionInfo.Event.NonAttached || IgnoreAttachTime > Time.time)
                    return;
                Tank.CollisionInfo.Obj thisC;
                Tank.CollisionInfo.Obj other;
                if (collide.a.tank == tank)
                {
                    thisC = collide.a;
                    other = collide.b;
                }
                else
                {
                    other = collide.a;
                    thisC = collide.b;
                }
                if (!physLockCol.Contains(thisC.collider))
                    return;
                if (whack == Tank.CollisionInfo.Event.Enter && !phyLock.IsAttached)
                {
                    WorldPosition WP = WorldPosition.FromScenePosition(collide.point);
                    phyLock.Attach(WP, thisC.collider, WP, other.collider);
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("Whoops - ModulePhysics " + e);
            }
        }

        public void FixedUpdateRelationToTargetTrans()
        {
            if (phyLock.IsAttached)
            {
            }
        }


        internal void DoUnlock(Tank tankO = null)
        {
            DoUnlock_Internal();
        }
        internal virtual void DoUnlock_Internal()
        {
        }

        protected void ReallyUnlock()
        {
            if (phyLock.IsAttached)
            {
                if (ManNetwork.IsNetworked)
                    DebugRandAddi.Log("ModulePhysicsExt - unlock local on NETWORK");
                phyLock.Detach();
            }
        }

        public class SerialData : Module.SerialData<SerialData>
        {
            public int lastMode;
        }

        protected void OnSaveSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            SerialData SD;
            if (Saving)
            {
                SD = new SerialData() { lastMode = LockMode };
                SD.Store(spec.saveState);
            }
            else
            {
                SD = SerialData.Retrieve(spec.saveState);
                if (SD != null)
                {
                    LockMode = SD.lastMode;
                }
            }
        }
        protected void OnTechSnapSerialization(bool Saving, TankPreset.BlockSpec spec, bool tankPresent)
        {
            if (!tankPresent)
                return;
            if (Saving)
            {
                spec.Store(GetType(), "type", LockMode.ToString());
            }
            else
            {
                try
                {
                    LockMode = int.Parse(spec.Retrieve(GetType(), "type"));
                }
                catch
                {
                    DebugRandAddi.Assert("RandomAdditions: ModuleRailJunction - Unable to deserialize(String)!");
                }
            }
        }

        protected class RAPhysicsAnchor : MonoBehaviour, IWorldTreadmill
        {
            private static RAPhysicsAnchor prefab;

            internal Rigidbody rbody;

            public static void FirstInit()
            {
                if (prefab != null)
                    return;
                var GO = new GameObject("MaglockAnchor");
                GO.layer = Globals.inst.layerCosmetic;
                prefab = GO.AddComponent<RAPhysicsAnchor>();
                GO.AddComponent<MeshFilter>();
                GO.AddComponent<MeshRenderer>();
                var SC = GO.AddComponent<SphereCollider>();
                SC.radius = 0.2f;
                SC.material = new PhysicMaterial();
                var rb = GO.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.None;
                GO.SetActive(false);
                prefab.CreatePool(1);
            }
            public static RAPhysicsAnchor InitNew(ExtModule inst, Vector3 contactPoint)
            {
                FirstInit();
                var MTA = prefab.Spawn();
                return MTA.Init(inst, contactPoint);
            }
            public RAPhysicsAnchor Init(ExtModule inst, Vector3 contactPoint)
            {
                rbody = GetComponent<Rigidbody>();
                transform.position = contactPoint;
                transform.rotation = inst.tank.transform.rotation;
                rbody.freezeRotation = true;
                rbody.mass = 9001;
                rbody.constraints = RigidbodyConstraints.FreezeAll;
                try
                {
                    GetComponent<MeshFilter>().sharedMesh = inst.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                    GetComponent<MeshRenderer>().sharedMaterial = inst.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                }
                catch { }
                ManWorldTreadmill.inst.AddListener(this);
                return this;
            }
            public void DeInit()
            {
                ManWorldTreadmill.inst.RemoveListener(this);
                this.Recycle();
            }

            public void OnMoveWorldOrigin(IntVector3 move)
            {
                transform.position += move;
            }
            public void OnRecycle()
            {

            }
        }
    }

    /*
    [AutoSaveComponent]
    public class ModulePhysicsExt_Legacy : ExtModule
    {
        internal static MethodInfo anc = typeof(TechAudio).GetMethod("OnTankAnchor", BindingFlags.NonPublic | BindingFlags.Instance);
        protected const float IgnoreImpulseBelow = 6;
        protected const float IgnoreAttachDelay = 1.4f;

        public float BreakingForce = 100000;
        public float BreakingTorque = 10000;

        private ModuleUIButtons buttonGUI = null;
        [SSaveField]
        public int LockMode = (int)PhysicsClampMode.AlwaysLock;

        protected Joint jointLock = null;
        protected RAPhysicsAnchor phyAnchor = null;
        protected float IgnoreAttachTime = 0;
        protected HashSet<Collider> physLockCol = new HashSet<Collider>();
        protected bool LockedToTech = false;
        protected Collider lockedCol = null;
        protected Transform lockedTrans => lockedCol.transform;
        protected Tank lockedTech = null;
        protected Transform LockFootVisualTrans = null;
        protected Transform LockFootTrans = null;
        protected Vector3 LockPosOffset = Vector3.zero;
        protected Quaternion LockRotOffset = Quaternion.identity;

        // Logic
        private bool LogicConnected = false;

        protected override void Pool()
        {
            InsureGUI();
            LockFootVisualTrans = KickStart.HeavyTransformSearch(transform, "_anchorEndVisual");
            LockFootTrans = KickStart.HeavyTransformSearch(transform, "_anchorEnd");
            if (LockFootTrans == null)
                LockFootTrans = transform;
        }

        public void InsureGUI()
        {
            if (buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Tech Physics Joint", true);
                buttonGUI.AddElement("Lock Settings", OnSetMode, OnIconSet, GetMode);
                buttonGUI.AddElement("Unlock", OnUnlock, OnIconUnlock);
            }
        }

        private Sprite OnIconUnlock()
        {
            return UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");
        }
        private float OnUnlock(float set)
        {
            DoUnlock();
            IgnoreAttachTime = IgnoreAttachDelay;
            return 1;
        }
        private Sprite OnIconSet()
        {
            return UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");
        }
        private float OnSetMode(float valueF)
        {
            if (float.IsNaN(valueF))
                return LockMode;
            int value = Mathf.RoundToInt(valueF * (Enum.GetValues(typeof(PhysicsClampMode)).Length - 1));
            if (LockMode != value)
            {
                TrySetMode(value);
            }
            return (float)LockMode / Enum.GetValues(typeof(PhysicsClampMode)).Length;
        }

        private void TrySetMode(int value)
        {
            if (ManPhysicsExt.netHook.CanBroadcast())
                ManPhysicsExt.netHook.TryBroadcast(new ManPhysicsExt.ModulePhysicsSetMessage(this, value));
            else
                OnSetMode(value);
        }
        internal void OnSetMode(int value)
        {
            LockMode = value;
        }

        internal string GetMode()
        {
            return ((PhysicsClampMode)LockMode).ToString();
        }

        public override void OnAttach()
        {
            InsureGUI();
            //DebugRandAddi.Log("OnAttach");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    block.CircuitNode?.Receiver.FrameChargeChangedEvent.Subscribe(OnRecCharge);
                }
            }
            enabled = true;
            block.serializeEvent.Subscribe(OnSaveSerialization);
            block.serializeTextEvent.Subscribe(OnTechSnapSerialization);
            tank.CollisionEvent.Subscribe(OnCollision);
            tank.control.explosiveBoltDetonateEvents[3].Subscribe(OnBolt);
            tank.control.driveControlEvent.Subscribe(OnInput);
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach");
            block.serializeTextEvent.Unsubscribe(OnTechSnapSerialization);
            block.serializeEvent.Unsubscribe(OnSaveSerialization);
            enabled = false;
            tank.control.driveControlEvent.Unsubscribe(OnInput);
            tank.control.explosiveBoltDetonateEvents[3].Unsubscribe(OnBolt);
            if (LogicConnected)
                block.CircuitNode.Receiver.FrameChargeChangedEvent.Unsubscribe(OnRecCharge);
            LogicConnected = false;
            DoUnlock();
            tank.CollisionEvent.Unsubscribe(OnCollision);
        }

        public void OnInput(TankControl.ControlState control)
        {
            if (control.AnyMovementOrBoostControl)
            {
                IgnoreAttachTime = Time.time + IgnoreAttachDelay;
                if (phyAnchor && jointLock)
                    DoUnlock();
            }
        }
        public void OnBolt(TechSplitNamer un)
        {
            IgnoreAttachTime = Time.time + IgnoreAttachDelay;
            DoUnlock();
        }

        public void AnchorsChanged(ModuleAnchor u, bool u2, bool u3)
        {
            DoUnlock();
            IgnoreAttachTime = 0;
        }
        public void OnRecCharge(Circuits.Charge charge)
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            try
            {
                if (charge.HighestChargeStrengthFromHere > 0)
                    DoUnlock();
            }
            catch { }
        }



        public Vector3 GetAnchorPos(Transform trans, Vector3 offset)
        {
            return trans.root.InverseTransformPoint(trans.TransformPoint(offset));
        }
        public Quaternion GetAnchorRotWORLD(Transform trans, Quaternion offset)
        {
            return trans.rotation * offset;
        }

        public virtual void UpdateLateVisuals()
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            if (LockFootVisualTrans != null)
            {
                if (TryGetAnchorPosWorld(out var v1, out var v2))
                {
                    LockFootVisualTrans.position = v1;
                    LockFootVisualTrans.rotation = v2;
                    return;
                }
            }
            LockFootVisualTrans.localPosition = Vector3.zero;
            LockFootVisualTrans.localRotation = Quaternion.identity;
        }

        protected void SaveAnchorPos(Vector3 scenePos)
        {
            if (lockedCol)
            {
                LockPosOffset = lockedTrans.InverseTransformPoint(scenePos);
                LockRotOffset = lockedTrans.rotation * Quaternion.Inverse(LockFootTrans.rotation);
            }
        }
        protected bool TryGetAnchorPosWorld(out Vector3 vec, out Quaternion rot)
        {
            if (lockedCol)
            {
                vec = lockedCol.transform.TransformPoint(LockPosOffset);
                rot = lockedCol.transform.rotation * LockRotOffset;
                return true;
            }
            vec = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }


        public void FixedUpdateRelationToTargetTrans()
        {
            if (jointLock != null && lockedCol != null)
            {
                var rbodyTarg = tank.GetComponent<Rigidbody>();
                if (rbodyTarg)
                {
                    var deltaQuat = Quaternion.RotateTowards(LockFootTrans.rotation,
                        GetAnchorRotWORLD(LockFootTrans, Quaternion.identity), 45);
                    rbodyTarg.MoveRotation(deltaQuat);
                }
                var rbodyTarg2 = lockedTrans.root.GetComponent<Rigidbody>();
                if (rbodyTarg2)
                {
                    var deltaQuat = Quaternion.RotateTowards(LockFootTrans.rotation,
                        GetAnchorRotWORLD(lockedTrans, LockRotOffset), 45);
                    rbodyTarg2.MoveRotation(deltaQuat);
                }
                jointLock.anchor = GetAnchorPos(LockFootTrans, Vector3.zero);
                jointLock.connectedAnchor = GetAnchorPos(lockedTrans, LockPosOffset);
            }
        }

        protected void PlaySound(bool attached)
        {
            try
            {
                anc.Invoke(tank.TechAudio, new object[2] { attached, true });
            }
            catch { }
        }

        protected bool ShouldDetach()
        {
            if (tank.rbody == null && lockedTech?.rbody == null)
                return true;
            else if (phyAnchor && (tank.beam.IsActive || lockedCol == null || !lockedCol.gameObject.activeInHierarchy))
                return true;
            switch (LockMode)
            {
                case 1:
                    return tank.control.TestAnyControl() || (lockedTech != null && lockedTech.control.TestAnyControl());
                default:
                    return false;
            }
        }

        protected void TryUnlock(Tank tankO = null)
        {
            if (ManPhysicsExt.netHookUnlock.CanBroadcast())
                ManPhysicsExt.netHookUnlock.TryBroadcast(new ManPhysicsExt.ModulePhysicsUnlockMessage(this));
            else
                DoUnlock();
        }

        public void OnCollision(Tank.CollisionInfo collide, Tank.CollisionInfo.Event whack)
        {
            try
            {
                if (whack == Tank.CollisionInfo.Event.NonAttached || IgnoreAttachTime > Time.time)
                    return;
                Tank.CollisionInfo.Obj thisC;
                Tank.CollisionInfo.Obj other;
                if (collide.a.tank == tank)
                {
                    thisC = collide.a;
                    other = collide.b;
                }
                else
                {
                    other = collide.a;
                    thisC = collide.b;
                }
                if (!physLockCol.Contains(thisC.collider))
                    return;
                if (whack == Tank.CollisionInfo.Event.Enter && jointLock == null)
                {
                    if (other.tank)
                    {
                        PlaySound(true);
                        LockTank_Internal(other.collider, other.tank, LockFootTrans.position);
                        return;
                    }
                    else if (other.collider.transform.root.gameObject.name == "_GameManager")
                    {
                        PlaySound(true);
                        if (!tank.CanRunPhysics())
                            return;
                        LockTerrain_Internal(other.collider, LockFootTrans.position);
                        return;
                    }
                }
                if (jointLock)
                {
                    if (other.tank == lockedTech && tank.rbody != null && collide.impulse.WithinBox(IgnoreImpulseBelow))
                    {
                        Vector3 impulse;
                        if (Vector3.Dot(collide.normal, collide.impulse) >= 0)
                            impulse = -collide.impulse;
                        else
                            impulse = collide.impulse;
                        if (tank.CanRunPhysics())
                            tank.rbody.AddForceAtPosition(-impulse, LockFootTrans.position, ForceMode.Force);
                        if (other.tank.CanRunPhysics())
                            other.tank.rbody.AddForceAtPosition(impulse, LockFootTrans.position, ForceMode.Force);
                    }
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.Log("Whoops - ModulePhysics " + e);
            }
        }
        protected virtual Joint InitJoint(Collider col, Vector3 contactWorld)
        {
            return tank.gameObject.AddComponent<FixedJoint>();
        }

        private void LockTank_Internal(Collider col, Tank toLock, Vector3 contactWorld)
        {
            if ((!toLock.rbody && !tank.rbody) || jointLock)
                return;
            if (toLock.rbody)
            {   // host tech may or many not have rigidbody
                lockedCol = col;
                SaveAnchorPos(contactWorld);
                lockedTech = toLock;
                jointLock = InitJoint(col, contactWorld);
                jointLock.anchor = tank.trans.InverseTransformPoint(contactWorld);
                jointLock.connectedBody = toLock.rbody;
                jointLock.connectedAnchor = toLock.trans.InverseTransformPoint(contactWorld);
                SetupPhysJoint_Internal();
                DebugRandAddi.Log("Locked " + tank.name + " to " + toLock.name);
                LockPostSubTank(toLock);
            }
            else if (tank.rbody)
            {   // other tech does NOT have rigidbody and this side does!
                lockedCol = col;
                SaveAnchorPos(contactWorld);
                phyAnchor = RAPhysicsAnchor.InitNew(this, contactWorld);
                LockToFixed(phyAnchor.rbody, col, contactWorld);
                LockPostSubTank(toLock);
            }
            LockTank(col, toLock, contactWorld);
        }
        protected virtual void LockTank(Collider col, Tank toLock, Vector3 contactWorld)
        {
        }
        private void LockTerrain_Internal(Collider col, Vector3 contactWorld)
        {
            if (!tank.rbody || jointLock)
                return;
            lockedCol = col;
            SaveAnchorPos(contactWorld);
            phyAnchor = RAPhysicsAnchor.InitNew(this, contactWorld);
            LockToFixed(phyAnchor.rbody, col, contactWorld);
            LockTerrain(col, contactWorld);
        }
        protected virtual void LockTerrain(Collider col, Vector3 contactWorld)
        {
        }
        private void LockToFixed(Rigidbody toLock, Collider col, Vector3 contactWorld)
        {
            jointLock = InitJoint(col, contactWorld);
            jointLock.anchor = tank.transform.InverseTransformPoint(contactWorld);
            jointLock.connectedBody = toLock;
            jointLock.connectedAnchor = toLock.transform.InverseTransformPoint(contactWorld);
            SetupPhysJoint_Internal();
            DebugRandAddi.Log("Locked " + tank.name + " to " + toLock.name + " with Joint");
        }
        private void LockPostSubTank(Tank toLock)
        {
            lockedTech = toLock;
            toLock.TankRecycledEvent.Subscribe(DoUnlock);
            toLock.AnchorEvent.Subscribe(AnchorsChanged);
        }

        private void SetupPhysJoint_Internal()
        {
            if (ManNetwork.IsNetworked)
                DebugRandAddi.Log("ModulePhysicsExt - lock local on NETWORK");
            jointLock.breakForce = BreakingForce;
            jointLock.breakTorque = BreakingTorque;
            jointLock.massScale = 1;
            jointLock.axis = Vector3.up;
            jointLock.connectedMassScale = 1;
            jointLock.autoConfigureConnectedAnchor = false;
            jointLock.enableCollision = true;
            jointLock.enablePreprocessing = false;
            SetupPhysJoint();
        }
        protected virtual void SetupPhysJoint()
        {
        }


        internal void DoUnlock(Tank tankO = null)
        {
            DoUnlock_Internal();
        }
        internal virtual void DoUnlock_Internal()
        {
        }

        protected void ReallyUnlock()
        {
            if (LockedToTech)
            {
                if (lockedTech)
                {
                    lockedTech.AnchorEvent.Unsubscribe(AnchorsChanged);
                    lockedTech.TankRecycledEvent.Unsubscribe(DoUnlock);
                }
                else if (!ManNetwork.IsNetworked)
                    throw new NullReferenceException("(Non-network) Unlock_Internal expects lockedTech if LockedToTech is true, but it was NULL");
                LockedToTech = false;
            }
            if (phyAnchor != null)
            {
                phyAnchor.DeInit();
                phyAnchor = null;
                lockedCol = null;
            }
            if (jointLock)
            {
                if (ManNetwork.IsNetworked)
                    DebugRandAddi.Log("ModulePhysicsExt - unlock local on NETWORK");
                Destroy(jointLock);
            }
            lockedTech = null;
            jointLock = null;
            PlaySound(false);
        }

        public class SerialData : Module.SerialData<SerialData>
        {
            public int lastMode;
        }

        protected void OnSaveSerialization(bool Saving, TankPreset.BlockSpec spec)
        {
            SerialData SD;
            if (Saving)
            {
                SD = new SerialData() { lastMode = LockMode };
                SD.Store(spec.saveState);
            }
            else
            {
                SD = SerialData.Retrieve(spec.saveState);
                if (SD != null)
                {
                    LockMode = SD.lastMode;
                }
            }
        }
        protected void OnTechSnapSerialization(bool Saving, TankPreset.BlockSpec spec, bool tankPresent)
        {
            if (!tankPresent)
                return;
            if (Saving)
            {
                spec.Store(GetType(), "type", LockMode.ToString());
            }
            else
            {
                try
                {
                    LockMode = int.Parse(spec.Retrieve(GetType(), "type"));
                }
                catch
                {
                    DebugRandAddi.Assert("RandomAdditions: ModuleRailJunction - Unable to deserialize(String)!");
                }
            }
        }

        protected class RAPhysicsAnchor : MonoBehaviour, IWorldTreadmill
        {
            private static RAPhysicsAnchor prefab;

            internal Rigidbody rbody;

            public static void FirstInit()
            {
                if (prefab != null)
                    return;
                var GO = new GameObject("MaglockAnchor");
                GO.layer = Globals.inst.layerCosmetic;
                prefab = GO.AddComponent<RAPhysicsAnchor>();
                GO.AddComponent<MeshFilter>();
                GO.AddComponent<MeshRenderer>();
                var SC = GO.AddComponent<SphereCollider>();
                SC.radius = 0.2f;
                SC.material = new PhysicMaterial();
                var rb = GO.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.None;
                GO.SetActive(false);
                prefab.CreatePool(1);
            }
            public static RAPhysicsAnchor InitNew(ExtModule inst, Vector3 contactPoint)
            {
                FirstInit();
                var MTA = prefab.Spawn();
                return MTA.Init(inst, contactPoint);
            }
            public RAPhysicsAnchor Init(ExtModule inst, Vector3 contactPoint)
            {
                rbody = GetComponent<Rigidbody>();
                transform.position = contactPoint;
                transform.rotation = inst.tank.transform.rotation;
                rbody.freezeRotation = true;
                rbody.mass = 9001;
                rbody.constraints = RigidbodyConstraints.FreezeAll;
                try
                {
                    GetComponent<MeshFilter>().sharedMesh = inst.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                    GetComponent<MeshRenderer>().sharedMaterial = inst.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                }
                catch { }
                ManWorldTreadmill.inst.AddListener(this);
                return this;
            }
            public void DeInit()
            {
                ManWorldTreadmill.inst.RemoveListener(this);
                this.Recycle();
            }

            public void OnMoveWorldOrigin(IntVector3 move)
            {
                transform.position += move;
            }
            public void OnRecycle()
            {

            }
        }
    }
    */

}
