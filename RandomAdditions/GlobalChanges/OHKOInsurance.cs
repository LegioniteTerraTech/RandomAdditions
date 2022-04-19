using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Not to be used on fresh, new blocks.
    /// </summary>
    internal class OHKOInsurance : MonoBehaviour
    {
        private TankBlock TB;
        private float DeathTimer = 15;

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
                    Debug.Log("RandomAdditions: TerraTech FAILED to label blocktype!  This is unresolvable.");
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
                    // Game was being too unreliable for this
                    /*
                    thisInst = block.gameObject.AddComponent<OHKOInsurance>();
                    thisInst.TB = block;
                    //if (thisTank != null)
                    //    Debug.Log("RandomAdditions: Block " + block.gameObject.name + " on " + thisTank.name + " has been marked for unstoppable death.");
                    thisInst.DeathTimer = 1;
                    thisInst.TB.damage.AbortSelfDestruct();               //let it suffer a bit longer
                    if (thisInst.TB.tank)
                    {
                        thisInst.TB.tank.blockman.Detach(thisInst.TB, false, false, true);//detach from Tech
                    }
                    thisInst.TB.AttachEvent.Subscribe(thisInst.Reset);
                    thisInst.TB.LockBlockAttach();
                    thisInst.TB.visible.SetInteractionTimeout(1);
                    */
                }
            }
        }

        private void Reset()
        {
            TB.visible.SetInteractionTimeout(0);
            TB.UnlockBlockAttach();
            TB.AttachEvent.Unsubscribe(Reset);
            Destroy(this);
        }

        private void ForceOverrideEverythingAndDie()
        {   // insta-death
            //Debug.Log("RandomAdditions: Block has gone poof"); 
            DestroyBlock(TB);
            Reset();
        }
        private static void DestroyBlock(TankBlock TB)
        {   // insta-death
            //Debug.Log("RandomAdditions: Block has gone poof");
            TankBlock cache = TB;
            cache.damage.Explode(true);
            ManLooseBlocks.inst.RequestDespawnBlock(cache, DespawnReason.Host);
        }

        private void Update()
        {
            //Debug.Log("RandomAdditions: DeathClock " + DeathTimer + "!");
            TB.damage.MultiplayerFakeDamagePulse();
            DeathTimer -= Time.deltaTime;
            if (DeathTimer <= 0)
                ForceOverrideEverythingAndDie();
        }
    }
}
