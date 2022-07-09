using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions
{
    //None of these should be used alone.
    public interface IExtGimbalControl
    {
        bool Linear();
    }

    public enum ExtGimbalAxis
    {
        Free,
        X,
        Y,
        Z,
    }

    public abstract class ExtGimbal : MonoBehaviour
    {
        protected IExtGimbalControl EGC;
        protected Vector3 rotAxis = Vector3.up;
        protected Quaternion startRotLocal = Quaternion.identity;
        protected float angle = 0;
        protected bool closeAim = false;

        public ExtGimbalAxis Axis = ExtGimbalAxis.X;
        public float[] AimRestrictions = new float[2] { -180, 180 };

        /// <summary>
        /// LOCAL
        /// </summary>
        protected Vector3 forwardsAim = Vector3.forward;
        /// <summary>
        /// LOCAL
        /// </summary>
        protected Vector3 upAim = Vector3.up;

        protected void OnPool()
        {
            if (AimRestrictions[0] > AimRestrictions[1])
            {
                float temp = AimRestrictions[1];
                AimRestrictions[1] = AimRestrictions[0];
                AimRestrictions[0] = temp;
            }
            float aimRange = AimRestrictions[1] - AimRestrictions[0];
            if (aimRange == 0 || 360 <= aimRange)
            {
                AimRestrictions[0] = -270;
                AimRestrictions[1] = 270;
            }
        }

        internal void Setup(IExtGimbalControl control)
        {

            startRotLocal = transform.localRotation;
            //Effector = transform.Find("Effector");
            EGC = control;
            switch (Axis)
            {
                case ExtGimbalAxis.X:
                    rotAxis = Vector3.up;
                    break;
                case ExtGimbalAxis.Y:
                    rotAxis = Vector3.right;
                    break;
                case ExtGimbalAxis.Z:
                    rotAxis = Vector3.forward;
                    break;
                default:
                    break;
            }
        }


        internal void ResetAim()
        {
            angle = 0;
            transform.localRotation = startRotLocal;
        }



        internal bool GetCloseEnough()
        {
            return closeAim;
        }
        internal void UpdateAim(float rotThisFrame)
        {
            switch (Axis)
            {
                case ExtGimbalAxis.X:
                    UpdateAimAngleX(rotThisFrame);
                    break;
                case ExtGimbalAxis.Y:
                    UpdateAimAngleY(rotThisFrame);
                    break;
                case ExtGimbalAxis.Z:
                    UpdateAimAngleZ(rotThisFrame);
                    break;
                default:
                    float dot = Vector3.Dot(transform.localRotation * Vector3.forward, forwardsAim);
                    if (EGC.Linear())
                    {
                        if (dot >= 0)
                        {
                            transform.localRotation =
                                Quaternion.RotateTowards(transform.localRotation,
                                Quaternion.LookRotation(forwardsAim, upAim), rotThisFrame);
                        }
                        else
                        {
                            transform.localRotation =
                                Quaternion.RotateTowards(transform.localRotation,
                                Quaternion.LookRotation(-forwardsAim, upAim), rotThisFrame);
                        }
                        closeAim = 0.85f < Mathf.Abs(dot);
                    }
                    else
                    {
                        transform.localRotation =
                            Quaternion.RotateTowards(transform.localRotation,
                            Quaternion.LookRotation(forwardsAim, upAim), rotThisFrame);
                        closeAim = 0.85f < dot;
                    }
                    break;
            }

        }
        internal void UpdateAimAngleX(float rotThisFrame)
        {
            Vector3 driveHeading;
            Vector3 driveHeadingR;
            if (EGC.Linear())
            {
                if (Vector3.Dot(transform.localRotation * Vector3.forward, forwardsAim) >= 0)
                {
                    driveHeading = Vector3.forward;
                    driveHeadingR = Vector3.right;
                }
                else
                {
                    driveHeading = Vector3.back;
                    driveHeadingR = Vector3.left;
                }
            }
            else
            {
                driveHeading = Vector3.forward;
                driveHeadingR = Vector3.right;
            }
            float aimedAngle = Vector3.Angle(transform.localRotation * driveHeading, forwardsAim.SetY(0).normalized);
            angle += Mathf.Clamp(aimedAngle, 0, rotThisFrame) *
                Mathf.Sign(Vector3.Dot(transform.localRotation * driveHeadingR, forwardsAim));
            closeAim = aimedAngle < rotThisFrame;
            angle = Mathf.Clamp(angle, AimRestrictions[0], AimRestrictions[1]);
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            transform.localRotation = Quaternion.AngleAxis(angle, rotAxis);//startRotLocal
        }
        internal void UpdateAimAngleY(float rotThisFrame)
        {
            Vector3 driveHeading;
            Vector3 driveHeadingD;
            if (EGC.Linear())
            {
                if (Vector3.Dot(transform.localRotation * Vector3.forward, forwardsAim) >= 0)
                {
                    driveHeading = Vector3.forward;
                    driveHeadingD = Vector3.down;
                }
                else
                {
                    driveHeading = Vector3.back;
                    driveHeadingD = Vector3.up;
                }
            }
            else
            {
                driveHeading = Vector3.forward;
                driveHeadingD = Vector3.down;
            }
            float aimedAngle = Vector3.Angle(transform.localRotation * driveHeading, forwardsAim.SetX(0).normalized);
            angle += Mathf.Clamp(aimedAngle, 0, rotThisFrame) *
                Mathf.Sign(Vector3.Dot(transform.localRotation * driveHeadingD, forwardsAim));
            closeAim = aimedAngle < rotThisFrame;
            angle = Mathf.Clamp(angle, AimRestrictions[0], AimRestrictions[1]);
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            transform.localRotation = Quaternion.AngleAxis(angle, rotAxis);//startRotLocal
        }
        internal void UpdateAimAngleZ(float rotThisFrame)
        {
            Vector3 driveHeadingU = Vector3.up;
            Vector3 driveHeadingR = Vector3.right;
            float aimedAngle = Vector3.Angle(transform.localRotation * driveHeadingU, forwardsAim.SetZ(0).normalized);
            angle += Mathf.Clamp(aimedAngle, 0, rotThisFrame) *
                Mathf.Sign(Vector3.Dot(transform.localRotation * driveHeadingR, upAim));
            closeAim = aimedAngle < rotThisFrame;
            angle = Mathf.Clamp(angle, AimRestrictions[0], AimRestrictions[1]);
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;
            transform.localRotation = Quaternion.AngleAxis(angle, rotAxis);//startRotLocal
        }

    }
}
