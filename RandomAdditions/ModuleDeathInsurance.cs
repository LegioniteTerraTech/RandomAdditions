using UnityEngine;

namespace RandomAdditions
{
    public class ModuleDeathInsurance : Module
    {
        private bool MarkedForDeath = false;
        private float DeathTimer = 300;

        public void TryQueueUnstoppableDeath()
        {
            if (ManGameMode.inst.IsCurrentModeMultiplayer())
                return; // Cannot exploit this in MP - the presence of such a powerful weapon (in unmodded) can cause many issues.
            var thisInst = gameObject.GetComponent<ModuleDeathInsurance>();
            var tankBloc = gameObject.GetComponent<TankBlock>();
            if (tankBloc.IsNotNull() && tankBloc.tank.IsNotNull())
            {
                if (Singleton.Manager<ManPlayer>.inst.PlayerIndestructible && ManSpawn.IsPlayerTeam(tankBloc.tank.Team))
                    return;//Cannot kill invincible
                if (tankBloc.damage.maxHealth < 0)
                    return;//Cannot kill borked
                thisInst.MarkedForDeath = true;
                Tank thisTank = tankBloc.transform.root.GetComponent<Tank>();
                if (thisTank != null)
                    Debug.Log("RandomAdditions: Block " + tankBloc.gameObject.name + " on " + thisTank.name + " has been marked for unstoppable death.");
                thisInst.DeathTimer = tankBloc.damage.SelfDestructTimeRemaining() * 50 + 50;
                tankBloc.damage.m_DamageDetachFragility = 999000;  //deny all future rigidity
                tankBloc.damage.AbortSelfDestruct();               //let it suffer a bit longer
                tankBloc.Separate();                               //detach from Tech
            }
        }

        private void ForceOverrideEverythingAndDie()
        {
            // Make sure that even if killing it fails, the block is unusable
            gameObject.GetComponent<TankBlock>().LockBlockAttach();
            gameObject.GetComponent<TankBlock>().LockBlockInteraction();
            // insta-death
            MarkedForDeath = false;
            Debug.Log("RandomAdditions: Block has gone poof");
            gameObject.GetComponent<TankBlock>().damage.SelfDestruct(0);
        }

        private void FixedUpdate()
        {
            var thisInst = gameObject.GetComponent<ModuleDeathInsurance>();
            if (thisInst.MarkedForDeath)
            {
                Debug.Log("RandomAdditions: DeathClock " + DeathTimer + "!");
                gameObject.GetComponent<TankBlock>().PreExplodePulse = true;
                thisInst.DeathTimer--;
                if (thisInst.DeathTimer <= 0)
                    ForceOverrideEverythingAndDie();
            }
        }
    }
}
