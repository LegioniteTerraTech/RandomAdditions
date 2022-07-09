using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Rotates a block when sent an Animator Signal
    /// </summary>
    public class AnimetteRotator : AniLinear
    {
        public ExtGimbalAxis Axis = ExtGimbalAxis.X;
        public float[] RotateRange = new float[2] { -180, 180 };

        private Quaternion startRot = Quaternion.identity;
        private float angle = 0;

        public void OnPool()
        {
            startRot = transform.localRotation;
            enabled = false;
        }

        protected override void UpdateTrans()
        {
            switch (Axis)
            {
                case ExtGimbalAxis.Y:
                    UpdateAimAngleY();
                    break;
                case ExtGimbalAxis.Z:
                    UpdateAimAngleZ();
                    break;
                default:
                    UpdateAimAngleX();
                    break;
            }
        }
        internal void UpdateAimAngleX()
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            transform.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.up);
        }
        internal void UpdateAimAngleY()
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            transform.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.right);
        }
        internal void UpdateAimAngleZ()
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            transform.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.forward);
        }

    }
}
