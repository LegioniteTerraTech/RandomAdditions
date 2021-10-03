using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    // Auto-attached to projectiles that can be distracted
    //   Not intended for use on newly spawned projectiles
    internal class DistractedProjectile : MonoBehaviour
    {
        private Rigidbody rbody;
        private Rigidbody lure;

        private static FieldInfo rotRate = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo aimAtTarg = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.NonPublic | BindingFlags.Instance);

        public Transform trans;
        private Projectile proj;
        private bool init = false;
        private bool AimAtTarg = false;
        private float RotRate = 50;

        public static void DistractAll(List<Rigidbody> targets, Rigidbody lure)
        {
            foreach (Rigidbody rb in targets) 
            {
                Distract(rb, lure);
            }
        }
        public static void Distract(Rigidbody target, Rigidbody lure)
        {
            if (!(bool)target)
                return;
            if (!target.GetComponent<MissileProjectile>() || !target.GetComponent<SeekingProjectile>())
                return;
            var targ = target.GetComponent<DistractedProjectile>();
            if (!(bool)targ)
                targ = target.gameObject.AddComponent<DistractedProjectile>();
            targ.GrabValues();
            targ.lure = lure;
        }
        public void GrabValues()
        {
            if (init)
                return;
            trans = gameObject.transform;
            rbody = gameObject.GetComponent<Rigidbody>();
            proj = gameObject.GetComponent<Projectile>();
            var seeking = gameObject.GetComponent<SeekingProjectile>();
            if (!(bool)seeking)
            {
                Debug.Log("RandomAdditions: DistractedProjectile - GrabValues was triggered on an invalid projectile. What? " + gameObject.name);
                return;
            }
            RotRate = (float)rotRate.GetValue(seeking);
            AimAtTarg = (bool)aimAtTarg.GetValue(seeking);
            init = true;
            //Debug.Log("RandomAdditions: Launched InterceptProjectile on " + gameObject.name);
        }
        public bool Distracted(SeekingProjectile seeking)
        {
            if (!(bool)lure || !(bool)rbody || lure.IsSleeping() || rbody.IsSleeping())
            {
                Destroy(this);
                return false;
            }
            Vector3 vec = lure.position - transform.position;
            Vector3 directed = Vector3.Cross(rbody.velocity, vec).normalized;
            float b = Vector3.Angle(seeking.transform.forward, vec);
            Quaternion quat = Quaternion.AngleAxis(Mathf.Min(RotRate * Time.deltaTime, b), directed);
            rbody.velocity = quat * rbody.velocity;
            if (AimAtTarg)
            {
                Quaternion rot = quat * rbody.rotation;
                rbody.MoveRotation(rot);
            }
            return true;
        }
    }
}
