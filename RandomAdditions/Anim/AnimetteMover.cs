using System;
using UnityEngine;

namespace RandomAdditions
{
    public class AnimetteMover : AniLinear
    {
        public Vector3 endLocalPosition = Vector3.forward;

        private Vector3 startLocalPosition = Vector3.forward;

        public void OnPool()
        {
            startLocalPosition = transform.localPosition;
            enabled = false;
        }

        protected override void UpdateTrans()
        {
            transform.localPosition = Vector3.Lerp(startLocalPosition, endLocalPosition, currentTime);
        }
    }
}
