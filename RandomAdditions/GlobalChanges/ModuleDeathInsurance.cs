using UnityEngine;

namespace RandomAdditions
{
    internal class ModuleDeathInsurance : Module
    {
        private TankBlock TB;
        private float DeathTimer = 300;

        public static void TryQueueUnstoppableDeath(TankBlock block)
        {
            if (ManGameMode.inst.IsCurrentModeMultiplayer())
                return; // Cannot exploit this in MP - the presence of such a powerful weapon (in unmodded) can cause many issues.
            if ((bool)block)
            {
                Tank thisTank = block.transform.root.GetComponent<Tank>();
                if (thisTank.IsNotNull())
                {
                    if (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible && ManSpawn.IsPlayerTeam(thisTank.Team))
                        return;//Cannot kill invincible
                }
                if (block.damage.maxHealth < 0)
                    return;//Cannot kill borked
                ModuleDeathInsurance thisInst = block.GetComponent<ModuleDeathInsurance>();
                if (!(bool)thisInst)
                {
                    thisInst = block.gameObject.AddComponent<ModuleDeathInsurance>();
                }
                thisInst.TB = block;
                if (thisTank != null)
                    Debug.Log("RandomAdditions: Block " + block.gameObject.name + " on " + thisTank.name + " has been marked for unstoppable death.");
                thisInst.DeathTimer = thisInst.TB.damage.SelfDestructTimeRemaining() * 50 + 50;
                thisInst.TB.damage.AbortSelfDestruct();               //let it suffer a bit longer
                thisInst.TB.Separate();                               //detach from Tech
            }
        }

        private void ForceOverrideEverythingAndDie()
        {
            // Make sure that even if killing it fails, the block is unusable
            TB.LockBlockAttach();
            TB.LockBlockInteraction();
            // insta-death
            Debug.Log("RandomAdditions: Block has gone poof");
            TankBlock cache = TB;
            Destroy(this);
            cache.damage.SelfDestruct(0);
        }

        private void FixedUpdate()
        {
            Debug.Log("RandomAdditions: DeathClock " + DeathTimer + "!");
            TB.PreExplodePulse = true;
            DeathTimer--;
            if (DeathTimer <= 0)
                ForceOverrideEverythingAndDie();
        }
    }
}
