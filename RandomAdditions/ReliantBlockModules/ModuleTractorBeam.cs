using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SafeSaves;
using TerraTechETCUtil;

public class ModuleTractorBeam : RandomAdditions.ModuleTractorBeam { };
namespace RandomAdditions
{
    /// <summary>
    /// NEED TO CHECK NETCODE
    /// </summary>
    public class ModuleTractorBeam : ExtModule
    {
        private Transform forceEmitter;
        private Transform forceEmitterEnd;
        private TargetAimer TargetAimer;      // the controller that controls the GimbalAimers

        public Tank heldTech;

        public float MaxRange = 250;
        //public float MaxLiftCapacity = 250; // In TONS (1 TerraTech Mass Unit)
        public float MaxMoveForce = 250;
        public float LaunchForce = 2500;
        public bool ZeroPointEnergy = true;  // ignores Tech mass

        public Spinner spinner;
        public float ReactSpeed = 125;
        public float ExtendSpeed = 12;

        public Material BeamMaterial = null;
        public Color BeamColorStart = new Color(0.05f, 0.1f, 1f, 0.8f);
        public Color BeamColorEnd = new Color(0.05f, 0.1f, 1f, 0.8f);

        /// <summary>
        /// Where to move it to
        /// </summary>
        private Vector3 TargetWorld = Vector3.zero;
        private Vector3 CurTrans = Vector3.zero;
        private float MaxRangeSq = 250;

