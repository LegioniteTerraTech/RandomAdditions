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

        public void OnPool()
        {
            if (KickStart.isWaterModPresent)// don't fire if water mod is not present
            {
                Debug.Log("RandomAdditions: Launched TorpedoProjectile on " + gameObject.name);
                thisTrans = gameObject.transform;
                fetchedRBody = gameObject.GetComponent<Rigidbody>();
                var isTransformPresent = gameObject.transform.Find("_subProp");
                if (isTransformPresent)
                {
                    addedThrustPosition = isTransformPresent.transform.localPosition;
                    addedThrustDirection = thisTrans.InverseTransformDirection(isTransformPresent.transform.forward);
                    //Debug.Log("RandomAdditions: Projectile " + gameObject.name + " Thrust is " + addedThrustDirection + " | and position is " + addedThrustPosition);
                }
                else
                {
                    addedThrustPosition = thisTrans.localPosition;
                    addedThrustDirection = thisTrans.InverseTransformDirection(thisTrans.forward);
                    Debug.Log("RandomAdditions: Projectile " + gameObject.name + " does not have any previous effectors or thrust transforms!  Defaulting to the center of the projectile!  \nAdd a \"_subProp\" to your projectile's JSON!");
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
                if (WaterMod.QPatch.WaterHeight > gameObject.transform.position.y)
                {
                    isSubmerged = true;
                    if (ThrustUntilProjectileDeath || !killThrust)
                    {
                        //Debug.Log("RandomAdditions: Projectile " + gameObject.name + " is thrusting submerged!");
                        fetchedRBody.AddForceAtPosition(thisTrans.TransformDirection(addedThrustDirection.normalized * SubmergedThrust), thisTrans.TransformPoint(addedThrustPosition));
                    }
                }
                else
                {
                    isSubmerged = false;
                }
            }
        }
    }
}
