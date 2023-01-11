using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace RandomAdditions.Networking
{
    /*
     * Handles network hooks for this mod
     */
    internal class ManModNetwork
    {
        internal static UnityEngine.Networking.NetworkInstanceId Host;
        internal static bool HostExists = false;

        const int NetworkHooksStart = 6590;
        internal static int NetworkHooks => NetworkHooksStart + hooks.Count;
        internal static Dictionary<int, NetworkHook> hooks = new Dictionary<int, NetworkHook>();

        internal static bool Register(NetworkHook hook)
        {
            int ID = NetworkHooks + 1;
            if (!hooks.TryGetValue(ID, out _))
            {
                hook.AssignedID = ID;
                hooks.Add(ID, hook);
                return true;
            }
            return false;
        }
        internal static bool UnRegister(NetworkHook hook)
        {
            return hooks.Remove(hook.AssignedID);
        }

        internal static bool Send(NetworkHook hook, MessageBase message)
        {
            if (hooks.TryGetValue(hook.AssignedID, out _))
            {
                try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllClients((TTMsgType)hook.AssignedID, message, Host);
                    DebugRandAddi.Log("Sent new AdvancedAI update to all");
                    return true;
                }
                catch { DebugRandAddi.Log("TACtical_AI: Failed to send new AdvancedAI update, shouldn't be too bad in the long run"); }
                return false;
            }
            else
                throw new Exception("The given NetworkHook is not registered in ManModNetwork");
        }
    }

    /// <summary>
    /// Use NetworkHook<T> instead!
    /// </summary>
    public class NetworkHook
    {
        public int AssignedID = -1;

        /// <summary>
        /// Must be called before game scene fully loads.  Do not unhook unless we are certain the hook is no longer needed!
        /// </summary>
        /// <returns>true if it worked, false if failed</returns>
        public bool Register()
        {
            return ManModNetwork.Register(this);
        }
        /// <summary>
        /// Must be called before game scene fully unloads.  Do not unhook unless we are certain the hook is no longer needed!
        /// </summary>
        /// <returns>true if it worked, false if failed</returns>
        public bool UnRegister()
        {
            return ManModNetwork.UnRegister(this);
        }

        public bool CanBroadcast()
        {
            return ManNetwork.IsNetworked && ManModNetwork.HostExists;
        }
        public bool TryBroadcastToAll(MessageBase message)
        {
            return ManModNetwork.Send(this, message);
        }

        internal virtual void OnClientReceive_Internal(NetworkMessage netMsg)
        {
            throw new NotImplementedException("You used NetworkHook which is incorrect.  NetworkHook<T> is the correct hook format!");
        }
    }
}
