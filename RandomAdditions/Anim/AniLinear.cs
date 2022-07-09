using System;
using UnityEngine;

namespace RandomAdditions
{
    public abstract class AniLinear : MonoBehaviour
    {
        public float smooth = 1; //[0.1 - 360] how much we should try accelerate and decelerate from positions

        private ManAnimette anim;
        internal float time = 1;  // In seconds
        protected float targetTime = 0;
        protected float currentTime = 0;

        private void OnPool()
        {
            smooth = Mathf.Clamp(smooth, -1, 1);
        }

        internal void Init(ManAnimette MA)
        {
            anim = MA;
        }

        internal void UpdateThis(float curTime)
        {
            float blunt = curTime * (Mathf.Abs(smooth) - 1f);
            float curve; 
            if (smooth == 0)
            {
                curve = 0;
            }
            else if (smooth > 0)
            {
                curve = Mathf.SmoothStep(currentTime, targetTime, curTime) * smooth;
            }
            else
            {
                curve = (blunt * 2) + (Mathf.SmoothStep(currentTime, targetTime, curTime) * smooth);
            }
            currentTime = blunt + curve;
            UpdateTrans();
            if (currentTime.Approximately(targetTime))
                enabled = false;
        }
        protected virtual void UpdateTrans()
        {
        }
    }
}
