using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class AnimetteAsset : RandomAdditions.AnimetteAsset { }
namespace RandomAdditions
{
    public class AnimetteAsset : AniLinear
    {
        private Animator anim;

        public string FieldName = "AnimetteAsset";
        private int FieldHash;
        private float FieldState;

        protected override void Setup()
        {
            anim = GetComponent<Animator>();
            FieldHash = FieldName.GetHashCode();
            DebugRandAddi.Assert(anim == null, "!! Failed to get Animator on GameObject " + gameObject.name);
            enabled = true;
        }

        protected override void UpdateTrans(float currentTime)
        {
            if (FieldState != currentTime)
            {
                FieldState = currentTime;
                anim.SetFloat(FieldHash, currentTime);
            }
        }
    }
}
