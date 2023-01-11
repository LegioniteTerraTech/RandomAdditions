using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using RandomAdditions.Networking;

namespace RandomAdditions
{
    public class NetworkHook<T> : NetworkHook where T : MessageBase
    {
        private Action<MessageBase> receiveAction;
        public NetworkHook(Action<MessageBase> onReceive)
        {
            receiveAction = onReceive;
        }
        internal override void OnClientReceive_Internal(NetworkMessage netMsg)
        {
            T decoded = (T)Activator.CreateInstance(typeof(T));
            decoded.Deserialize(netMsg.reader);
            receiveAction.Invoke(decoded);
        }

        public class TempMessageBase : MessageBase
        {
            public TempMessageBase() { }
            public TempMessageBase(int MissionHash, bool Success)
            {
                this.MissionHash = MissionHash;
                this.Success = Success;
            }

            public int MissionHash;
            public bool Success;
        }
    }
}
