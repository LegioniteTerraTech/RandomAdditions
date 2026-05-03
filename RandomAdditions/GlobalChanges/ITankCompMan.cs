using System;
using System.Collections.Generic;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// <b>DO NOT USE THIS, USE <see cref="ITankCompManagedList{T, V}"/> or <see cref="ITankCompManagedHash{T, V}"/> instead!</b>
    /// </summary>
    /// <typeparam name="T">Manager type</typeparam>
    /// <typeparam name="V">Our Component</typeparam>
    public interface ITankCompManaged<T, V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
    { 
        Tank tank { get; }
    }
    public interface ITankCompManagedHash<T, V> : ITankCompManaged<T, V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
    {
        T tankMan { get; set; }
    }
    public interface ITankCompManagedList<T, V> : ITankCompManaged<T, V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
    {
        T tankMan { get; set; }
    }

    /// <summary>
    /// <b>DO NOT USE THIS, USE <see cref="ITankCompManList{T, V}"/> or <see cref="ITankCompManHash{T, V}"/> instead!</b>
    /// </summary>
    /// <typeparam name="T">Manager type</typeparam>
    /// <typeparam name="V">Our Component</typeparam>
    public interface ITankCompMan<T, V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
    {
        /// <summary>
        /// The tank this is assigned to
        /// </summary>
        Tank tank { get; set; }

        /// <summary>
        /// Called when this is first created and starts being used
        /// </summary>
        void StartManagingPre();
        /// <summary>
        /// Called when this is first created and starts being used AFTER <see cref="StartManagingPre"/> after calling
        /// <see cref="AddModule"/> for the FIRST <typeparamref name="T"/> addition.
        /// </summary>
        void StartManagingPost();
        /// <summary>
        /// Called AFTER the last <typeparamref name="T"/> is removed.
        /// </summary>
        void StopManaging();

        /// <summary>
        /// <b>DO NOT CALL THIS DIRECTLY</b>
        /// <para>Use <see cref="TankCompManExt.HandleAddition{T, V}(ITankCompMan{T, V}, V)"/> to call this instead</para>
        /// </summary>
        /// <param name="eMod"><typeparamref name="T"/> to start managing (usually on attachment)</param>
        void AddModule(V eMod);

        /// <summary>
        /// <b>DO NOT CALL THIS DIRECTLY</b>
        /// <para>Use <see cref="TankCompManExt.HandleRemoval{T, V}(ITankCompMan{T, V}, V){T, V}(ITankCompMan{T, V}, V)"/> to call this instead</para>
        /// </summary>
        /// <param name="eMod"><typeparamref name="T"/> to stop managing (usually BEFORE detachment)</param>
        void RemoveModule(V eMod);

    }

    /// <summary>
    /// Simple base class for quick component managers that have to manage blocks across entire vehicles
    /// </summary>
    /// <typeparam name="T">Manager type</typeparam>
    /// <typeparam name="V">Our Component</typeparam>
    public interface ITankCompManHash<T,V> : ITankCompMan<T,V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManagedHash<T, V>
    {
        /// <summary>
        /// The managed list of modules this is assigned to
        /// </summary>
        HashSet<V> Managed { get; }
    }

    /// <summary>
    /// Simple base class for quick component managers that have to manage blocks across entire vehicles
    /// </summary>
    /// <typeparam name="T">Manager type</typeparam>
    /// <typeparam name="V">Our Component</typeparam>
    public interface ITankCompManList<T, V> : ITankCompMan<T, V> where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManagedList<T, V>
    {
        /// <summary>
        /// The managed list of modules this is assigned to
        /// </summary>
        List<V> Managed { get; }
    }


    public static class TankCompManExt
    {
        private static T StartManaging<T, V>(Tank tank)
            where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
        {
            if (tank.gameObject == null)
                throw new NullReferenceException("TankCompAuto.StartManaging() expects a Tank instance when called, but there was none!!");
            T man = tank.gameObject.AddComponent<T>();
            if (man == null)
                throw new NullReferenceException("TankCompAuto.StartManaging() FAILED to init due to fail in AddComponent<TankCompAuto<T, V>>()");
            if (!(man is T inst))
                throw new NullReferenceException("TankCompAuto.StartManaging() FAILED to init due to mismatch in TankCompAuto<T, V> parameters");
            inst.tank = tank;
            inst.StartManagingPre();
            inst.enabled = true;
            return inst;
        }
        private static void StopManagingInternal<T, V>(T inst) 
            where T : MonoBehaviour, ITankCompMan<T, V> where V : MonoBehaviour, ITankCompManaged<T, V>
        {
            inst.StopManaging();
            UnityEngine.Object.Destroy(inst);
        }
        public static void StartManagingHash<T, V>(this ITankCompManagedHash<T, V> mod)
            where T : MonoBehaviour, ITankCompManHash<T, V> where V : MonoBehaviour, ITankCompManagedHash<T, V>
        {
            V eMod = mod as V;
            var tank = eMod.tank;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - TANK IS NULL");
                return;
            }
            var comp = tank.GetComponent<T>();
            if (!(bool)comp)
            {
                comp = StartManaging<T,V>(tank);
                if (!comp.Managed.Contains(eMod))
                    comp.Managed.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
                comp.StartManagingPost();
            }
            else if (comp is T inst2)
            {
                comp = inst2;
                if (!comp.Managed.Contains(eMod))
                    comp.Managed.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
            }
            eMod.tankMan = comp;
            comp.AddModule(eMod);
        }

        public static void StartManagingList<T, V>(this ITankCompManagedList<T, V> mod)
            where T : MonoBehaviour, ITankCompManList<T, V> where V : MonoBehaviour, ITankCompManagedList<T, V>
        {
            V eMod = mod as V;
            var tank = eMod.tank;
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - TANK IS NULL");
                return;
            }
            var comp = tank.GetComponent<T>();
            if (!(bool)comp)
            {
                comp = StartManaging<T, V>(tank);
                if (!comp.Managed.Contains(eMod))
                    comp.Managed.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
                comp.StartManagingPost();
            }
            else if (comp is T inst2)
            {
                comp = inst2;
                if (!comp.Managed.Contains(eMod))
                    comp.Managed.Add(eMod);
                else
                    DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType() + " of " + eMod.name
                        + " was already added to " + tank.name + " but an add request was given?!?");
            }
            eMod.tankMan = comp;
            comp.AddModule(eMod);
        }

        public static void StopManagingHash<T, V>(this ITankCompManagedHash<T, V> mod)
            where T : MonoBehaviour, ITankCompManHash<T, V> where V : MonoBehaviour, ITankCompManagedHash<T, V>
        {
            V eMod = mod as V;
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
            if (!(comp is T inst))
                throw new NullReferenceException("TankCompAuto.HandleRemoval() FAILED to init due to mismatch in TankCompAuto<T, V> parameters");
            inst.RemoveModule(eMod);
            if (!inst.Managed.Remove(eMod))
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType()
                    + " of " + eMod.name + " requested removal from " + tank.name + " but no such " + typeof(V).GetType() + " is assigned.");
            eMod.tankMan = null;

            if (inst.Managed.Count == 0)
            {
                StopManagingInternal<T, V>(inst);
            }
        }
        public static void StopManagingList<T, V>(this ITankCompManagedList<T, V> mod)
            where T : MonoBehaviour, ITankCompManList<T, V> where V : MonoBehaviour, ITankCompManagedList<T, V>
        {
            V eMod = mod as V;
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
            if (!(comp is T inst))
                throw new NullReferenceException("TankCompAuto.HandleRemoval() FAILED to init due to mismatch in TankCompAuto<T, V> parameters");
            inst.RemoveModule(eMod);
            if (!inst.Managed.Remove(eMod))
                DebugRandAddi.Log("RandomAdditions: " + typeof(T).GetType() + " - " + typeof(V).GetType()
                    + " of " + eMod.name + " requested removal from " + tank.name + " but no such " + typeof(V).GetType() + " is assigned.");
            eMod.tankMan = null;

            if (inst.Managed.Count == 0)
            {
                StopManagingInternal<T,V>(inst);
            }
        }
    }
}
