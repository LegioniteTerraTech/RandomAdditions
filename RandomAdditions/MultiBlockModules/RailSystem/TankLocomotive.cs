using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    public class TankLocomotive : MonoBehaviour
    {

        private Tank tank;
        private Transform cab => tank.rootBlockTrans;
        private EnergyRegulator reg;
        private List<ModuleRailBogey> BogeyBlocks = new List<ModuleRailBogey>();

        public static void HandleAddition(Tank tank, ModuleRailBogey bogey)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleAddition) - TANK IS NULL");
                return;
            }
            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                train = tank.gameObject.AddComponent<TankLocomotive>();
                train.tank = tank;
                train.reg = tank.EnergyRegulator;
                ManRails.railTechs.Add(train);
            }

            if (!train.BogeyBlocks.Contains(bogey))
                train.BogeyBlocks.Add(bogey);
            else
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModuleRailBogey of " + bogey.name + " was already added to " + tank.name + " but an add request was given?!?");
            bogey.engine = train;
        }
        public static void HandleRemoval(Tank tank, ModuleRailBogey bogey)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive(HandleRemoval) - TANK IS NULL");
                return;
            }

            var train = tank.GetComponent<TankLocomotive>();
            if (!(bool)train)
            {
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - Got request to remove for tech " + tank.name + " but there's no TankLocomotive assigned?!?");
                return;
            }
            if (!train.BogeyBlocks.Remove(bogey))
                DebugRandAddi.Log("RandomAdditions: TankLocomotive - ModulePointDefense of " + bogey.name + " requested removal from " + tank.name + " but no such ModulePointDefense is assigned.");
            bogey.engine = null;

            if (train.BogeyBlocks.Count() == 0)
            {
                ManRails.railTechs.Remove(train);
                if (ManRails.railTechs.Count == 0)
                {
                    //hasPointDefenseActive = false;
                }
                Destroy(train);
            }
        }

        public void StopAllBogeys()
        {
            foreach (var item in BogeyBlocks)
            {
                item.Halt();
            }
        }

        public void FixedUpdate()
        {
            if (tank.rbody)
            {
                if (BogeyBlocks.Count == 1)
                {   // Align with forwards facing
                    ModuleRailBogey MRB = BogeyBlocks.First();
                    Vector3 forwardsAim;
                    if (Vector3.Dot(MRB.BogeyRemote.forward, tank.rootBlockTrans.forward) > 0)
                    {
                        forwardsAim = MRB.BogeyRemote.forward;
                    }
                    else
                    {
                        forwardsAim = -MRB.BogeyRemote.forward;
                    }
                    Vector3 turnVal = Quaternion.LookRotation(
                        tank.rootBlockTrans.InverseTransformDirection(forwardsAim.SetY(0).normalized),
                        tank.rootBlockTrans.InverseTransformDirection(Vector3.up)).eulerAngles;

                    //Convert turnVal to runnable format
                    if (turnVal.x > 180)
                        turnVal.x = -((turnVal.x - 360) / 180);
                    else
                        turnVal.x = -(turnVal.x / 180);
                    if (turnVal.z > 180)
                        turnVal.z = -((turnVal.z - 360) / 180);
                    else
                        turnVal.z = -(turnVal.z / 180);
                    if (turnVal.y > 180)
                        turnVal.y = Mathf.Clamp(-((turnVal.y - 360) / 60), -1, 1);
                    else
                        turnVal.y = Mathf.Clamp(-(turnVal.y / 60), -1, 1);

                    // Turn it in
                    PHYRotateAxis(turnVal, MRB.LoneBogeyFacingThrust, MRB.LoneBogeyFacingAcceleration, MRB.LoneBogeyFacingDampener);
                }
            }
        }


        private const float RotationalLimit = 16f;      //
        private Vector3 CurRotat = Vector3.zero;
        private void PHYRotateAxis(Vector3 command, float torsionThrust, float torsionAccel, float torsionDamp)
        {
            Vector3 localAngleVelo = cab.InverseTransformVector(tank.rbody.angularVelocity);
            Vector3 localVelo = Vector3.Scale(cab.InverseTransformVector(tank.rbody.angularVelocity), tank.rbody.inertiaTensor) * 2;
            Vector3 InertiaDampenCheck = Vector3.zero;
            InertiaDampenCheck.x = localVelo.x;// * Mathf.Abs(localVelo.x);
            InertiaDampenCheck.y = localVelo.y;// * Mathf.Abs(localVelo.y);
            InertiaDampenCheck.z = localVelo.z;// * Mathf.Abs(localVelo.z);
            Vector3 InertiaDampen = Vector3.zero;
            float forceX;
            if (!PHYRotateDirect(-command.x, localAngleVelo.x, localVelo.x, torsionThrust, torsionAccel, ref CurRotat.x, out forceX))
                InertiaDampen.x = -InertiaDampenCheck.x * 2;
            float forceY;
            if (!PHYRotateDirect(-command.y, localAngleVelo.y, localVelo.y, torsionThrust, torsionAccel, ref CurRotat.y, out forceY))
                InertiaDampen.y = -InertiaDampenCheck.y * 2;
            float forceZ;
            if (!PHYRotateDirect(-command.z, localAngleVelo.z, localVelo.z, torsionThrust, torsionAccel, ref CurRotat.z, out forceZ))
                InertiaDampen.z = -InertiaDampenCheck.z * 2;

            Vector3 maxed = new Vector3(forceX, forceY, forceZ);
            if (!localAngleVelo.Approximately(Vector3.zero, 0.1f))
                InertiaDampen = Vector3.ClampMagnitude(InertiaDampen, torsionDamp);
            else
            {
                if (maxed.Approximately(Vector3.zero))
                {
                    tank.rbody.angularVelocity = Vector3.zero;  // FREEZE
                    return;
                }
            }
            if (!maxed.Approximately(Vector3.zero))
                maxed = Vector3.ClampMagnitude(maxed, torsionThrust);
            ApplyLocalRotationalForces(maxed + InertiaDampen);
        }

        private bool PHYRotateDirect(float mag, float currentInert, float currentInertFull, float torsionThrust, float torsionAccel, ref float toApply, out float forceToApply)
        {
            if (mag.Approximately(0))
            {
                toApply = 0;
                forceToApply = 0;
                return false;
            }
            else
            {
                toApply += Mathf.Sign(mag) * torsionAccel * Time.fixedDeltaTime;
                float target = Mathf.Abs(mag) * torsionThrust;
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
                float overspeed = RotationalLimit - Mathf.Abs(currentInert);
                if (overspeed < 0)
                {
                    forceToApply = -currentInertFull;
                    return false;
                }
                forceToApply = toApply;
                return true;
            }
        }
        private void ApplyLocalRotationalForces(Vector3 Force)
        {
            tank.rbody.AddRelativeTorque(Force * Time.fixedDeltaTime, ForceMode.Impulse);
        }
    }
}
