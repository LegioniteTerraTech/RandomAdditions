using UnityEngine;

namespace RandomAdditions
{
    public class WeightedProjectile : MonoBehaviour
    {
        /* Throw this within m_BulletPrefab
        "RandomAdditions.WeightedProjectile":{ // launch bowling balls but they actually have proper weight
            "ProjectileMass": 4,        // The Mass you want the projectile to have
            "CustomGravity": false,     // enable the parameter below - WARNING overrides all grav settings! (and breaks WeaponAimMod!)
            "GravityAndSpeedScale": 1.0,// The percent (1.0 is 100%) force gravity pulls down on this and the speed it travels at 
            //so you can have dramatic super-heavy projectiles that move slow through the air
        },
        */

        public float ProjectileMass = 4.0f;
        public bool CustomGravity = false;
        public float GravityAndSpeedScale = 1.0f;


        private Rigidbody fetchedRBody;
        private Transform thisTrans;
        private bool hasFiredOnce = false;

        public void OnPool()
        {
            thisTrans = gameObject.transform;
            fetchedRBody = gameObject.GetComponent<Rigidbody>();
            hasFiredOnce = true;
            Debug.Log("RandomAdditions: Launched WeightedProjectile on " + gameObject.name);
        }

        private void FixedUpdate()
        {
            if (!hasFiredOnce)
                OnPool();
            if (CustomGravity)
                ForceProjectileGrav();
        }
        
        public void SetProjectileMass()
        {
            try 
            { 
                gameObject.GetComponent<Rigidbody>().mass = Mathf.Max(ProjectileMass, 0.00123f);
                Debug.Log("RandomAdditions: Set projectile mass to " + gameObject.GetComponent<Rigidbody>().mass);
            }
            catch { Debug.Log("RandomAdditions: Could not set host projectile mass!"); }
        }
        public void ForceProjectileGrav()
        {
            Vector3 grav = Physics.gravity;
            fetchedRBody.AddForceAtPosition(grav * GravityAndSpeedScale - grav, thisTrans.position, ForceMode.Acceleration);
        }
    }
}
