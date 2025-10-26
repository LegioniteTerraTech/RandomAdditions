using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;
using static WaterMod.SurfacePool;

namespace RandomAdditions
{
    /// <summary>
    /// some pointless attempts to optimize the game!
    /// </summary>
    internal class Optimax : MonoBehaviour
    {
        internal class DurationTracker
        {
            public string target;
            public Stopwatch timer;
            public long totalCycle;
            public long totalPeak;
            public long lastPeakSteps;
        }
        internal const int ColDefaultIterations = 1;
        internal const int VelDefaultIterations = 1;
        internal const int ColTankIterations = 2;
        internal const int VelTankIterations = 1;


        internal static Optimax inst;
        internal static bool optimize = false;

        private static int ColDefaultIterations_Prev = -1;
        private static int VelDefaultIterations_Prev = 0;
        private static Dictionary<int, Dictionary<Type, DurationTracker>> DeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> FixedDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> LateDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static void Init()
        {
            if (inst)
                return;
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.First, OnStaticUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, () => { EndTrackingUpdate0<ManUpdate>(); }, -90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, OnStaticFixedUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, () => { EndTrackingFixedUpdate0<ManUpdate>(); }, -90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.First, OnStaticLateUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, () => { EndTrackingLateUpdate0<ManUpdate>(); }, -90010);
            inst = new GameObject("Optimaxxer").AddComponent<Optimax>();
            BeginTrackingUpdate0<ManUpdate>();
            BeginTrackingFixedUpdate0<ManUpdate>();
            TrackTypeUpdate<MB_Update>("Update");
            TrackTypeFixed<MB_FixedUpdate>("FixedUpdate");
            TrackTypeLate<MB_LateUpdate>("LateUpdate");
            TrackTypeUpdate<ManMods>("Update");
            TrackTypeFixed<ManMods>("FixedUpdate");
            //TrackTypeUpdate<ManTileLoader>("Update");// Doesn't exists
            TrackTypeUpdate<Tank>("Update");
            TrackTypeFixed<Tank>("FixedUpdate");
            TrackTypeUpdate<HoverJet>("OnUpdate");
            TrackTypeFixed<HoverJet>("OnFixedUpdate");
            TrackTypeUpdate<TechWeapon>("OnUpdate");
            TrackTypeUpdate<TechVision>("GetFirstVisibleTechIsEnemy");
            TrackTypeFixed<ManWheels>("OnFirstFixedUpdate");
            TrackTypeFixed<ManWheels>("OnLastFixedUpdate");
            /*
            TrackTypeFixed(typeof(Tank), "OnCollisionEnter");
            TrackTypeFixed(typeof(Tank), "OnCollisionStay");
            TrackTypeFixed(typeof(Tank), "OnTriggerEnter");
            TrackTypeFixed(typeof(Tank), "OnTriggerExit");
            */
            TrackTypeUpdate<TileManager>("UpdateTileCache");
            TrackTypeUpdate<ManVisible.SearchIterator>("InitSearch");
            //TrackTypeFixed(typeof(Visible), "OnFixedUpdate");
            TrackTypeUpdate<ManPointer>("UpdateTargetsFromNearby");
            TrackTypeUpdate<UIMiniMapDisplay>("Update");
            if (KickStart.isNuterraSteamPresent)
            {
                try
                {
                    Init2();
                }
                catch { }
            }
        }
        private static void Init2()
        {
            TrackTypeUpdate(typeof(CustomModules.GameObjectExtensions), "RecursiveFindWithProperties");
        }
        public static void SetActive(bool state)
        {
            if (state)
                Init();
            if (inst)
                inst.gameObject.SetActive(state);
        }


