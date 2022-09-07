using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ModulePartWeaponBarrel : RandomAdditions.ModulePartWeaponBarrel { }
public class ModulePartWeaponDongle : RandomAdditions.ModulePartWeaponDongle { }
namespace RandomAdditions
{

    public abstract class ModulePartWeaponPart : ExtModule
    {
        public bool AlignWithController = false;
        public WeaponDongleType Type = WeaponDongleType.Payload;

        protected ModulePartWeapon assigned { get; set; }
        public ModulePartWeapon AssignedMPW => assigned;
        protected Transform VisualMesh;

        protected override void Pool()
        {
            VisualMesh = KickStart.HeavyObjectSearch(transform, "_MainMesh");
        }

        protected void AlignInternalMeshWithController(bool align)
        {
            if (AlignWithController && VisualMesh)
            {
                if (align && assigned)
                {
                    VisualMesh.rotation = assigned.transform.rotation;
                }
                else
                    VisualMesh.localRotation = Quaternion.identity;
            }
        }



        internal void RequestConnection(ModulePartWeapon MPW)
        {
            if (MPW != null)
            {
                if (MPW != assigned)
                {
                    if (assigned)
                        assigned.Disconnect(this);
                    MPW.Connect(this);
                    AlignInternalMeshWithController(true);
                }
            }
            else
            {
                if (assigned)
                    assigned.Disconnect(this);
                AlignInternalMeshWithController(false);
            }
        }

        private void DoConnection(ModulePartWeapon MPW)
        {
            if (MPW != null)
            {
                if (MPW != assigned)
                {
                    if (assigned)
                    {
                        DebugRandAddi.Assert("DoConnection was given a new ModulePartWeapon before removing previous");
                        DoConnectionRelay(MPW, false);
                        assigned.DoDisconnect(this);
                    }
                    assigned = MPW;
                    assigned.DoConnect(this);
                    DoConnectionRelay(MPW, true);
                    AlignInternalMeshWithController(true);
                }
            }
            else
            {
                DoConnectionRelay(null, false);
                if (assigned)
                {
                    assigned.Disconnect(this);
                    assigned.DoDisconnect(this);
                }
                assigned = null;
                AlignInternalMeshWithController(false);
            }
        }
        internal virtual void DoConnectionRelay(ModulePartWeapon MPW, bool connected)
        {
        }

        internal virtual void NotifyOthersConnect()
        {
            DebugRandAddi.Assert("NotifyOthersConnect was fired in the wrong instance");
        }

        internal virtual void OnDetachUpdateConnections()
        {
            DebugRandAddi.Assert("OnDetachUpdateConnections was fired in the wrong instance");
        }



        protected bool IsConnected<T>(out Event<bool, bool, ModulePartWeapon> eventCase) where T : ModulePartWeaponPart
        {
            eventCase = new Event<bool, bool, ModulePartWeapon>();
            bool success = RecurseCheckConnectedAll<T>(ref eventCase);
            DebugRandAddi.Log("Is still connected? " + success);
            return success;
        }
        internal bool IsConnectedToController<T>(bool updateIfDisconnected) where T : ModulePartWeaponPart
        {
            Event<bool, bool, ModulePartWeapon> eventCase = new Event<bool, bool, ModulePartWeapon>();
            bool success = RecurseCheckConnectedToControllerAll<T>(ref eventCase);
            eventCase.Send(true, updateIfDisconnected ? !success : false, null);
            eventCase.EnsureNoSubscribers();
            return success;
        }

