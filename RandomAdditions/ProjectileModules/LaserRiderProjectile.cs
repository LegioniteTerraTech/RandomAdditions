using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    public class LaserRiderProjectile : ExtProj
    {
        public Vector3 WorldGravitateDirection = Vector3.up;
        public Vector3 GravitateCenter = Vector3.zero;
        public float WorldGravitateStrength = 0.01f;
        public bool WorldUseTerrainOffset = false;
        public bool WorldAugmentedDragEnabled = false;
        public float WorldAugmentedDragStrength = 0.1f;

        public float MovementDampening = 30;

        public bool WorldHeightBiasEnabled = false;
        public float WorldHeightBias = 50;

        public bool AffectedByWater = false;
        public float WaterDepthSeekingStrength = 1;
        public float WaterDepth = 1;

        /* 
        //thinking on this one, plausible for seeking sea mines or bomb drone rovers
        public bool ChaseTarget = false;
        public bool ChaseTargetIgnoreGround = false;
        public bool FaceTarget = false; 
        public float TargetSeekingStrength = 1f;
        */

        //AutoCollection
        public float floatHeight
        {
            get
            {
                if (WorldUseTerrainOffset)
                    return GetOffsetFromGround();
                return WorldHeightBias;
            }
        }
        private float movementDampening = 10;
        private float heightModifier = 1;
        private bool hasFiredOnce = false;
        private Transform thisTrans;


        public override void Pool()
        {
            thisTrans = gameObject.transform;
            if (MovementDampening < 0.001)
            {
                movementDampening = 1;
                DebugRandAddi.Log("RandomAdditions: Projectile " + gameObject.name + " has a MovementDampening value too close to or below zero!  Change it to a higher value!");
            }
            else
                movementDampening = MovementDampening;
            hasFiredOnce = true;
        }
        public float GetOffsetFromGround()
        {
            float final_y;
            float input = thisTrans.position.y;
            bool terrain = Singleton.Manager<ManWorld>.inst.GetTerrainHeight(thisTrans.position, out float height);
            if (terrain)
                final_y = height + WorldHeightBias;
            else
                final_y = WorldHeightBias;

            if (KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > height)
                    final_y = KickStart.WaterHeight + WorldHeightBias;
            }
            if (input < final_y)
            {
                input = final_y;
            }

            return input;
        }


        private void FixedUpdate()
        {
            if (!hasFiredOnce)
                Pool();

            Vector3 directionalForce = WorldGravitateDirection.normalized * WorldGravitateStrength;
            directionalForce.y *= heightModifier;
            PB.rbody.AddForceAtPosition(directionalForce, thisTrans.TransformPoint(GravitateCenter), ForceMode.Impulse);

            if (WorldAugmentedDragEnabled)
            {
                PB.rbody.velocity *= 1f - WorldAugmentedDragStrength;
            }
        }
    }
}
