using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Handles all Tech Destractors
    /// </summary>
    public class TankDestraction : MonoBehaviour
    {
        public static bool forceDisable = false;
        internal Tank tank;
        private List<Destraction> Destractions = new List<Destraction>();

        private Vector3 destractLocalPos = Vector3.zero;
        private float destractTimer = 0;
        private const float DestractTime = 0.6f;

        public static void HandleAddition(Tank tank, Destraction destract)
        {
            if (forceDisable)
                return;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction(HandleAddition) - TANK IS NULL");
                return;
            }
            var dis = tank.GetComponent<TankDestraction>();
            if (!(bool)dis)
            {
                dis = tank.gameObject.AddComponent<TankDestraction>();
                dis.tank = tank;
            }

            if (!dis.Destractions.Contains(destract))
            {
                dis.Destractions.Add(destract);
            }
            else
                DebugRandAddi.Log("RandomAdditions: TankDistraction - Destraction of " + tank.name + " was already added to " + tank.name + " but an add request was given?!?");
        }
        public static void HandleRemoval(Tank tank, Destraction destract)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction(HandleRemoval) - TANK IS NULL");
                return;
            }

            var dis = tank.GetComponent<TankDestraction>();
            if (!(bool)dis)
            {
                DebugRandAddi.Log("RandomAdditions: TankDistraction - Got request to remove for tech " + tank.name + " but there's no TankDistraction assigned?!?");
                return;
            }
            if (!dis.Destractions.Remove(destract))
                DebugRandAddi.Log("RandomAdditions: TankDistraction - ModuleMirage of " + destract.name + " requested removal from " + tank.name + " but no such ModuleMirage is assigned.");


            if (dis.Destractions.Count() == 0)
            {
                Destroy(dis);
            }
        }


        internal Vector3 GetPosDistract(Vector3 pos)
        {
            if (Time.time > destractTimer)
            {
                Destractions.Shuffle();
                Vector3 pos2 = pos;
                float destractRad = 0;
                foreach (var item in Destractions)
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
            foreach (var item in Destractions)
            {
                item.UpdateThis();
            }
        }
    }

    public class Destraction : MonoBehaviour
    {
        protected TankDestraction TD;
        protected Tank tank;

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