        private static Rect HotWindow = new Rect(0, 0, 400, 500);   // the "window"
        private static Vector2 scroll = default;   // the "window"
        public void OnGUI()
        {
            HotWindow = AltUI.Window(13213124, HotWindow, GUIHandler,
                "Processing", CloseMenu);
        }
        public void GUIHandler(int ID)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Update", AltUI.LabelBlackTitle);
            GUILayout.FlexibleSpace();
            //GUILayout.Label(Time.deltaTime.ToString("0.000"));
            long combined = 0;
            foreach (var item in DeltaTime)
                foreach (var item2 in item.Value)
                    combined += item2.Value.timer.ElapsedMilliseconds;
            GUILayout.Label((combined / 1000f).ToString("0.000"));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked Update()");
            GUILayout.Label("|", AltUI.LabelBlue);
            GUILayout.Label((combined / (Time.deltaTime * 1000f)).ToString("p"));
            AltUI.Tooltip.GUITooltip("Total tracked time / reported Unity Time.deltaTime.  Time.deltaTime: " + 
                Time.deltaTime.ToString("0.000"));
            GUILayout.EndHorizontal();
            foreach (var item in DeltaTime)
                foreach (var item2 in item.Value)
                {
                    GUILayout.BeginHorizontal(AltUI.TextfieldBlack);
                    GUILayout.Label(item2.Key.Name);
                    GUILayout.Label(item2.Value.target);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((item2.Value.totalPeak / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("Peak in the last 80 calls");
                    GUILayout.Label("|");
                    GUILayout.Label((item2.Value.totalCycle / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("This cycle");
                    GUILayout.EndHorizontal();
                }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Fixed", AltUI.LabelBlackTitle);
            GUILayout.FlexibleSpace();
            //GUILayout.Label(Time.fixedDeltaTime.ToString("0.000"));
            long combined2 = 0;
            foreach (var item in FixedDeltaTime)
                foreach (var item2 in item.Value)
                    combined2 += item2.Value.timer.ElapsedMilliseconds;
            GUILayout.Label((combined2 / 1000f).ToString("0.000"));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked FixedUpdate()");
            GUILayout.Label("|", AltUI.LabelBlue);
            GUILayout.Label((combined2 / (Time.fixedDeltaTime * 1000f)).ToString("p"));
            AltUI.Tooltip.GUITooltip("Total tracked time / reported Unity Time.fixedDeltaTime.  Time.fixedDeltaTime: " +
                Time.fixedDeltaTime.ToString("0.000"));
            GUILayout.EndHorizontal();
            foreach (var item in FixedDeltaTime)
                foreach (var item2 in item.Value)
                {
                    GUILayout.BeginHorizontal(AltUI.TextfieldBlack);
                    GUILayout.Label(item2.Key.Name);
                    GUILayout.Label(item2.Value.target);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((item2.Value.totalPeak / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("Peak in the last 80 calls");
                    GUILayout.Label("|");
                    GUILayout.Label((item2.Value.totalCycle / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("This cycle");
                    GUILayout.EndHorizontal();
                }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Late", AltUI.LabelBlackTitle);
            GUILayout.FlexibleSpace();
            long combined3 = 0;
            foreach (var item in LateDeltaTime)
                foreach (var item2 in item.Value)
                    combined3 += item2.Value.timer.ElapsedMilliseconds;
            GUILayout.Label((combined3 / 1000f).ToString("0.000"));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked LateUpdate()");
            GUILayout.Label("|", AltUI.LabelBlue);
            GUILayout.Label("???");
            AltUI.Tooltip.GUITooltip("Unity does not report lateDeltaTime.");
            GUILayout.EndHorizontal();
            foreach (var item in LateDeltaTime)
                foreach (var item2 in item.Value)
                {
                    GUILayout.BeginHorizontal(AltUI.TextfieldBlack);
                    GUILayout.Label(item2.Key.Name);
                    GUILayout.Label(item2.Value.target);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label((item2.Value.totalPeak / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("Peak in the last 80 calls");
                    GUILayout.Label("|");
                    GUILayout.Label((item2.Value.totalCycle / 1000f).ToString("0.000"));
                    AltUI.Tooltip.GUITooltip("This cycle");
                    GUILayout.EndHorizontal();
                }
            if (GUILayout.Button("UPDATE COLLIDERS"))
                UpdateColliders();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
        public void CloseMenu()
        {
            inst.gameObject.SetActive(false);
        }


        public static void BeginTrackingUpdate0<T>() => BeginTrackingUpdate_Internal<T>(0);
        public static void BeginTrackingUpdate1<T>() => BeginTrackingUpdate_Internal<T>(1);
        public static void BeginTrackingUpdate2<T>() => BeginTrackingUpdate_Internal<T>(2);
        public static void BeginTrackingUpdate3<T>() => BeginTrackingUpdate_Internal<T>(3);
        public static void BeginTrackingUpdate4<T>() => BeginTrackingUpdate_Internal<T>(4);
        public static void BeginTrackingUpdate5<T>() => BeginTrackingUpdate_Internal<T>(5);
        public static void BeginTrackingUpdate6<T>() => BeginTrackingUpdate_Internal<T>(6);
        public static void BeginTrackingUpdate7<T>() => BeginTrackingUpdate_Internal<T>(7);
        public static void BeginTrackingUpdate8<T>() => BeginTrackingUpdate_Internal<T>(8);
        public static void BeginTrackingUpdate9<T>() => BeginTrackingUpdate_Internal<T>(9);
        // DeltaTime
        private static void BeginTrackingUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!DeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                DeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Start();
        }
        public static void EndTrackingUpdate0<T>() => EndTrackingUpdate_Internal<T>(0);
        public static void EndTrackingUpdate1<T>() => EndTrackingUpdate_Internal<T>(1);
        public static void EndTrackingUpdate2<T>() => EndTrackingUpdate_Internal<T>(2);
        public static void EndTrackingUpdate3<T>() => EndTrackingUpdate_Internal<T>(3);
        public static void EndTrackingUpdate4<T>() => EndTrackingUpdate_Internal<T>(4);
        public static void EndTrackingUpdate5<T>() => EndTrackingUpdate_Internal<T>(5);
        public static void EndTrackingUpdate6<T>() => EndTrackingUpdate_Internal<T>(6);
        public static void EndTrackingUpdate7<T>() => EndTrackingUpdate_Internal<T>(7);
        public static void EndTrackingUpdate8<T>() => EndTrackingUpdate_Internal<T>(8);
        public static void EndTrackingUpdate9<T>() => EndTrackingUpdate_Internal<T>(9);
        private static void EndTrackingUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!DeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                DeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Stop();
        }

        // FixedDeltaTime
        public static void BeginTrackingFixedUpdate0<T>() => BeginTrackingFixedUpdate_Internal<T>(0);
        public static void BeginTrackingFixedUpdate1<T>() => BeginTrackingFixedUpdate_Internal<T>(1);
        public static void BeginTrackingFixedUpdate2<T>() => BeginTrackingFixedUpdate_Internal<T>(2);
        public static void BeginTrackingFixedUpdate3<T>() => BeginTrackingFixedUpdate_Internal<T>(3);
        public static void BeginTrackingFixedUpdate4<T>() => BeginTrackingFixedUpdate_Internal<T>(4);
        public static void BeginTrackingFixedUpdate5<T>() => BeginTrackingFixedUpdate_Internal<T>(5);
        private static void BeginTrackingFixedUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!FixedDeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                FixedDeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Start();
        }
        public static void EndTrackingFixedUpdate0<T>() => EndTrackingFixedUpdate_Internal<T>(0);
        public static void EndTrackingFixedUpdate1<T>() => EndTrackingFixedUpdate_Internal<T>(1);
        public static void EndTrackingFixedUpdate2<T>() => EndTrackingFixedUpdate_Internal<T>(2);
        public static void EndTrackingFixedUpdate3<T>() => EndTrackingFixedUpdate_Internal<T>(3);
        public static void EndTrackingFixedUpdate4<T>() => EndTrackingFixedUpdate_Internal<T>(4);
        public static void EndTrackingFixedUpdate5<T>() => EndTrackingFixedUpdate_Internal<T>(5);
        private static void EndTrackingFixedUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!FixedDeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                FixedDeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Stop();
        }


        // LateDeltaTime
        public static void BeginTrackingLateUpdate0<T>() => BeginTrackingLateUpdate_Internal<T>(0);
        public static void BeginTrackingLateUpdate1<T>() => BeginTrackingLateUpdate_Internal<T>(1);
        public static void BeginTrackingLateUpdate2<T>() => BeginTrackingLateUpdate_Internal<T>(2);
        public static void BeginTrackingLateUpdate3<T>() => BeginTrackingLateUpdate_Internal<T>(3);
        public static void BeginTrackingLateUpdate4<T>() => BeginTrackingLateUpdate_Internal<T>(4);
        public static void BeginTrackingLateUpdate5<T>() => BeginTrackingLateUpdate_Internal<T>(5);
        private static void BeginTrackingLateUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!LateDeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                LateDeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Start();
        }
        public static void EndTrackingLateUpdate0<T>() => EndTrackingLateUpdate_Internal<T>(0);
        public static void EndTrackingLateUpdate1<T>() => EndTrackingLateUpdate_Internal<T>(1);
        public static void EndTrackingLateUpdate2<T>() => EndTrackingLateUpdate_Internal<T>(2);
        public static void EndTrackingLateUpdate3<T>() => EndTrackingLateUpdate_Internal<T>(3);
        public static void EndTrackingLateUpdate4<T>() => EndTrackingLateUpdate_Internal<T>(4);
        public static void EndTrackingLateUpdate5<T>() => EndTrackingLateUpdate_Internal<T>(5);
        private static void EndTrackingLateUpdate_Internal<T>(int index)
        {
            Type type = typeof(T);
            DurationTracker watch;
            if (!LateDeltaTime.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                LateDeltaTime.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out watch))
            {
                watch = new DurationTracker
                {
                    target = "Base",
                    timer = new Stopwatch(),
                    totalCycle = 0,
                };
                inline.Add(type, watch);
            }
            watch.timer.Stop();
        }


        /// <summary>
        /// An absolutely terrible idea lol
        /// </summary>
        internal class Extender<T> { }
        public static void OnStaticUpdate()
        {
            for (int i = 0; i < DeltaTime.Count; i++)
            {
                var inline = DeltaTime.ElementAt(i).Value;
                for (int j = 0; j < inline.Count; j++)
                {
                    var item = inline.ElementAt(j);
                    var secs = item.Value.timer.ElapsedMilliseconds;
                    if (secs > 0)
                        item.Value.totalCycle = secs;
                    if (item.Value.totalCycle > item.Value.totalPeak)
                    {
                        item.Value.totalPeak = item.Value.totalCycle;
                        item.Value.lastPeakSteps = 80;
                    }
                    else
                    {
                        if (item.Value.lastPeakSteps > 0)
                        {
                            item.Value.lastPeakSteps = item.Value.lastPeakSteps - 1;
                            if (item.Value.lastPeakSteps <= 0)
                            {
                                item.Value.totalPeak = item.Value.totalCycle;
                                item.Value.lastPeakSteps = 0;
                            }
                        }
                    }
                    item.Value.timer.Reset();
                }
            }
            BeginTrackingUpdate0<ManUpdate>();
        }

        public static void OnStaticFixedUpdate()
        {
            for (int i = 0; i < FixedDeltaTime.Count; i++)
            {
                var inline = FixedDeltaTime.ElementAt(i).Value;
                for (int j = 0; j < inline.Count; j++)
                {
                    var item = inline.ElementAt(j);
                    var secs = item.Value.timer.ElapsedMilliseconds;
                    if (secs > 0)
                        item.Value.totalCycle = secs;
                    if (item.Value.totalCycle > item.Value.totalPeak)
                    {
                        item.Value.totalPeak = item.Value.totalCycle;
                        item.Value.lastPeakSteps = 80;
                    }
                    else
                    {
                        if (item.Value.lastPeakSteps > 0)
                        {
                            item.Value.lastPeakSteps = item.Value.lastPeakSteps - 1;
                            if (item.Value.lastPeakSteps <= 0)
                            {
                                item.Value.totalPeak = item.Value.totalCycle;
                                item.Value.lastPeakSteps = 0;
                            }
                        }
                    }
                    item.Value.timer.Reset();
                }
            }
            BeginTrackingFixedUpdate0<ManUpdate>();
        }

        public static void OnStaticLateUpdate()
        {
            for (int i = 0; i < LateDeltaTime.Count; i++)
            {
                var inline = LateDeltaTime.ElementAt(i).Value;
                for (int j = 0; j < inline.Count; j++)
                {
                    var item = inline.ElementAt(j);
                    var secs = item.Value.timer.ElapsedMilliseconds;
                    if (secs > 0)
                        item.Value.totalCycle = secs;
                    if (item.Value.totalCycle > item.Value.totalPeak)
                    {
                        item.Value.totalPeak = item.Value.totalCycle;
                        item.Value.lastPeakSteps = 80;
                    }
                    else
                    {
                        if (item.Value.lastPeakSteps > 0)
                        {
                            item.Value.lastPeakSteps = item.Value.lastPeakSteps - 1;
                            if (item.Value.lastPeakSteps <= 0)
                            {
                                item.Value.totalPeak = item.Value.totalCycle;
                                item.Value.lastPeakSteps = 0;
                            }
                        }
                    }

                    item.Value.timer.Reset();
                }
            }
            BeginTrackingLateUpdate0<ManUpdate>();
        }

        public static void UpdateColliders()
        {
            foreach (var item in ManTechs.inst.IterateTechsWhere(x => x != null))
            {
                RandomTank.Insure(item).UpdateColliderToggle();
            }
        }
        public static void PrematureOptimization(bool state)
        {
            if (optimize != state)
            {
                if (state)
                {
                    if (ColDefaultIterations_Prev == -1)
                    {
                        ColDefaultIterations_Prev = Physics.defaultSolverIterations;
                        VelDefaultIterations_Prev = Physics.defaultSolverVelocityIterations;
                    }
                    DebugRandAddi.Log("Iterations for Physics altered - [" +
                        ColDefaultIterations_Prev + " -> " + ColDefaultIterations + "], [" +
                        VelDefaultIterations_Prev + " -> " + VelDefaultIterations + "]");
                    Physics.defaultSolverIterations = ColDefaultIterations;
                    Physics.defaultSolverVelocityIterations = VelDefaultIterations;
                }
                else
                {
                    Physics.defaultSolverIterations = ColDefaultIterations_Prev;
                    Physics.defaultSolverVelocityIterations = VelDefaultIterations_Prev;
                }
                optimize = state;
            }
        }
        public static void TrackTypeUpdate<T>(string targetFunc)
        {
            Type ToPatch = typeof(T);
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type adder = ToPatch;
                int depth = 0;
                while (DeltaTime.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                    depth++;
                DeltaTime.AddInlined(depth, adder, new DurationTracker
                {
                    target = targetFunc,
                    timer = new Stopwatch(),
                    totalCycle = 0,
                });
                KickStart.harmonyInstance.Patch(target,
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "BeginTrackingUpdate" + depth.ToString(), null, new Type[] { adder })),
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "EndTrackingUpdate" + depth.ToString(), null, new Type[] { adder })));
                DebugRandAddi.Log(KickStart.ModName + ": Tracking " + ToPatch.Name + " for function - " + targetFunc);
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }
        private class TypeFiller0 { };
        private class TypeFiller1 { };
        private class TypeFiller2 { };
        private class TypeFiller3 { };
        private class TypeFiller4 { };
        private class TypeFiller5 { };
        private static Type[] fillers = new Type[]{
            typeof(TypeFiller0),
            typeof(TypeFiller1),
            typeof(TypeFiller2),
            typeof(TypeFiller3),
            typeof(TypeFiller4),
            typeof(TypeFiller5),
            };
        public static void TrackTypeUpdate(Type ToPatch, string targetFunc)
        {
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type adder = ToPatch;
                int depth = 0;
                if (adder.IsAbstract)
                {
                    for (int step = 0; step < 6; step++)
                    {
                        adder = fillers[step];
                        depth = 0;
                        for (; depth < 6; depth++)
                        {
                            if (!DeltaTime.TryGetValue(depth, out var next) || !next.ContainsKey(ToPatch))
                                goto skipper;
                        }
                    }
                    DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + 
                        " - no more static handles could be created");
                    return;
                    skipper: { }
                }
                else
                {
                    while (DeltaTime.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                        depth++;
                }
                DeltaTime.AddInlined(depth, adder, new DurationTracker
                {
                    target = targetFunc,
                    timer = new Stopwatch(),
                    totalCycle = 0,
                });
                KickStart.harmonyInstance.Patch(target,
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "BeginTrackingUpdate" + depth.ToString(), null, new Type[] { adder })),
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "EndTrackingUpdate" + depth.ToString(), null, new Type[] { adder })));
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }
        public static void TrackTypeFixed<T>(string targetFunc)
        {
            Type ToPatch = typeof(T);
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type adder = ToPatch;
                int depth = 0;
                if (adder.IsAbstract)
                {
                    for (int step = 0; step < 6; step++)
                    {
                        adder = fillers[step];
                        depth = 0;
                        for (; depth < 6; depth++)
                        {
                            if (!FixedDeltaTime.TryGetValue(depth, out var next) || !next.ContainsKey(ToPatch))
                                goto skipper;
                        }
                    }
                    DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc +
                        " - no more static handles could be created");
                    return;
                    skipper: { }
                }
                else
                {
                    while (FixedDeltaTime.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                        depth++;
                }
                FixedDeltaTime.AddInlined(depth, adder, new DurationTracker
                {
                    target = targetFunc,
                    timer = new Stopwatch(),
                    totalCycle = 0,
                });
                KickStart.harmonyInstance.Patch(target,
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "BeginTrackingFixedUpdate" + depth.ToString(), null, new Type[] { adder })),
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "EndTrackingFixedUpdate" + depth.ToString(), null, new Type[] { adder })));
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }
        public static void TrackTypeFixed(Type ToPatch, string targetFunc)
        {
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type adder = ToPatch;
                int depth = 0;
                while (FixedDeltaTime.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                    depth++;
                FixedDeltaTime.AddInlined(depth, adder, new DurationTracker
                {
                    target = targetFunc,
                    timer = new Stopwatch(),
                    totalCycle = 0,
                });
                KickStart.harmonyInstance.Patch(target,
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "BeginTrackingFixedUpdate" + depth.ToString(), null, new Type[] { adder })),
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "EndTrackingFixedUpdate" + depth.ToString(), null, new Type[] { adder })));
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }
        public static void TrackTypeLate<T>(string targetFunc)
        {
            Type ToPatch = typeof(T);
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type adder = ToPatch;
                int depth = 0;
                if (adder.IsAbstract)
                {
                    for (int step = 0; step < 6; step++)
                    {
                        adder = fillers[step];
                        depth = 0;
                        for (; depth < 6; depth++)
                        {
                            if (!LateDeltaTime.TryGetValue(depth, out var next) || !next.ContainsKey(ToPatch))
                                goto skipper;
                        }
                    }
                    DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc +
                        " - no more static handles could be created");
                    return;
                skipper: { }
                }
                else
                {
                    while (LateDeltaTime.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                        depth++;
                }
                LateDeltaTime.AddInlined(depth, adder, new DurationTracker
                {
                    target = targetFunc,
                    timer = new Stopwatch(),
                    totalCycle = 0,
                });
                KickStart.harmonyInstance.Patch(target,
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "BeginTrackingLateUpdate" + depth.ToString(), null, new Type[] { adder })),
                    new HarmonyMethod(AccessTools.Method(typeof(Optimax), "EndTrackingLateUpdate" + depth.ToString(), null, new Type[] { adder })));
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }
    }
}
