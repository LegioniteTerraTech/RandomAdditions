using System;
using UnityEngine;

namespace RandomAdditions
{
    public class AnimetteScaler : AniLinear
    {
        private Vector3 startLocalScale = Vector3.one;

        public Vector3 endLocalScale = Vector3.one * 2;

        public void OnPool()
        {
            startLocalScale = transform.localScale;
            enabled = false;
        }

        protected override void UpdateTrans()
        {
            transform.localPosition = Vector3.Lerp(startLocalScale, endLocalScale, currentTime);
        }
    }
}
