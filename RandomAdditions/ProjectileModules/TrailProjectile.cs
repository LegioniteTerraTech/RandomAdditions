using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using System.Reflection;
using static CompoundExpression.EEInstance;

public class TrailProjectile : RandomAdditions.TrailProjectile { }
namespace RandomAdditions
{
    // "TrailProjectile": {},   // Makes trails prettier on projectile hit

    /// <summary>
    /// A special type of projectile that contines dealing damage based on a provided TrailRenderer
    /// </summary>
    public class TrailProjectile : ExtProj
    {
        private static WorldTrailHolder latentPool;
        internal static FieldInfo m_DieAfterDelay_fetch;

        public class WorldTrailHolder : MonoBehaviour, IWorldTreadmill
        {
            private TrailRenderer TR;
            public void OnCreate(TrailRenderer trailOriginal, float deathDelayed)
            {
                TR = gameObject.GetComponent<TrailRenderer>();
                TR.enabled = true;
                TR.emitting = true;
                TR.time = deathDelayed;
                TR.textureMode = trailOriginal.textureMode;
                TR.sharedMaterial = trailOriginal.sharedMaterial;
                TR.alignment = trailOriginal.alignment;
                TR.allowOcclusionWhenDynamic = trailOriginal.allowOcclusionWhenDynamic;
                TR.startColor = trailOriginal.startColor;
                TR.endColor = trailOriginal.endColor;
                TR.startWidth = trailOriginal.startWidth;
                TR.endWidth = trailOriginal.endWidth;
                TR.minVertexDistance = trailOriginal.minVertexDistance;
                TR.widthMultiplier = trailOriginal.widthMultiplier;
                TR.numCapVertices = trailOriginal.numCapVertices;
                TR.numCornerVertices = trailOriginal.numCornerVertices;
                Vector3[] transfer = new Vector3[trailOriginal.positionCount];
                //if (trailOriginal.positionCount != transferArray.Length)
                //    Array.Resize(ref transferArray, trailOriginal.positionCount);
                trailOriginal.GetPositions(transfer);
                TR.SetPositions(transfer);
                //trailOriginal.Clear();
                InvokeHelper.Invoke(DoRecycle, deathDelayed);
            }
            public void DoRecycle()
            {
                this.Recycle();
            }
            public void OnMoveWorldOrigin(IntVector3 delta)
            {
                Vector3[] transfer = new Vector3[TR.positionCount];
                TR.GetPositions(transfer);
                for (int i = 0; i < TR.positionCount; i++)
                    transfer[i] += delta;
                TR.SetPositions(transfer);
            }
        }

