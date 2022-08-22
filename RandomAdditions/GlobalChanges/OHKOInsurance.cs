using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Not to be used on fresh, new blocks.
    /// </summary>
    internal class OHKOInsurance : MonoBehaviour
    {
        public static void TryQueueUnstoppableDeath(TankBlock block)
        {
#if !STEAM
            if (ManNetwork.IsNetworked)
                return; // Cannot exploit this in MP - the presence of such a powerful weapon (in unmodded) can cause many issues.
#else
            if (!ManNetwork.IsHost)
                return; // Only host can update.
#endif
            if ((bool)block)
            {
                try
                {
                    if (block.BlockType == BlockTypes.EXP_RD_Target_971)
                        return; // CANNOT KILL TARGET
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: block FAILED to fetch blocktype!  This is unresolvable.");
                }

                Tank thisTank = block.transform.root.GetComponent<Tank>();
                if (thisTank.IsNotNull())
                {
                    if (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible && ManSpawn.IsPlayerTeam(thisTank.Team))
                        return;//Cannot kill invincible
                }
                if (block.damage.maxHealth < 0)
                    return;//Cannot kill borked
                OHKOInsurance thisInst = block.GetComponent<OHKOInsurance>();
                if (!(bool)thisInst)
                {
                    if (thisTank)
                    {
                        thisTank.blockman.Detach(block, false, false, true);//detach from Tech
                    }
                    DestroyBlock(block);
                }
            }
        }

        private static void DestroyBlock(TankBlock TB)
        {   // insta-death
            //DebugRandAddi.Log("RandomAdditions: Block has gone poof");
            TankBlock cache = TB;
            cache.damage.Explode(true);
            ManLooseBlocks.inst.RequestDespawnBlock(cache, DespawnReason.Host);
        }
    }
}
