﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using System.Reflection;

public class TrailProjectile : RandomAdditions.TrailProjectile { }
namespace RandomAdditions
{
    // "TrailProjectile": {},   // Makes trails prettier on projectile hit

    /// <summary>
    /// A special type of projectile that contines dealing damage based on a provided TrailRenderer
    /// </summary>
    public class TrailProjectile : ExtProj
    {
        private static TrailRenderer latentPool;

        private static void InsureInit()
        {
            if (!latentPool)
            {
                latentPool = new GameObject("TrailProjectileDummy").AddComponent<TrailRenderer>();
                latentPool.autodestruct = false;
                latentPool.CreatePool(8);
            }
        }
        private static Vector3[] transferArray = new Vector3[26];
        private void KeepTrailPresent()
        {
            var TRo = trail;
            var TR = latentPool.Spawn(null, transform.position);
            TR.time = delay;
            TR.textureMode = TRo.textureMode;
            TR.sharedMaterial = TRo.sharedMaterial;
            TR.alignment = TRo.alignment;
            TR.allowOcclusionWhenDynamic = TRo.allowOcclusionWhenDynamic;
            TR.startColor = TRo.startColor;
            TR.endColor = TRo.endColor;
            TR.startWidth = TRo.startWidth;
            TR.endWidth = TRo.endWidth;
            TR.minVertexDistance = TRo.minVertexDistance;
            TR.widthMultiplier = TRo.widthMultiplier;
            TR.numCapVertices = TRo.numCapVertices;
            TR.numCornerVertices = TRo.numCornerVertices;
            if (TRo.positionCount != transferArray.Length)
                Array.Resize(ref transferArray, TRo.positionCount);
            trail.GetPositions(transferArray);
            TR.SetPositions(transferArray);
            TRo.Clear();
            InvokeHelper.Invoke(TR.Recycle, delay, true);
        }


        private Collider[] cols;
        private TrailRenderer trail;
        private bool hasEmiss = false;
        private ParticleSystem[] emiss;
        private bool wasGrav = false;
        private float delay => trail.time;

        public override void Fire(FireData fireData)
        {
            PB.rbody.useGravity = wasGrav;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(true);

            if (trail)
            {   // clear
                trail.SetPositions(new Vector3[0]);
                trail.emitting = true;
            }

            foreach (var col in cols)
                col.enabled = true;
        }

        public override void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            if (ForceDestroy)
            {
                //__instance.trans.position = hitPoint;
                ForceDestroy = false;
            }

            if (trail)
            {
                KeepTrailPresent();
                trail.emitting = false;
            }
            foreach (var col in cols)
                col.enabled = false;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(false);

            PB.rbody.useGravity = false;
            PB.rbody.velocity = Vector3.zero;
        }
        static FieldInfo deathDis = typeof(Projectile).GetField("m_DieAfterDelay", BindingFlags.NonPublic | BindingFlags.Instance);

        public override void Pool()
        {
            InsureInit();
            cols = GetComponentsInChildren<Collider>();
            wasGrav = PB.rbody.useGravity;
            trail = GetComponent<TrailRenderer>();
            //deathDis.SetValue(PB.project, false);
            var particles = GetComponentsInChildren<ParticleSystem>();
            if (particles != null)
            {
                hasEmiss = true;
                emiss = particles;
            }
            if (trail && !trail.enabled)
            {   
                LogHandler.ThrowWarning("RandomAdditions: TrailProjectile expects an active TrailRenderer in hierarchy, but it is not enabled!");
            }
        }

    }
}
