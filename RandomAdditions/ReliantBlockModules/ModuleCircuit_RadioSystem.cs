using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;

namespace RandomAdditions
{
    /// <summary>
    /// Long-distance transmitter
    /// </summary>
    public class ManRadio : MonoBehaviour
    {
        [SSManagerInst]
        public static ManRadio inst;
        [SSaveField]
        public Dictionary<int, HashSet<IntVector2>> ChannelsNeedReload = new Dictionary<int, HashSet<IntVector2>>();
        private static Dictionary<int, RadioSignal> ChannelsTransmitted = new Dictionary<int, RadioSignal>();

        internal class RadioSignal
        {
            public int Signal = 0;
            public HashSet<ModuleCircuit_RadioSystem> Registered = new HashSet<ModuleCircuit_RadioSystem>();
        }

        public static void InsureInit()
        {
            if (inst)
                return;
            inst = new GameObject("ManRadio").AddComponent<ManRadio>();
            Circuits.EndChargeUpdate.Subscribe(inst.EndTransmit);
        }
        public static void DeInit()
        {
            if (!inst)
                return;
            Circuits.EndChargeUpdate.Unsubscribe(inst.EndTransmit);
            Destroy(inst.gameObject);
        }

        public static void OnChannelSwitch(ModuleCircuit_RadioSystem radio, int prev, int set)
        {
            RadioSignal RS;
            if (ChannelsTransmitted.TryGetValue(prev, out RS))
            {
                RS.Registered.Remove(radio);
            }
            if (ChannelsTransmitted.TryGetValue(set, out RS))
            {
                RS.Registered.Add(radio);
            }
            else
            {
                RS = new RadioSignal();
                RS.Registered.Add(radio);
                ChannelsTransmitted.Add(set, RS);
            }
        }
        public static void HandleAddition(ModuleCircuit_RadioSystem radio)
        {
            if (inst.ChannelsNeedReload.TryGetValue(radio.RadioChannel, out var tiles))
            {
                IntVector2 rm = WorldPosition.FromScenePosition(radio.tank.visible.centrePosition).TileCoord;
                tiles.Remove(rm);
                if (tiles.Count == 0)
                    inst.ChannelsNeedReload.Remove(radio.RadioChannel);
            }
        }
        public static void HandleRemoval(ModuleCircuit_RadioSystem radio)
        {
            Tank tank = radio.tank;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ManRadio(HandleRemoval) - TANK IS NULL");
                return;
            }
            if (ManSaveGame.Storing)
            {
                IntVector2 rm = WorldPosition.FromScenePosition(radio.tank.visible.centrePosition).TileCoord;
                if (inst.ChannelsNeedReload.TryGetValue(radio.RadioChannel, out var tiles))
                {
                    if (!tiles.Contains(rm))
                        tiles.Add(rm);
                }
                else
                {
                    inst.ChannelsNeedReload.Add(radio.RadioChannel, new HashSet<IntVector2>() { rm });
                }
            }
        }

        public void Transmit(ModuleCircuit_RadioSystem radio, Circuits.BlockChargeData charge)
        {
            if (radio.tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ManRadio(Transmit) - TANK IS NULL");
                return;
            }
            var RS = ChannelsTransmitted[radio.RadioChannel];
            if (RS.Signal < charge.ChargeStrength)
            {
                if (RS.Signal == 0 && charge.ChargeStrength > 0)
                {
                    if (inst.ChannelsNeedReload.TryGetValue(radio.RadioChannel, out HashSet<IntVector2> val2))
                    {
                        if (ManNetwork.IsHost)
                        {
                            foreach (var item in val2)
                            {
                                ManWorldTileExt.ClientTempLoadTile(item, false, 3);
                            }
                        }
                    }
                }
                RS.Signal = charge.ChargeStrength;
            }
        }
        public int ReceiveRadio(ModuleCircuit_RadioSystem radio)
        {
            if (radio.tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ManRadio(ReceiveRadio) - TANK IS NULL");
                return 0;
            }
            if (ChannelsTransmitted.TryGetValue(radio.RadioChannel, out var RS))
            {
                return RS.Signal;
            }
            return 0;
        }
        public void EndTransmit()
        {
            foreach (var item in ChannelsTransmitted)
            {
                item.Value.Signal = 0;
            }
        }
    }
    /// <summary>
    /// Can load remote chunks if need be on logic signal - WIP
    /// </summary>
    public class ModuleCircuit_RadioSystem : ExtModule, ICircuitDispensor
    {
        private const float AttachOrSpawnSignalLength = 0.5f;

        internal int RadioChannel = 0;
        private float SignalSend = 0;

        public bool Transmits = true;
        public bool Receives = true;

        // Logic
        public int SignalOnAPIndex = 0;
        private bool LogicConnected = false;


        protected override void Pool()
        {
            if (SignalOnAPIndex < 0 || SignalOnAPIndex >= block.attachPoints.Length)
            {
                BlockDebug.ThrowWarning(false, "RandomAdditions: ModuleCircuit_RadioSystem SignalOnAPIndex ["
                    + SignalOnAPIndex + "] is out of APIndex range [0-" + (block.attachPoints.Length - 1)
                    + "] to operate correctly.\n  Cause of error - Block " + block.name);
                SignalOnAPIndex = 0;
            }
            ManRadio.InsureInit();
        }


        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleRailEngine");
            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver && Transmits)
                {
                    LogicConnected = true;
                    ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, false, true);
                }
            }
            ManRadio.HandleAddition(this);
            SignalSend = Time.time + AttachOrSpawnSignalLength;
            enabled = true;
        }

        public override void OnDetach()
        {
            enabled = false;
            SignalSend = Time.time;
            ManRadio.HandleRemoval(this);
            if (LogicConnected && Transmits)
                ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, true, true);
            LogicConnected = false;
        }

        public void OnRecCharge(Circuits.BlockChargeData charge)
        {
            if (!Receives)
                return;
            if (charge.AllChargeAPsAndCharges.TryGetValue(block.attachPoints[SignalOnAPIndex], out int val))
            {
                if (val > 0)
                {
                    ManRadio.inst.Transmit(this, charge);
                    //DebugRandAddi.Log("OnRecCharge " + charge);
                }
            }
        }
      
        /// <summary>
        /// Directional!
        /// </summary>
        public int GetDispensableCharge(Vector3 APOut)
        {
            if (Receives || block.attachPoints[SignalOnAPIndex] != APOut)
                return 0;
            if (Time.time < SignalSend)
                return block.CircuitNode.Dispensor.DefaultChargeStrength;
            return ManRadio.inst.ReceiveRadio(this);
        }
    }
}
