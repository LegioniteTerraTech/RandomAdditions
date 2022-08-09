using System;
using UnityEngine;

public class AnimetteScaler : RandomAdditions.AnimetteScaler { }
namespace RandomAdditions
{
    public class AnimetteScaler : AniLinear
    {
        private Vector3 startLocalScale = Vector3.one;

        public Vector3 endLocalScale = Vector3.one * 2;

        protected override void Setup()
        {
            startLocalScale = transform.localScale;
            enabled = true;
        }

        protected override void UpdateTrans(float currentTime)
        {
            trans.localScale = Vector3.Lerp(startLocalScale, endLocalScale, currentTime);
        }
    }
}