        protected override void Pool()
        {
            enabled = true;
            try
            {
                forceEmitter = KickStart.HeavyTransformSearch(transform, "_Emitter");
            }
            catch { }
            if (forceEmitter == null)
            {
                forceEmitter = this.transform;
                LogHandler.ThrowWarning("RandomAdditions: \nModuleTractorBeam NEEDS a GameObject in hierarchy named \"_Emitter\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
            }
            if (!(bool)spinner)
                spinner = GetComponentInChildren<Spinner>(true);
            MaxRangeSq = MaxRange * MaxRange;
            try
            {
                forceEmitterEnd = KickStart.HeavyTransformSearch(transform, "_Target");
            }
            catch { }
            if (forceEmitterEnd != null)
            {
                TargetAimer = GetComponent<TargetAimer>();
                if (TargetAimer == null)
                {
                    LogHandler.ThrowWarning("RandomAdditions: \nModuleTractorBeam NEEDS a valid TargetAimer in hierarchy!\nCause of error - Block " + gameObject.name);
                    return;
                }
                if (TargetAimer)
                {
                    InitTracBeam();
                }
            }
        }

        public override void OnAttach()
        {
            enabled = true;
            ExtUsageHint.ShowExistingHint(4010);
        }
        public override void OnDetach()
        {
            ReleaseTech();
            enabled = false;
        }


        public bool IsInRange(Tank tech)
        {
            return MaxRangeSq >= (tech.WorldCenterOfMass - forceEmitter.position).sqrMagnitude;
        }

        /// <summary>
        /// Check if this can carry a Tech
        /// </summary>
        /// <param name="tech">The Tech to check</param>
        /// <returns>True if it can move it</returns>
        public bool CanCarryTech(Tank tech, bool debugLog = false)
        {
            if (!tech)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - TECH IS NULL - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech == tank)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Tech tried to grab itself - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!tech.visible.isActive)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Target tech is not active - Are we trying to grab at the edge of the scene? - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!tech.rbody)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Can't grab static/anchored Techs. - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (tech.Team == ManSpawn.NeutralTeam)
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Target tech is neutral - We are not allowed to grab neutrals how dare you - \n" + StackTraceUtility.ExtractStackTrace());
                return false;
            }
            else if (!IsInRange(tech))
            {
                if (debugLog)
                    DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Target tech is out of Range radius " + MaxRange + " - \n" + StackTraceUtility.ExtractStackTrace());
                return false;   // Tech is Out of Range
            }
            if (!ZeroPointEnergy) // We have to care about weight
            {
                float techWeightMin = (tech.rbody.mass * tech.GetGravityScale()) + 25;
                if (techWeightMin > MaxMoveForce)
                {
                    if (debugLog)
                        DebugRandAddi.Log("RandomAdditions: ModuleTractorBeam - Target tech exceeds max carrying capacity: Capacity " + MaxMoveForce + " vs Tech weight " + techWeightMin + " - \n" + StackTraceUtility.ExtractStackTrace());
                    return false;   // TOO HEAVY 
                }
            }
            return true;
        }
        /// <summary>
        /// Are we holding a Tech?
        /// </summary>
        /// <returns>True if we are holding a valid Tech</returns>
        public bool HasTech()
        {
            return heldTech;
        }
        /// <summary>
        /// Releases hold of the held Tech
        /// </summary>
        public void ReleaseTech()
        {
            if (canUseBeam)
                StopBeam();
            heldTech = null;
        }
        /// <summary>
        /// Throws the held Tech directly away from the tractor beam and releases it.
        /// </summary>
        public void ThrowTech()
        {
            if (heldTech.rbody && ManNetwork.IsHost)
                heldTech.rbody.AddForce((heldTech.boundsCentreWorld - forceEmitter.position).normalized * LaunchForce, ForceMode.Impulse);
            ReleaseTech();
        }
        /// <summary>
        /// Set the target of this ModuleTractorBeam. Will validate automatically but
        /// </summary>
        /// <param name="tech">The Tech to give to this ModuleTractorBeam</param>
        /// <param name="debugLog">Log why this isn't accepting a tech</param>
        /// <returns>True if it could grab the Tech</returns>
        public bool GrabTech(Tank tech, bool debugLog = false)
        {
            if (!CanCarryTech(tech, debugLog))
                return false;
            heldTech = tech;
            return true;
        }
        /// <summary>
        /// Set ModuleTractorBeam's final target location to move the held Tech.
        /// </summary>
        /// <param name="ScenePos">The position in World (non-save worldPosition) for this to move the grabbed tech to</param>
        public void SetTargetWorld(Vector3 ScenePos)
        {
            TargetWorld = ScenePos;
        }


        public void FixedUpdate()
        {
            if (heldTech)
            {  
                if (!CanCarryTech(heldTech))
                    heldTech = null;
                else
                {
                    if (canUseBeam)
                    {
                        if (forceEmitterEnd.InverseTransformPoint(heldTech.WorldCenterOfMass).Approximately(Vector3.zero, distVar + 1f))
                            PhysicsCarryTech(TargetWorld, heldTech);
                    }
                    else
                        PhysicsCarryTech(TargetWorld, heldTech);
                }
            }
        }


        // TRANSLATIONAL
        private void PhysicsCarryTech(Vector3 targetWorld, Tank tech)
        {
            if (!ManNetwork.IsHost)
                return; // Only the host does physics
            Vector3 commandMove;
            if (ZeroPointEnergy)
            {
                commandMove = Vector3.ClampMagnitude(targetWorld - tech.boundsCentreWorld , 1);
                PhysicsCarryTranslate(tech.rootBlockTrans.InverseTransformVector(commandMove), tech, true);
            }
            else
            {
                Vector3 movedirect = targetWorld - tech.boundsCentreWorld;
                commandMove = Vector3.ClampMagnitude(movedirect + (Vector3.up * Mathf.Clamp(Vector3.Dot(movedirect, Vector3.up) * 3, 0, 1) * tech.rbody.mass * tech.GetGravityScale()), 1);
                PhysicsCarryTranslate(tech.rootBlockTrans.InverseTransformVector(commandMove), tech);
            }
        }
        private void PhysicsCarryTranslate(Vector3 command, Tank tank, bool ignoreGravity = false)
        {
            Vector3 localVelo = tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity);
            Vector3 InertiaDampenCheck = Vector3.zero;
            InertiaDampenCheck.x = localVelo.x * tank.rbody.mass;
            InertiaDampenCheck.y = localVelo.y * tank.rbody.mass;
            InertiaDampenCheck.z = localVelo.z * tank.rbody.mass;
            Vector3 InertiaDampen = Vector3.zero;
            float forceX;
            if (!PhysicsTranslate(command.x, InertiaDampenCheck.x, ref CurTrans.x, out forceX))
                InertiaDampen.x = -InertiaDampenCheck.x;
            float forceY;
            if (!PhysicsTranslate(command.y, InertiaDampenCheck.y, ref CurTrans.y, out forceY))
                InertiaDampen.y = -InertiaDampenCheck.y;
            float forceZ;
            if (!PhysicsTranslate(command.z, InertiaDampenCheck.z, ref CurTrans.z, out forceZ))
                InertiaDampen.z = -InertiaDampenCheck.z;

            Vector3 maxed = new Vector3(forceX, forceY, forceZ);
            if (!localVelo.Approximately(Vector3.zero))
                InertiaDampen = Vector3.ClampMagnitude(InertiaDampen, MaxMoveForce);
            else
            {
                if (maxed.Approximately(Vector3.zero))
                {
                    tank.rbody.velocity = Vector3.zero;  // FREEZE
                    return;
                }
            }
            if (!maxed.Approximately(Vector3.zero))
                maxed = Vector3.ClampMagnitude(maxed, MaxMoveForce);
            if (ignoreGravity)
                ApplyGlobalTranslationalForces(tank.rootBlockTrans.TransformVector(maxed + InertiaDampen) + (Vector3.up * tank.rbody.mass * tank.GetGravityScale()), tank);
            else
                ApplyGlobalTranslationalForces(tank.rootBlockTrans.TransformVector(maxed + InertiaDampen), tank);
        }
        private bool PhysicsTranslate(float mag, float currentInert, ref float toApply, out float forceToApply)
        {
            if (mag.Approximately(0))
            {
                toApply = 0;
                forceToApply = 0;
                return false;
            }
            else if (Mathf.Sign(mag) == Mathf.Sign(currentInert))
            {
                toApply += Mathf.Sign(mag) * MaxMoveForce * Time.fixedDeltaTime;
                float target = Mathf.Abs(mag) * MaxMoveForce;
                if (Mathf.Sign(mag) > 0)
                {
                    if (toApply < 0)
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, 0, target);
                    }
                }
                else
                {
                    if (toApply > 0)
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                        forceToApply = 0;
                        return false;
                    }
                    else
                    {
                        toApply = Mathf.Clamp(toApply, -target, 0);
                    }
                }

                forceToApply = toApply;
                return true;
            }
            forceToApply = Mathf.Sign(currentInert) * -MaxMoveForce;
            return true;
        }

        private static Vector3 OffsetGrabPoint = Vector3.up * 2;
        private void ApplyGlobalTranslationalForces(Vector3 Force, Tank tank)
        {
            tank.rbody.AddForceAtPosition(Force * Time.fixedDeltaTime, tank.transform.TransformPoint(tank.CenterOfMass + OffsetGrabPoint), ForceMode.Impulse);
        }



        // BEAM EFFECT
        public void Update()
        {
            if (canUseBeam)
            {
                if (heldTech)
                {
                    StartBeam();
                    TargetAimer.AimAtWorldPos(heldTech.WorldCenterOfMass, ReactSpeed);
                    float aimZDist = forceEmitterEnd.parent.InverseTransformPoint(heldTech.WorldCenterOfMass).z;
                    UpdateTargetDist(aimZDist);
                    UpdateTracBeam();
                }
                else
                {
                    StopBeam();
                    Vector3 defaultAim = forceEmitterEnd.position + (25 * block.trans.forward);
                    TargetAimer.AimAtWorldPos(defaultAim, ReactSpeed);
                    UpdateTargetDist(targeterZOffset);
                }
            }
        }

        private LineRenderer TracBeamVis;
        private bool canUseBeam = false;
        private float targeterZOffset = 0;
        private float lastZDist = 0;
        private float TargetRad = 0;
        private float animPulse = 0;
        private const float animSpeedMulti = 1.5f;
        private const float distVar = 1.5f;
        private void InitTracBeam()
        {
            Transform TO = transform.Find("TracLine");
            GameObject gO = null;
            if ((bool)TO)
                gO = TO.gameObject;
            if (!(bool)gO)
            {
                gO = Instantiate(new GameObject("TracLine"), transform, false);
                gO.transform.localPosition = Vector3.zero;
                gO.transform.localRotation = Quaternion.identity;
            }
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                if (BeamMaterial != null)
                    lr.material = BeamMaterial;
                else
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.positionCount = 2;
                lr.endWidth = 0.1f;
                lr.startWidth = 1;
                lr.useWorldSpace = true;
                lr.startColor = BeamColorStart;
                lr.endColor = BeamColorEnd;
                lr.numCapVertices = 8;
                lr.SetPositions(new Vector3[2] { new Vector3(0, 0, -1), Vector3.zero });
            }
            TracBeamVis = lr;
            TracBeamVis.gameObject.SetActive(false);
            canUseBeam = true;
        }
        private void UpdateTracBeam()
        {
            Vector3 exts = heldTech.blockBounds.extents;
            float aimedScale = (bool)heldTech ? Mathf.Max(exts.x, exts.y, exts.z, 1) : 1;

            float sizeChangeRate = 0.25f;
            float sizeChange = aimedScale - TargetRad;
            TargetRad += sizeChange * sizeChangeRate;
            TracBeamVis.startWidth = TargetRad;
            animPulse += Time.deltaTime * animSpeedMulti;
            if (animPulse > 3.14f)
                animPulse -= 3.14f;
            float pulse = (Mathf.Cos(animPulse) / 4) + 0.7f;
            /*
            TracBeamVis.startColor = new Color(0.25f, 1, 0.25f, 0.5f * pulse);
            TracBeamVis.endColor = new Color(0.1f, 1, 0.1f, 0.75f * pulse);*/
            TracBeamVis.positionCount = 2;
            TracBeamVis.SetPositions(new Vector3[2] { forceEmitterEnd.position + (Vector3.up * (pulse - 0.5f) * TargetRad), forceEmitterEnd.TransformPoint(Vector3.forward * -(lastZDist - targeterZOffset)) });
        }
        private void StartBeam()
        {
            if ((bool)spinner)
                spinner.SetAutoSpin(true);
            if (!TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(true);
            }
        }
        private void StopBeam()
        {
            if ((bool)spinner)
                spinner.SetAutoSpin(false);
            if (TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(false);
            }
        }
        private void UpdateTargetDist(float aimZDist)
        {
            float distChange;
            if (aimZDist > lastZDist + distVar)
            {
                distChange = lastZDist + ExtendSpeed * Time.deltaTime;
                if (distChange > MaxRange)
                    distChange = MaxRange;
                forceEmitterEnd.localPosition = forceEmitterEnd.localPosition.SetZ(distChange);
                lastZDist = distChange;
            }
            else if (aimZDist < lastZDist - distVar)
            {
                distChange = aimZDist;
                forceEmitterEnd.localPosition = forceEmitterEnd.localPosition.SetZ(distChange);
                lastZDist = distChange;
            }
        }
    }
}
