using UnityEngine;

public class TorpedoProjectile : RandomAdditions.TorpedoProjectile { };
namespace RandomAdditions
{
    public class TorpedoProjectile : MonoBehaviour
    {
        // With Watermod installed, any missile that goes into the water with this projectile type
        //   will change their thrust value to the one specified here.
        /*
           "RandomAdditions.TorpedoProjectile": {
                "SubmergedThrust" : 0.5,                //Thrust to apply underwater
                "ThrustUntilProjectileDeath" : false,   //Should we thrust until we explode?
           },
            // Make sure to add a new GameObject with the name "_subProp" and position it to where the thrust should be
         */

        public float SubmergedThrust = 0.5f;// how much force to apply when in the water

        public bool ThrustUntilProjectileDeath = false;

        public bool isSubmerged = false;

        //AutoCollection
        private bool killThrust = false;
        private Vector3 addedThrustPosition;
        private Vector3 addedThrustDirection;
        private Rigidbody fetchedRBody;
        private Transform thisTrans;
        private ParticleSystem ps;

        public void OnPool()
        {
            if (KickStart.isWaterModPresent)// don't fire if water mod is not present
            {
                DebugRandAddi.Info("RandomAdditions: Launched TorpedoProjectile on " + gameObject.name);
                thisTrans = gameObject.transform;
                fetchedRBody = gameObject.GetComponent<Rigidbody>();
                var isTransformPresent = gameObject.transform.Find("_subProp");
                if (isTransformPresent)
                {
                    addedThrustPosition = isTransformPresent.transform.localPosition;
                    addedThrustDirection = thisTrans.InverseTransformDirection(isTransformPresent.transform.forward);
                    ps = isTransformPresent.GetComponent<ParticleSystem>();
                    if (ps)
                        ps.Clear(false);
                    //Debug.Log("RandomAdditions: Projectile " + gameObject.name + " Thrust is " + addedThrustDirection + " | and position is " + addedThrustPosition);
                }
                else
                {
                    addedThrustPosition = Vector3.zero;
                    addedThrustDirection = Vector3.forward;
                    DebugRandAddi.Log("RandomAdditions: Projectile " + gameObject.name + " does not have any previous effectors or thrust transforms!  Defaulting to the center of the projectile!  \nAdd a \"_subProp\" to your projectile's JSON!");
                }
            }
        }
        public void OnFire()
        {
            killThrust = false;
        }
        public void KillSubmergedThrust()
        {
            killThrust = true;
        }
        private void FixedUpdate()
        {
            if (KickStart.isWaterModPresent)// don't fire if water mod is not present
            {
                if (KickStart.WaterHeight > gameObject.transform.position.y)
                {
                    isSubmerged = true;
                    if (ThrustUntilProjectileDeath || !killThrust)
                    {
                        //Debug.Log("RandomAdditions: Projectile " + gameObject.name + " is thrusting submerged!");
                        fetchedRBody.AddForceAtPosition(thisTrans.TransformDirection(addedThrustDirection.normalized * SubmergedThrust), thisTrans.TransformPoint(addedThrustPosition));
                        if (ps && !ps.isPlaying)
                            ps.Play(false);
                    }
                    else if (ps && ps.isPlaying)
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                else
                {
                    isSubmerged = false;
                    if (ps && ps.isPlaying)
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
