using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// A projectile type that exceeds the base game's projectile abilities. 
    ///   Offers more options with less issues
    /// </summary>
    public class AdvProjectile : MonoBehaviour
    {
        internal TankBlock launcher;
        internal Tank SourceTank => launcher.lastTank;

        internal Visible Target;

        private Action OnFired;
        /// <summary>
        /// launch ScenePos, aim direction, target
        /// </summary>
        public Event<Vector3, Quaternion, Visible> OnFire;

        public bool TryGetTargetPosition(out Vector3 scenePos)
        {
            if (Target)
            {
                switch (Target.m_ItemType.ObjectType)
                {
                    case ObjectTypes.Vehicle:
                        scenePos = Target.GetAimPoint(transform.position);
                        break;
                    case ObjectTypes.Block:
                    case ObjectTypes.Chunk:
                    case ObjectTypes.Waypoint:
                        scenePos = Target.centrePosition;
                        break;
                    case ObjectTypes.Scenery:
                    case ObjectTypes.Crate:
                        scenePos = Target.centrePosition + Vector3.up;
                        break;
                    default:
                        scenePos = Vector3.zero;
                        return false;
                }
                return true;
            }
            else
            {
                scenePos = Vector3.zero;
                return false;
            }
        }
    }
}
