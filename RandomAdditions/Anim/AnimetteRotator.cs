using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class AnimetteRotator : RandomAdditions.AnimetteRotator { }
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

        protected override void Setup()
        {
            startRot = transform.localRotation;
            enabled = true;
        }

        protected override void UpdateTrans(float currentTime)
        {
            switch (Axis)
            {
                case ExtGimbalAxis.Y:
                    UpdateAimAngleY(currentTime);
                    break;
                case ExtGimbalAxis.Z:
                    UpdateAimAngleZ(currentTime);
                    break;
                default:
                    UpdateAimAngleX(currentTime);
                    break;
            }
        }
        internal void UpdateAimAngleX(float currentTime)
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            trans.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.up);
        }
        internal void UpdateAimAngleY(float currentTime)
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            trans.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.right);
        }
        internal void UpdateAimAngleZ(float currentTime)
        {
            angle = Mathf.Lerp(RotateRange[0], RotateRange[1], currentTime);
            trans.localRotation = startRot * Quaternion.AngleAxis(angle, Vector3.forward);
        }

    }
}