        /// <summary>
        /// How long should we continue applying damage after initial hit?
        ///  Will cause the trail to continue trying to move forwards
        /// </summary>
        public float TrailingDamageTime = 0;
        /// <summary>
        /// How many hits should this deal over TrailingDamageTime
        /// </summary>
        public int MaxTrailingHits = 0;
        private static void InsureInit()
        {
            if (!latentPool)
            {
                GameObject TPD = new GameObject("TrailProjectileDummy");
                TPD.AddComponent<TrailRenderer>().autodestruct = false;
                latentPool = TPD.AddComponent<WorldTrailHolder>();
                latentPool.CreatePool(8);
                m_DieAfterDelay_fetch = typeof(Projectile).GetField("m_DieAfterDelay", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }
        private static Vector3[] transferArray = new Vector3[26];
        private void CopyTrail()
        {
            DebugRandAddi.Assert("Spawned copy trail at " + transform.position);
            latentPool.Spawn(null, transform.position + new Vector3(0, 5, 0)).OnCreate(trail, delay);
        }


        private Collider[] cols;
        private TrailRenderer trail;
        private bool hasEmiss = false;
        private ParticleSystem[] emiss;
        private bool wasGrav = false;
        private Vector3 lastVelo1 = Vector3.zero;
        private Vector3 lastVelo2 = Vector3.zero;
        private float delay => trail.time;
        private float trailingCurTime = 0;
        private float trailingCooldownTime = 0;

        public override void Fire(FireData fireData)
        {
            PB.rbody.useGravity = wasGrav;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(true);

            if (trail)
            {   // clear
                trail.SetPositions(Array.Empty<Vector3>());
                trail.emitting = true;
            }

            foreach (var col in cols)
                col.enabled = true;
            if (TrailingDamageTime > 0 && MaxTrailingHits > 0)
            {
                trailingCurTime = 0;
                InvokeHelper.Invoke(UpdatePositionalTracker, 0.01f);
                lastVelo2 = PB.rbody.velocity;
                lastVelo1 = lastVelo2;
            }
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
                //CopyTrail(); // disabled since this was causing it not to work rip
                trail.emitting = false;
            }
            foreach (var col in cols)
                col.enabled = false;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(false);
            if (MaxTrailingHits != 0)
            {
                trailingCooldownTime = TrailingDamageTime / MaxTrailingHits;
                InvokeHelper.CancelInvoke(UpdatePositionalTracker);
                InvokeHelper.Invoke(UpdateTrailingDamage, 0.01f);
            }

            PB.rbody.useGravity = false;
            PB.rbody.velocity = Vector3.zero;
        }
        public override void WorldRemoval()
        {
            if (MaxTrailingHits != 0)
            {
                trailingCurTime = TrailingDamageTime;
                if (trailingCurTime == 0)
                    InvokeHelper.CancelInvoke(UpdatePositionalTracker);
                else
                    InvokeHelper.CancelInvoke(UpdateTrailingDamage);
            }
            base.WorldRemoval();
        }
        public void UpdatePositionalTracker()
        {
            lastVelo2 = lastVelo1;
            lastVelo1 = PB.rbody.velocity;
        }
        public void UpdateTrailingDamage()
        {
            if (trailingCurTime > 0)
            {
                trailingCooldownTime -= Time.deltaTime;

                float prevTime = trailingCurTime;
                trailingCurTime -= Time.deltaTime;
                float dmgFrame = MaxTrailingHits * ((prevTime - Mathf.Max(0, trailingCurTime)) / trailingCurTime);

                var targHit = RaycastProjectile.targHit;
                Vector3 fwdVec = lastVelo2.normalized;
                Vector3 offsetOrigin = transform.position - fwdVec;
                float distEnd = lastVelo2.magnitude;
                int hitNum = Physics.RaycastNonAlloc(new Ray(offsetOrigin, lastVelo2),
                    targHit, distEnd, RaycastProjectile.layerMask, QueryTriggerInteraction.Collide);


                RaycastHit hit;
                int hitIndex = -1;
                for (int step = 0; step < hitNum; step++)
                {
                    hit = targHit[step];
                    if (hit.distance > 0 && hit.distance <= distEnd)
                    {
                        if (hit.collider.gameObject.layer == Globals.inst.layerShieldBulletsFilter)
                        {
                            Visible vis = ManVisible.inst.FindVisible(hit.collider);
                            if (vis?.block?.tank && !vis.block.tank.IsEnemy(PB.shooter.Team))
                                continue;
                        }
                        hitIndex = step;
                        distEnd = hit.distance;
                    }
                }
                Vector3 nextPoint = offsetOrigin;
                if (hitIndex > -1)
                {
                    hit = targHit[hitIndex];
                    nextPoint = fwdVec * hit.distance;
                    if (trailingCooldownTime < 0)
                    {   // Aquire hit target
                        Damageable toDamage = hit.collider.GetComponentInParents<Damageable>(false);
                        // Keep hitting until our cooldown is over
                        while (trailingCooldownTime < 0)
                        {
                            trailingCooldownTime += TrailingDamageTime / MaxTrailingHits;
                            PB.project.HandleCollision(toDamage, hit.point, hit.collider, false);
                        }
                    }
                }
                else
                {
                    nextPoint += fwdVec * distEnd;
                    while (trailingCooldownTime < 0)
                    {
                        trailingCooldownTime += TrailingDamageTime / MaxTrailingHits;
                    }
                }
                trail.AddPosition(nextPoint);
            }
        }
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
                BlockDebug.ThrowWarning(true, "RandomAdditions: TrailProjectile expects an active TrailRenderer in hierarchy, but it is not enabled!");
            }
            m_DieAfterDelay_fetch.SetValue(PB.project, true);
            if (TrailingDamageTime <= 0 || MaxTrailingHits < 0)
                MaxTrailingHits = 0;
        }
    }
}
