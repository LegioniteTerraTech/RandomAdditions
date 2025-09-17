using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Payload.UI.Commands;
using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using UnityEngine.Networking;

namespace RandomAdditions
{
    public class NetUtil
    {
        public class BlockMessage : MessageBase
        {
            public BlockMessage() { }
            public BlockMessage(TankBlock block)
            {
                BlockIndex = block.GetBlockIndexAndTechNetID(out TechID);
            }
            /// <summary>
            /// CAN RETURN NULL IF BLOCK NOT FOUND
            /// </summary>
            /// <returns></returns>
            public TankBlock GetBlock()
            {
                NetTech NT = ManNetTechs.inst.FindTech(TechID);
                if (NT?.tech)
                {
                    TankBlock TB = NT.tech.blockman.GetBlockWithIndex(BlockIndex);
                    if (TB)
                        return TB;
                }
                return null;
            }

            public uint TechID;
            public int BlockIndex;
        }
        public class NetworkedBoolMessage : BlockMessage
        {
            public NetworkedBoolMessage() { }
            public NetworkedBoolMessage(TankBlock block, bool state): base(block)
            {
                this.state = state;
            }
            public bool state;
        }
    }
}
