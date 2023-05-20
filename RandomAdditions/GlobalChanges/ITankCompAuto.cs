using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Simple base class for quick components
    /// </summary>
    /// <typeparam name=""></typeparam>
    public interface ITankCompAuto<T,V> where T : MonoBehaviour where V : ExtModule
    {
        Tank tank { get; set; }
        T Inst { get; }
        HashSet<V> Modules { get; }

        void StartManagingPre();
        void StartManagingPost();
        void StopManaging();

        void AddModule(V eMod);

        void RemoveModule(V eMod);

    }

    public static class TankCompAutoExt
    {
        private static void StopManagingInternal<T, V>(ITankCompAuto<T, V> inst) where T : MonoBehaviour where V : ExtModule
        {
            inst.StopManaging();
            UnityEngine.Object.Destroy(inst.Inst);
        }

        private static ITankCompAuto<T, V> StartManaging<T, V>(Tank tank) where T : MonoBehaviour where V : ExtModule
        {
            if (tank.gameObject == null)
                throw new NullReferenceException("TankCompAuto.StartManaging() expects a Tank instance when called, but there was none!!");
            T man = tank.gameObject.AddComponent<T>();
            if (man == null)
                throw new NullReferenceException("TankCompAuto.StartManaging() FAILED to init due to fail in AddComponent<TankCompAuto<T, V>>()");
            if (!(man is ITankCompAuto<T, V> inst))
                throw new NullReferenceException("TankCompAuto.StartManaging() FAILED to init due to mismatch in TankCompAuto<T, V> parameters");
            inst.tank = tank;
            inst.StartManagingPre();
            inst.Inst.enabled = true;
            return inst;
        }
        public static void HandleAddition<T, V>(this ITankCompAuto<T, V> inst, V eMod) where T : MonoBehaviour where V : ExtModule
        {
            var tank = eMod.tank;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - TANK IS NULL");
                return;
            }
            var comp = tank.GetComponent<T>();
            if (!(bool)comp)
            {
                inst = StartManaging<T,V>(tank);
                if (!inst.Modules.Contains(eMod))
                    inst.Modules.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
                inst.StartManagingPost();
            }
            else if (comp is ITankCompAuto<T, V> inst2)
            {
                inst = inst2;
                if (!inst.Modules.Contains(eMod))
                    inst.Modules.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
            }
            inst.AddModule(eMod);
        }

        public static void HandleRemoval<T, V>(this ITankCompAuto<T, V> unused, V eMod) where T : MonoBehaviour where V : ExtModule
        {
            var tank = eMod.tank;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + "(HandleRemoval) - TANK IS NULL");
                return;
            }

            var comp = tank.GetComponent<T>();
            if (!(bool)comp)
            {
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - Got request to remove for tech " + tank.name
                    + " but there's no " + typeof(T).GetType() + " assigned?!?");
                return;
            }
            if (!(comp is ITankCompAuto<T, V> inst))
                throw new NullReferenceException("TankCompAuto.HandleRemoval() FAILED to init due to mismatch in TankCompAuto<T, V> parameters");
            inst.RemoveModule(eMod);
            if (!inst.Modules.Remove(eMod))
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType()
                    + " of " + eMod.name + " requested removal from " + tank.name + " but no such " + typeof(V).GetType() + " is assigned.");

            if (inst.Modules.Count() == 0)
            {
                StopManagingInternal(inst);
            }
        }
    }
}
