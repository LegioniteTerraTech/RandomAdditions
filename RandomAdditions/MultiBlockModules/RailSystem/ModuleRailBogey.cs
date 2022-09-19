using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;

namespace RandomAdditions
{
    // Might come in 2024, we'll see
    // Connects a Tech to the rails
    public class ModuleRailBogey : ExtModule
    {
        private static float MaxRailVelocity = 350;

        internal TankLocomotive engine;
        internal RailTrack network;
        internal ManRails.RailSegment CurrentRail;

        public Transform BogeyCenter { get; private set; }
        public Vector3 BogeyCenterOffset => BogeyCenter.position + (Vector3.down * BogeyOffsetDist);
        public Transform BogeyRemote { get; private set; }


        public RailSystemType RailSystemType = RailSystemType.BeamRail;
        public float DriveForce = 65f;
        public float DriveBrakeForce = 45f;
        public float DampenerPercent = 0.35f;
        public float SpringTensionForce = 750;
        public float ForwardsTensionForce = 150;
        public float LoneBogeyFacingThrust = 500;
        public float LoneBogeyFacingDampener = 500;
        public float LoneBogeyFacingAcceleration = 1750;
        public float BogeyOffsetDist = 1;
        public float BogeyFollowPercent = 0.65f;
        public float MaxForceDistance = 3;
        public float TetherScale = 0.4f;

        public Material BeamMaterial = null;
        public Color BeamColorStart = new Color(0.05f, 0.1f, 1f, 0.8f);
        public Color BeamColorEnd = new Color(0.05f, 0.1f, 1f, 0.8f);


        internal int CurrentRailIndex = 0;
        internal float PositionOnRail = 0;
        private float MaxForceDistanceSqr = 3;
        internal Vector3 BogeyManPosition;
        internal Quaternion BogeyFacing;
        private float velocityForwards = 0;
        private Vector3 driveDirection = Vector3.zero;

