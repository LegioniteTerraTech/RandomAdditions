using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    //Allows a projectile to collide with MissileProjectile
    /*
        "RandomAdditions.InterceptProjectile":{ // Add a special movement effect to your projectile
            "ForcedAiming": false, // If there's a projectile, this will always aim at it first
            "Aiming":       true,  // If there's a projectile, this will aim at it if there's no enemy in range
            "OnlyDefend":   false, // will not home in on enemies
            "InterceptMultiplier":   3, // How much to multiply the aiming strength if targeting a missile
        },
    */
    public class InterceptProjectile : MonoBehaviour
    {
        public bool ForcedAiming = false;
        public bool Aiming = false;
        public bool OnlyDefend = false;
        public float InterceptMultiplier = 3;

        private static FieldInfo rotRate = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo aimAtTarg = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo range = typeof(SeekingProjectile).GetField("m_VisionRange", BindingFlags.NonPublic | BindingFlags.Instance);


        public Transform trans;
        public int team = -1;
        private Rigidbody rbody;
        private bool init = false;
        private byte timer = 3;
        private bool AimAtTarg = false;
        private float Range = 50;
        private float RotRate = 50;

        private Rigidbody LockedTarget;

        public void Reset()
        {
            LockedTarget = null;
            timer = 3;
            Debug.Log("RandomAdditions: InterceptProjectile - RESET");
            init = false;
            GrabValues();
        }
        public void GrabValues()
        {
            if (init)
                return;
            trans = gameObject.transform;
            rbody = gameObject.GetComponent<Rigidbody>();
            var teamM = gameObject.GetComponent<Projectile>().Shooter;
            if (teamM)
                team = teamM.Team;
            var seeking = gameObject.GetComponent<SeekingProjectile>();
            if (!gameObject.GetComponent<SeekingProjectile>())
                return;
            RotRate = (float)rotRate.GetValue(seeking);
            AimAtTarg = (bool)aimAtTarg.GetValue(seeking);
            Range = (float)range.GetValue(seeking);
            init = true;
            Debug.Log("RandomAdditions: Launched InterceptProjectile on " + gameObject.name);
        }
        public bool OverrideAiming(SeekingProjectile seeking)
        {
            if (!init)
                GrabValues();
            if (!FindAndHome(out Vector3 posOut))
                return false;
            Vector3 vec = posOut - trans.position;
            Vector3 directed = Vector3.Cross(rbody.velocity, vec).normalized;
            float b = Vector3.Angle(seeking.transform.forward, vec);
            Quaternion quat = Quaternion.AngleAxis(Mathf.Min(RotRate * InterceptMultiplier * Time.deltaTime, b), directed);
            rbody.velocity = quat * rbody.velocity;
            if (AimAtTarg)
            {
                Quaternion rot = quat * rbody.rotation;
                rbody.MoveRotation(rot);
            }
            return true;
        }
        public bool FindAndHome(out Vector3 posOut)
        {
            posOut = Vector3.zero;
            if (!GetOrTrack(out Rigidbody rbodyT))
                return false;

            if ((rbodyT.position - rbody.position).sqrMagnitude < Range / 4)
                posOut = rbodyT.position;
            else
                posOut = rbodyT.position + (rbodyT.velocity * Time.deltaTime);
            //Debug.Log("RandomAdditions: InterceptProjectile - Homing at " + posOut);
            return true;
        }
        public bool GetOrTrack(out Rigidbody rbodyT)
        {
            if (LockedTarget.IsNotNull() && !LockedTarget.isKinematic)
            {
                if ((LockedTarget.position - rbody.position).sqrMagnitude > Range * Range)
                {
                    LockedTarget = null;
                }
                else
                {
                    rbodyT = LockedTarget;
                    return true;
                }
            }

            if (timer <= 3)
            {
                if (ProjectileManager.GetClosestProjectile(this, Range, out Rigidbody rbodyCatch))
                {
                    rbodyT = rbodyCatch;
                    LockedTarget = rbodyCatch;
                    Debug.Log("RandomAdditions: InterceptProjectile - LOCK");
                    return true;
                }
                timer = 7;
            }
            timer--;
            rbodyT = null;
            return false;
        }
    }
}
