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
    public class ManModNetwork
    {
        public static NetworkInstanceId Host;
        public static bool HostExists = false;

        const int NetworkHooksStart = 6590;
        public static int NetworkHooks => NetworkHooksStart + hooks.Count;
        public static Dictionary<int, NetworkHook> hooks = new Dictionary<int, NetworkHook>();

        internal static bool Register(NetworkHook hook)
        {
            int ID = NetworkHooks;
            if (!hooks.ContainsKey(ID))
            {
                hook.AssignedID = ID;
                hooks.Add(ID, hook);
                return true;
            }
            return false;
        }
        internal static bool UnRegister(NetworkHook hook)
        {
            throw new Exception("Cannot unregister hooks!");
            if (hooks.Remove(hook.AssignedID))
            {

                return true;
            }
            return false;
        }

        internal static bool SendToClient(int connectionID, NetworkHook hook, MessageBase message)
        {
            if (hooks.ContainsKey(hook.AssignedID))
            {
                try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToClient(connectionID, (TTMsgType)hook.AssignedID, message);
                    DebugRandAddi.Log("RandomAdditions: SendToClient - Sent new network update for " + hook.AssignedID + ", type " + hook.Type);
                    return true;
                }
                catch { DebugRandAddi.Log("RandomAdditions: SendToClient - Failed to send new network update for " + hook.AssignedID + ", type " + hook.Type); }
                return false;
            }
            else
                throw new Exception("SendToClient - The given NetworkHook is not registered in ManModNetwork");
        }
        internal static bool SendToAllClients(NetworkHook hook, MessageBase message)
        {
            if (hooks.ContainsKey(hook.AssignedID))
            {
                try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToAllClients((TTMsgType)hook.AssignedID, message, Host);
                    DebugRandAddi.Log("RandomAdditions: SendToAllClients - Sent new network update for " + hook.AssignedID + ", type " + hook.Type);
                    return true;
                }
                catch { DebugRandAddi.Log("RandomAdditions: SendToAllClients - Failed to send new network update for " + hook.AssignedID + ", type " + hook.Type); }
                return false;
            }
            else
                throw new Exception("SendToAllClients - The given NetworkHook is not registered in ManModNetwork");
        }
        internal static bool SendToServer(NetworkHook hook, MessageBase message)
        {
            if (hooks.ContainsKey(hook.AssignedID))
            {
                try
                {
                    Singleton.Manager<ManNetwork>.inst.SendToServer((TTMsgType)hook.AssignedID, message, Host);
                    DebugRandAddi.Log("RandomAdditions: SendToServer - Sent new network update for " + hook.AssignedID + ", type " + hook.Type);
                    return true;
                }
                catch { DebugRandAddi.Log("RandomAdditions: SendToServer - Failed to send new network update for " + hook.AssignedID + ", type " + hook.Type); }
                return false;
            }
            else
                throw new Exception("SendToServer - The given NetworkHook is not registered in ManModNetwork");
        }


        public class PlayerRequestServerCallbackBase : MessageBase
        {
            public PlayerRequestServerCallbackBase() { }
            public PlayerRequestServerCallbackBase(int senderID, int hookID)
            {
                this.senderID = senderID;
                this.hookID = hookID;
            }

            public int senderID;
            public int hookID;
        }
    }


    /// <summary>
    /// Use NetworkHook<T> instead!
    /// </summary>
    public class NetworkHook
    {
        public int AssignedID = -1;
        public NetMessageType Type;

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

        public bool ClientSends()
        {
            return Type <= NetMessageType.RequestServerFromClient;
        }
        public bool ServerSends()
        {
            return Type >= NetMessageType.FromClientToServerThenClients;
        }
        public bool ClientRecieves()
        {
            switch (Type)
            {
                case NetMessageType.ToClientsOnly:
                case NetMessageType.FromClientToServerThenClients:
                case NetMessageType.RequestServerFromClient:
                    return true;
                case NetMessageType.ToServerOnly:
                default:
                    return false;
            }
        }
        public bool ServerRecieves()
        {
            switch (Type)
            {
                case NetMessageType.ToServerOnly:
                case NetMessageType.FromClientToServerThenClients:
                case NetMessageType.RequestServerFromClient:
                    return true;
                case NetMessageType.ToClientsOnly:
                default:
                    return false;
            }
        }



        public bool CanBroadcast()
        {
            return ManNetwork.IsNetworked && ManModNetwork.HostExists;
        }
        public bool CanBroadcastTech(Tank tank)
        {
            return ManNetwork.IsNetworked && ManModNetwork.HostExists && tank?.netTech;
        }
        public bool TryBroadcast(MessageBase message)
        {
            switch (Type)
            {
                case NetMessageType.ToClientsOnly:
                    return TryBroadcastToAllClients(message);
                case NetMessageType.ToServerOnly:
                    return TryBroadcastToServer(message);
                case NetMessageType.FromClientToServerThenClients:
                    return TryBroadcastToServer(message);
                case NetMessageType.RequestServerFromClient:
                    return TryBroadcastToServer(message);
                default:
                    throw new Exception("TryBroadcast - Invalid NetMessageType");
            }
        }
        protected bool TryBroadcastToClient(int connectionID, MessageBase message)
        {
            return ManModNetwork.SendToClient(connectionID, this, message);
        }
        protected bool TryBroadcastToAllClients(MessageBase message)
        {
            return ManModNetwork.SendToAllClients(this, message);
        }
        protected bool TryBroadcastToServer(MessageBase message)
        {
            return ManModNetwork.SendToServer(this, message);
        }

        /// <summary>
        /// NetworkHook<T> is the correct hook format! DO NOT USE THIS ONE
        /// </summary>
        public virtual void OnToClientReceive_Internal(NetworkMessage netMsg)
        {
            throw new NotImplementedException("You used NetworkHook which is incorrect.  NetworkHook<T> is the correct hook format!");
        }
        /// <summary>
        /// NetworkHook<T> is the correct hook format! DO NOT USE THIS ONE
        /// </summary>
        public virtual void OnToServerReceive_Internal(NetworkMessage netMsg)
        {
            throw new NotImplementedException("You used NetworkHook which is incorrect.  NetworkHook<T> is the correct hook format!");
        }
    }
}