        internal bool recursed = false;
        internal bool RecurseCheckConnectedAll<T>(ref Event<bool, bool, ModulePartWeapon> act) where T : ModulePartWeaponPart
        {
            recursed = true;
            act.Subscribe(SetConnectivity);
            bool connected = false;
            var neighboors = GetReachableConnections();
            if (neighboors != null)
            {
                //DebugRandAddi.Log("RecurseCheckConnectedAll has " + neighboors.Length + " neighboors");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module && module == assigned)
                    {
                        connected = true;
                    }
                    else
                    {
                        var module2 = item.GetComponent<T>();
                        if (module2 && !module2.recursed)
                        {
                            if (module2.RecurseCheckConnectedAll<T>(ref act))
                            {
                                connected = true;
                            }
                        }
                    }
                }
            }
            return connected;
        }
        internal bool RecurseCheckConnectedToControllerAll<T>(ref Event<bool, bool, ModulePartWeapon> act) where T : ModulePartWeaponPart
        {
            recursed = true;
            act.Subscribe(SetConnectivity);
            bool connected = false;
            var neighboors = GetReachableConnections();
            if (neighboors != null)
            {
                //DebugRandAddi.Log("RecurseCheckConnectedToControllerAll has " + neighboors.Length + " neighboors");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module && module == assigned)
                    {
                        connected = true;
                    }
                    else
                    {
                        var module2 = item.GetComponent<T>();
                        if (module2 && !recursed)
                        {
                            if (module2.RecurseCheckConnectedToControllerAll<T>(ref act))
                            {
                                connected = true;
                            }
                        }
                    }
                }
            }
            return connected;
        }
        internal ModulePartWeapon GetConnected<T>() where T : ModulePartWeaponPart
        {
            Event<bool, bool, ModulePartWeapon> act = new Event<bool, bool, ModulePartWeapon>();
            var MPW = RecurseGetConnected<T>(ref act);
            DebugRandAddi.Info("GetConnected called " + act.GetSubscriberCount());
            act.Send(true, false, null);
            act.EnsureNoSubscribers();
            return MPW;
        }
        private ModulePartWeapon RecurseGetConnected<T>(ref Event<bool, bool, ModulePartWeapon> act) where T : ModulePartWeaponPart
        {
            recursed = true;
            act.Subscribe(SetConnectivity);
            if (assigned)
                return assigned;
            var neighboors = GetReachableConnections();
            if (neighboors != null)
            {
                //DebugRandAddi.Log("RecurseGetConnected has " + neighboors.Length + " neighboors");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module)
                    {
                        return module;
                    }
                    else
                    {
                        var module2 = item.GetComponent<T>();
                        if (module2 && !module2.recursed)
                        {
                            var recurseFind = module2.RecurseGetConnected<T>(ref act);
                            if (recurseFind)
                                return recurseFind;
                        }
                    }
                }
            }
            return null;
        }

        internal void RecurseConnectivity<T>(ref Event<bool, bool, ModulePartWeapon> act) where T : ModulePartWeaponPart
        {
            recursed = true;
            act.Subscribe(SetConnectivity);
            var neighboors = GetReachableConnections();
            if (neighboors != null)
            {
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<T>();
                    if (module && !module.recursed)
                    {
                        module.RecurseConnectivity<T>(ref act);
                    }
                }
            }
        }

        internal virtual TankBlock[] GetReachableConnections()
        {
            return GetAllAttachedAPNeighboors();
        }

        /// <summary>
        /// CALL THIS WHEN CONNECTING/DISCONNECTING THIS FROM A ModulePartWeapon!
        /// </summary>
        internal void SetConnectivity(bool resetRecurse, bool UpdateConnection, ModulePartWeapon target)
        {
            if (resetRecurse)
                recursed = false;
            if (UpdateConnection)
                DoConnection(target);
        }
    }

    public abstract class ModulePartWeaponPart<T> : ModulePartWeaponPart where T : ModulePartWeaponPart
    {
        public override void OnAttach()
        {
            //HandleSearch();
            var temp = GetConnected<T>();
            if (temp)
            {
                temp.Connect(this);
            }
        }

        public override void OnDetach()
        {
            block.visible.EnableOutlineGlow(false, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
            AlignInternalMeshWithController(false);
            //OnDetachUpdateConnections();
            SetConnectivity(true, true, null);
        }


        /// <summary>
        /// Gets all of the adjacent ModulePartWeaponDongles to see if any is connected and if yes, relays
        ///  this connectivity to others that aren't
        /// </summary>
        private void HandleSearch()
        {
            if (assigned)
                return;
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                DebugRandAddi.Log("HandleSearch returned " + neighboors.Length + " results");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module)
                    {
                        DebugRandAddi.Log("HandleSearch found " + module.name + " as a controller");
                        RequestConnection(module);
                        NotifyOthersConnect();
                    }
                    else
                    {
                        var module2 = item.GetComponent<T>();
                        if (module2 && module2.AssignedMPW)
                        {
                            DebugRandAddi.Log("HandleSearch found " + module2.name + " as a controller in another ModulePartWeaponPart");
                            RequestConnection(module2.AssignedMPW);
                            NotifyOthersConnect();
                        }
                    }
                }
            }
        }


        internal override void NotifyOthersConnect()
        {
            if (!assigned)
            {
                DebugRandAddi.Log("NotifyOthersConnect cannot fire without assigned being set to a valid instance");
                return;
            }
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                DebugRandAddi.Log("NotifyOthersConnect returned " + neighboors.Length + " results");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<T>();
                    if (module && !module.AssignedMPW)
                    {
                        DebugRandAddi.Log("HandleSearch found " + module.name + " another ModulePartWeaponPart that needs a controller");
                        module.RequestConnection(assigned);
                        module.NotifyOthersConnect();
                    }
                }
            }
        }

        internal override void OnDetachUpdateConnections()
        {
            recursed = true;
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                //DebugRandAddi.Log("OnDetachUpdateConnections has " + neighboors.Length + " nearby");
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<T>();
                    if (module && !module.recursed)
                    {
                        module.IsConnectedToController<T>(true);
                    }
                }
            }
            recursed = false;
        }

    }

    public class ModulePartWeaponBarrel: ModulePartWeaponPart<ModulePartWeaponBarrel>
    {
        public int barrelLength = 1;
        private Collider mainCollider;

        internal override void DoConnectionRelay(ModulePartWeapon MPW, bool connected)
        {
            if (MPW == null)
            {
                GetBarrelModelTrans(true);
            }
        }


        internal Transform GetBarrelModelTrans(bool show)
        {
            if (mainCollider == null)
                mainCollider = GetComponentInChildren<Collider>();
            if (mainCollider)
            {
                if (show)
                {
                    //mainCollider.gameObject.layer = Globals.inst.layerTank;
                    ResetMainColliderPosition();
                }
            }
            if (VisualMesh != null)
            {
                VisualMesh.gameObject.SetActive(show);
            }
            return VisualMesh;
        }
        internal void UpdateMainColliderPosition(Vector3 worldSpace, Quaternion forwards)
        {
            if (mainCollider == null)
                mainCollider = GetComponentInChildren<Collider>();
            if (mainCollider)
            {
                mainCollider.transform.rotation = forwards;
                mainCollider.transform.position = worldSpace;
            }
        }
        internal void ResetMainColliderPosition()
        {
            if (mainCollider == null)
                mainCollider = GetComponentInChildren<Collider>();
            if (mainCollider)
            {
                mainCollider.transform.localPosition = Vector3.zero;
            }
        }
        public Collider[] GetBarrelColliders()
        {
            return GetComponentsInChildren<Collider>();
        }

    }
    public class ModulePartWeaponDongle : ModulePartWeaponPart<ModulePartWeaponDongle>
    {
    }
    /// <summary>
    /// Relays to a another ModulePartWeaponRelay, forwards block face that has no AP.
    ///   Intended for use with Control Blocks.
    /// </summary>
    public class ModulePartWeaponRelay : ModulePartWeaponDongle
    {
        private TankBlock GetRemoteConnection()
        {
            TankBlock blockRemote = GetRemoteConnectionNoCheck();
            if (blockRemote)
            {
                TankBlock returnToSender = blockRemote.GetComponent<ModulePartWeaponRelay>()?.GetRemoteConnectionNoCheck();
                if (returnToSender && block == returnToSender)
                    return blockRemote;
            }
            return null;
        }
        private TankBlock GetRemoteConnectionNoCheck()
        {
            Vector3 forwardsNode = block.cachedLocalPosition + block.cachedLocalRotation * Vector3.forward;
            return tank.blockman.GetBlockAtPosition(forwardsNode);
        }
        internal override TankBlock[] GetReachableConnections()
        {
            List<TankBlock> blocks = new List<TankBlock>();

            TankBlock blockRemote = GetRemoteConnection();
            if (blockRemote)
                blocks.Add(blockRemote);

            TankBlock[] getCase = GetAllAttachedAPNeighboors();
            if (getCase != null)
                blocks.AddRange(getCase);
            if (blocks.Count > 0)
                return blocks.ToArray();
            return null;
        }
    }
}
