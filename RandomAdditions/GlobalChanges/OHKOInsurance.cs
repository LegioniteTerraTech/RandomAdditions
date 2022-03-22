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
                if (block.BlockType == BlockTypes.EXP_RD_Target_971)
                    return; // CANNOT KILL TARGET

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
                    thisInst = block.gameObject.AddComponent<OHKOInsurance>();
                    thisInst.TB = block;
                    //if (thisTank != null)
                    //    Debug.Log("RandomAdditions: Block " + block.gameObject.name + " on " + thisTank.name + " has been marked for unstoppable death.");
                    thisInst.DeathTimer = thisInst.TB.damage.SelfDestructTimeRemaining() * 50 + 50;
                    thisInst.TB.damage.AbortSelfDestruct();               //let it suffer a bit longer
                    thisInst.TB.Separate();                               //detach from Tech
                    thisInst.TB.AttachEvent.Subscribe(thisInst.Reset);
                    thisInst.TB.LockBlockAttach();
                }
            }
        }

        private void Reset()
        {
            TB.UnlockBlockAttach();
            TB.AttachEvent.Unsubscribe(Reset);
            Destroy(this);
        }

        private void ForceOverrideEverythingAndDie()
        {
            if (TB.IsAttached)
            {
                Reset(); // it was destroyed & respawned before this could happen
                return;
            }
            // insta-death
            //Debug.Log("RandomAdditions: Block has gone poof");
            TankBlock cache = TB;
            cache.damage.Explode(true);
            ManLooseBlocks.inst.RequestDespawnBlock(cache, DespawnReason.Host);
            Reset();
        }

        private void FixedUpdate()
        {
            //Debug.Log("RandomAdditions: DeathClock " + DeathTimer + "!");
            TB.PreExplodePulse = true;
            DeathTimer--;
            if (DeathTimer <= 0)
                ForceOverrideEverythingAndDie();
        }
    }
}