        protected override void Pool()
        {
            enabled = false;
            try
            {
                BogeyCenter = KickStart.HeavyObjectSearch(transform, "_bogeyCenter");
            }
            catch { }
            if (BogeyCenter == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailwayBogey NEEDS a GameObject in hierarchy named \"_bogeyCenter\" for the tractor beam effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }

            try
            {
                BogeyRemote = KickStart.HeavyObjectSearch(transform, "_bogeyMain");
            }
            catch { }
            if (BogeyRemote == null)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailwayBogey NEEDS a GameObject in hierarchy named \"_bogeyMain\" for the rail bogey effect!\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }

            MaxForceDistanceSqr = MaxForceDistance * MaxForceDistance;
            //AC = KickStart.FetchAnimette(transform, "_Tether", AnimCondition.Tether);
            //block.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
        }


        public override void OnAttach()
        {
            DebugRandAddi.Log("OnAttach - ModuleRailBogey");
            TankLocomotive.HandleAddition(tank, this);
            tank.control.driveControlEvent.Subscribe(DriveCommand);
            enabled = true;
        }

        public override void OnDetach()
        {
            DebugRandAddi.Log("OnDetach - ModuleRailBogey");
            if (network != null)
                network.RemoveBogey(this);
            enabled = false;
            tank.control.driveControlEvent.Unsubscribe(DriveCommand);
            TankLocomotive.HandleRemoval(tank, this);
            DetachBogey();
        }


        private Vector3 PHYThrustDampen3D()
        {
            Vector3 localVelo = tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity);
            Vector3 InertiaDampenCheck = Vector3.zero;
            InertiaDampenCheck.x = PHYThrustDampen(localVelo.x);
            InertiaDampenCheck.y = PHYThrustDampen(localVelo.y);
            InertiaDampenCheck.z = PHYThrustDampen(localVelo.z);
            return  tank.rootBlockTrans.InverseTransformVector(tank.rbody.velocity);
        }
        private float PHYThrustDampen(float velo)
        {
            return velo * tank.rbody.mass;
        }
        /// <summary>
        /// Force the controller to float over the rails
        /// </summary>
        private void FixedUpdate()
        {
            if (CurrentRail && tank.rbody)
            {   // Apply rail-binding forces
                Vector3 bogeyOffset = BogeyCenterOffset - BogeyRemote.position;
                Vector3 bogeyFollowForce;
                if (bogeyOffset.Approximately(Vector3.zero, 0.5f))
                    bogeyFollowForce = Vector3.zero;
                else
                    bogeyFollowForce = bogeyOffset;
                if (driveDirection.ApproxZero())
                {
                    velocityForwards -= Mathf.Sign(velocityForwards) * Mathf.Min(Mathf.Abs(velocityForwards), 
                        Time.fixedDeltaTime * (DriveBrakeForce / tank.rbody.mass));
                    if (bogeyOffset.sqrMagnitude > 4)
                        PositionOnRail += Time.fixedDeltaTime * (velocityForwards
                            + BogeyRemote.InverseTransformVector(bogeyFollowForce * BogeyFollowPercent).z);
                    else
                        PositionOnRail += Time.fixedDeltaTime * velocityForwards;
                }
                else
                {
                    velocityForwards += Time.fixedDeltaTime * (BogeyRemote.InverseTransformVector(driveDirection * DriveForce).z 
                        / tank.rbody.mass);
                    PositionOnRail += Time.fixedDeltaTime * (velocityForwards
                        + BogeyRemote.InverseTransformVector(bogeyFollowForce * BogeyFollowPercent).z);
                }
                VeloSlowdown();
                ManRails.UpdateRailBogey(this);
                //DebugRandAddi.Log(block.name + " - velo " + velocityForwards + ", pos " + PositionOnRail);
                float distance = bogeyOffset.sqrMagnitude;

                float pullForce = Mathf.Clamp(Mathf.InverseLerp(0, MaxForceDistanceSqr, distance), 0.0f, 1);
                Vector3 directedForce = BogeyRemote.InverseTransformVector(bogeyOffset.normalized * pullForce);
                directedForce.x *= SpringTensionForce;
                directedForce.y *= SpringTensionForce;
                directedForce.z *= ForwardsTensionForce;
                tank.rbody.AddForceAtPosition(BogeyRemote.TransformVector(-directedForce), BogeyCenter.position, ForceMode.Force);
                tank.rbody.AddRelativeForce(tank.rbody.transform.InverseTransformVector(tank.rbody.velocity) * -DampenerPercent, ForceMode.Acceleration);
            }
            else
            {   // Try find a rail 
                CurrentRail = ManRails.TryGetAndAssignClosestRail(this, out int railIndex);
                if (CurrentRail)
                {
                    CurrentRailIndex = railIndex;
                    BogeyRemote.position = CurrentRail.GetClosestPointOnRail(BogeyRemote.position, out PositionOnRail);
                    PositionOnRail *= CurrentRail.RoughLineDist;
                    DebugRandAddi.Log(block.name + " Found and fixed to a rail");
                }
            }
        }
        public void AddForceThisFixedFrame(float add)
        {
            velocityForwards += add;
        }
        public void InvertVelocity()
        {
            velocityForwards = -velocityForwards;
        }
        public void Halt()
        {
            velocityForwards = 0;
        }
        private void VeloSlowdown()
        {
            velocityForwards -= Time.fixedDeltaTime * velocityForwards * 0.1f;
            velocityForwards = Mathf.Clamp(velocityForwards, -MaxRailVelocity, MaxRailVelocity);
        }

        public void DetachBogey()
        {
            BogeyRemote.localPosition = Vector3.zero;
            BogeyRemote.localRotation = Quaternion.identity;

            network = null;
            CurrentRail = null;
            CurrentRailIndex = 0;
            PositionOnRail = 0;
            velocityForwards = 0;
        }

        private void DriveCommand(TankControl.ControlState controlState)
        {
            Vector3 move = Vector3.ClampMagnitude(controlState.Throttle + controlState.InputMovement, 1);
            driveDirection = tank.rootBlockTrans.TransformVector(move);
        }

        public void Update()
        {
            if (CurrentRail)
            {
                BogeyFacing = CurrentRail.CalcBogeyFacing(this, out BogeyManPosition);
                BogeyRemote.position = BogeyManPosition;
                BogeyRemote.rotation = BogeyFacing;
            }
            else
            {
                BogeyRemote.localPosition = Vector3.zero;
                BogeyRemote.localRotation = Quaternion.identity;
            }
        }




        private LineRenderer TracBeamVis;
        private bool canUseBeam = false;
        private bool BeamSide = false;
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
                lr.endWidth = TetherScale;
                lr.startWidth = TetherScale;
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
            TracBeamVis.startColor = new Color(0.25f, 1, 0.25f, 0.9f);
            TracBeamVis.endColor = new Color(0.1f, 1, 0.1f, 0.9f);
            TracBeamVis.positionCount = 2;
            TracBeamVis.SetPositions(new Vector3[2] { BogeyCenter.position, BogeyRemote.position });
        }
        private void StartBeam()
        {
            if (!TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(true);
            }
        }
        private void StopBeam()
        {
            if (TracBeamVis.gameObject.activeSelf)
            {
                TracBeamVis.gameObject.SetActive(false);
            }
        }
    }
}
