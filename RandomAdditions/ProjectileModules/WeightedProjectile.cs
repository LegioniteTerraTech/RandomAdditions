using UnityEngine;
using TerraTechETCUtil;

public class WeightedProjectile : RandomAdditions.WeightedProjectile { };
namespace RandomAdditions
{
    public class WeightedProjectile : ExtProj
    {
        /* Throw this within m_BulletPrefab
        "RandomAdditions.WeightedProjectile":{ // launch bowling balls but they actually have proper weight
            "ProjectileMass": 4,        // The Mass you want the projectile to have
            "CustomGravity": false,     // enable the parameter below - WARNING! breaks WeaponAimMod!
            "CustomGravityFractionSpeed": true, // Slow down the projectile's speed so that it doesn't overaim too badly
            "GravityAndSpeedScale": 1.0,// The percent (1.0 is 100%) force gravity pulls down on this and the speed it travels at 
            //so you can have dramatic super-heavy projectiles that move slow through the air
        },
        */

        public float ProjectileMass = 4.0f;
        public bool CustomGravity = false;
        public bool CustomGravityFractionSpeed = true;
        public float GravityAndSpeedScale = 1.0f;


        private Transform thisTrans;
        private bool hasFiredOnce = false;

        public void FirstUpdate()
        {
            thisTrans = gameObject.transform;
            hasFiredOnce = true;
            //DebugRandAddi.Log("RandomAdditions: Launched WeightedProjectile on " + gameObject.name);
        }

        private void FixedUpdate()
        {
            if (!hasFiredOnce)
                FirstUpdate();
            if (CustomGravity)
                ForceProjectileGrav();
        }

        public override void Pool()
        {
            try 
            { 
                gameObject.GetComponent<Rigidbody>().mass = Mathf.Max(ProjectileMass, 0.00123f);
                DebugRandAddi.Info("RandomAdditions: Set projectile mass to " + gameObject.GetComponent<Rigidbody>().mass);
            }
            catch { DebugRandAddi.Log("RandomAdditions: Could not set host projectile mass!"); }
        }

        public override void Fire(FireData fireData)
        {
            if (PB.shooter != null && CustomGravity && CustomGravityFractionSpeed)
            {
                Vector3 final = ((PB.rbody.velocity - PB.shooter.rbody.velocity) * GravityAndSpeedScale) + PB.shooter.rbody.velocity;
                DebugRandAddi.Log("RandomAdditions: Scaled WeightedProjectile Speed from " + PB.rbody.velocity + " to " + final);
                PB.rbody.velocity = final;
            }
        }

        public void ForceProjectileGrav()
        {
            if (!PB)
                BlockDebug.ThrowWarning(true, "PROJECTILE BASE IS NULL");
            else
            {
                Vector3 grav = Physics.gravity * PB.project.GetGravityScale() * PB.rbody.mass;
                PB.rbody.AddForceAtPosition(grav * GravityAndSpeedScale - grav, PB.rbody.centerOfMass);
            }
        }
    }
}
