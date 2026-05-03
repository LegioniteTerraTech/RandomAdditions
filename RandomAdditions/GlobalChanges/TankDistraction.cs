using System;
using System.Collections.Generic;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Handles all Tech Destractors
    /// </summary>
    public abstract class TankDestraction : MonoBehaviour
    {
        public abstract List<Destraction> Managed { get; set; }

        private Vector3 destractLocalPos = Vector3.zero;
        private float destractTimer = 0;
        private const float DestractTime = 0.6f;

        internal Vector3 GetPosDistract(Vector3 pos)
        {
            if (Time.time > destractTimer)
            {
                Managed.Shuffle();
                Vector3 pos2 = pos;
                float destractRad = 0;
                foreach (var item in Managed)
                {
                    destractRad += item.GetRadDistract();
                    if (item.GetPosDistract(pos, out pos2))
                    {
                        pos2 = pos;
                    }
                }
                destractLocalPos = transform.InverseTransformPoint(pos2) + (destractRad * UnityEngine.Random.insideUnitSphere);
                destractTimer = Time.time + DestractTime;
            }
            return transform.TransformPoint(destractLocalPos);
        }

        private void Update()
        {
            foreach (var item in Managed)
                item.UpdateThis();
        }
    }

    public class Destraction : MonoBehaviour
    {
        public Tank tank { get; set; }
        public TankDestraction tankMan { get; set; }

        internal virtual void UpdateThis() { }
        internal virtual float GetRadDistract()
        {
            return 0;
        }
        internal virtual bool GetPosDistract(Vector3 pos, out Vector3 dis2)
        {
            dis2 = pos;
            return false;
        }
    }
}
