using UnityEngine;

public class GravitateProjectile : RandomAdditions.GravitateProjectile { };
namespace RandomAdditions
{
    public class GravitateProjectile : ExtProj
    {
        //A projectile that floats off (gravitationally-ish) in the specified direction with the specified force. 
        //  Can be used to create floating mines or balloon-like ordinances.
        /* Throw this within your JSONBLOCK's FireData m_BulletPrefab
        "RandomAdditions.GravitateProjectile":{ // Add a special movement effect to your projectile
            "WorldGravitateDirection":  {"x": 0, "y": 1, "z": 0}, // Gravitate direction
            "GravitatePosition":        {"x": 0, "y": 0, "z": 0}, // Center of the gravitation
            "WorldGravitateStrength": 0.01,     // Force of the gravitation

            "WorldAugmentedDragEnabled": false, // Should this projectile slow down faster? (WARNING! MESSES WITH AIMING WEAPONS!)
            "WorldAugmentedDragStrength": 0.1,  // The strength of the drag effect  [MULTIPLIER!]

            //-----------------------------------------------------------------------------
            "MovementDampening": 30,            // Dampener for the operations below
        
            "WorldHeightBiasEnabled": false,    // Should this float at a set altitude?
            "WorldHeightBias": 50,              // The height to float at
            "WorldUseTerrainOffset": false,     // Should this projectile float in relation to the height above terrain/water?

            "AffectedByWater": false,           // Should this projectile act differently in water?
            "WaterDepth": 1,                    // The depth to float at in relation to water (overrides WorldHeightBias)
            "WaterDepthSeekingStrength": 1,     // The strength we should try to enforce the depth  [MULTIPLIER!]
        },
        */
        //I mean well it lets you do sideways gravity but that's sorta strange at the moment.
        //  maybe that will change down the line but for now it's kinda pointless

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
            get {
                if (WorldUseTerrainOffset)
                    return GetOffsetFromGround();
                return WorldHeightBias;
            }
        }
        private float movementDampening = 10;
        private float heightModifier = 1;
        private bool hasFiredOnce = false;
        private Transform thisTrans;


        internal override void Pool()
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

            if (AffectedByWater && KickStart.isWaterModPresent)
            {
                if (KickStart.WaterHeight > gameObject.transform.position.y)
                {
                    heightModifier = Mathf.Clamp((KickStart.WaterHeight - WaterDepth - gameObject.transform.position.y) / movementDampening, -1, 1) * WaterDepthSeekingStrength;
                }
                else if (WorldHeightBiasEnabled)
                {
                    heightModifier = Mathf.Clamp((floatHeight - transform.position.y) / movementDampening, -1, 1);
                }
                else
                    heightModifier = 1;
            }
            else if (WorldHeightBiasEnabled)
            {
                heightModifier = Mathf.Clamp((floatHeight - transform.position.y) / movementDampening, -1, 1);
            }
            else
                heightModifier = 1;

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
