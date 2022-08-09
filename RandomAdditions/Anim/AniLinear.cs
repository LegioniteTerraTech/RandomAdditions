using System;
using UnityEngine;

namespace RandomAdditions
{
    public abstract class AniLinear : MonoBehaviour
    {
        public float smooth = 0; //[-1 - 1] how much we should try accelerate and decelerate from positions

        public bool useAnimCurveInstead = false;
        public AnimationCurve AnimCurve = new AnimationCurve {
            keys = new Keyframe[2] {
                new Keyframe
                {
                    time = 0,
                    value = 0,
                    inWeight = 1,
                    outWeight = 1,
                    inTangent = 1,
                    outTangent = 1,
                },
                new Keyframe
                {
                    time = 1,
                    value = 1,
                    inWeight = 1,
                    outWeight = 1,
                    inTangent = 1,
                    outTangent = 1,
                },
            },
            preWrapMode = WrapMode.Default,
            postWrapMode = WrapMode.Default,
        };

        public AnimetteController anim;
        internal Transform trans; 
        internal float time = 1;  // In seconds

        internal void Init()
        {
            smooth = Mathf.Clamp(smooth, -1, 1);
            trans = transform;
            Setup();
        }
        protected virtual void Setup()
        {
        }

        internal void Init(AnimetteController MA)
        {
            anim = MA;
        }

        internal void UpdateThis(float curTime)
        {
            
            float blunt = curTime * (-Mathf.Abs(smooth) + 1f);
            float curve; 
            if (smooth == 0)
            {
                curve = 0;
            }
            else if (smooth > 0)
            {
                curve = Mathf.SmoothStep(0, 1, curTime) * smooth;
            }
            else
            {
                curve = (blunt * 2) + (Mathf.SmoothStep(0, 1, curTime) * smooth);
            }
            float finalTime;
            if (useAnimCurveInstead)
                finalTime = AnimCurve.Evaluate(blunt + curve);
            else
                finalTime = blunt + curve;
            //DebugRandAddi.Log("ANIMATION EVAL " + finalTime);
            UpdateTrans(finalTime);
        }
        protected virtual void UpdateTrans(float currentTime)
        {
        }
    }
}
