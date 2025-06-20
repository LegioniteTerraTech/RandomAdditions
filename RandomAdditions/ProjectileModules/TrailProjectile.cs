using System;
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
        private float delay => trail.time;

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

            PB.rbody.useGravity = false;
            PB.rbody.velocity = Vector3.zero;
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
        }
    }
}
