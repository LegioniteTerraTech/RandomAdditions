using System;
using UnityEngine;

public class AnimetteMover : RandomAdditions.AnimetteMover { }

namespace RandomAdditions
{
    public class AnimetteMover : AniLinear
    {
        private Vector3 startLocalPosition = Vector3.forward;

        public Vector3 endLocalPosition = Vector3.forward;


        protected override void Setup()
        {
            startLocalPosition = transform.localPosition;
            enabled = true;
        }

        protected override void UpdateTrans(float currentTime)
        {
            trans.localPosition = Vector3.Lerp(startLocalPosition, endLocalPosition, currentTime);
        }
    }
}
