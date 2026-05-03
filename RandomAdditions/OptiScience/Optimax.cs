using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// some pointless attempts to optimize the game!
    /// </summary>
    internal class Optimax : MonoBehaviour
    {
        internal class DurationTracker
        {
            public Type targetClass;
            public string targetMethod;
            public string DisplayName;
            public Stopwatch timer;
            public HashSet<DurationTracker> timersInlayed = new HashSet<DurationTracker>();
            public long totalCycle;
            public long totalLifetime;
            public long totalPeak;
            public long lastPeakSteps;
            public byte checkPhase;

            public override string ToString() => DisplayName;

            public long CalcActualTime(long rawCycle)
            {
                totalCycle = rawCycle;
                foreach (var item in timersInlayed)
                    totalCycle -= item.totalCycle;
                if (totalCycle < 0)
                    totalCycle = 0;
                return totalCycle;
            }
        }
        internal const int ColDefaultIterations = 1;
        internal const int VelDefaultIterations = 1;
        internal const int ColTankIterations = 2;
        internal const int VelTankIterations = 1;


        internal static Optimax inst;
        internal static bool profile = false;
        internal static bool optimize = false;

        private static int ColDefaultIterations_Prev = -1;
        private static int VelDefaultIterations_Prev = 0;
        private static Dictionary<int, Dictionary<Type, DurationTracker>> DeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> FixedDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> LateDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> CircuitsDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();
        private static Dictionary<int, Dictionary<Type, DurationTracker>> OtherDeltaTime =
            new Dictionary<int, Dictionary<Type, DurationTracker>>();

        private class PatchedParam
        {
            public MethodBase target;
            public MethodInfo thePatch;
        }

        private static List<PatchedParam> AllPatched = null;

        private static void Init()
        {
            if (profile)
                return;
            DebugRandAddi.Log(KickStart.ModName + ": Init " + nameof(Optimax));
            profile = true;

            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.First, OnStaticUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.First, EndTrackUpdate, -90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, BeginTrackUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.Update, ManUpdate.Order.Last, EndTrackUpdateLATE, -90010);

            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, OnStaticFixedUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, EndTrackFixedUpdate, -90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, BeginTrackFixedUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, EndTrackFixedUpdatePHY, -90010);

            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.First, OnStaticLateUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.First, EndTrackLateUpdate, -90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, BeginTrackLateUpdate, 90010);
            ManUpdate.inst.AddAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, EndTrackLateUpdateREND, -90010);


            if (inst != null)
                return;
            inst = new GameObject("Optimaxxer").AddComponent<Optimax>();

            AllPatched = new List<PatchedParam>();

            // ------------------------------------------------------------

            StartTrackTypeCircuits<Circuits>("DoCircuitLoop");
            StartTrackTypeCircuits<Circuits.Network>("ReceiveCharge");
            StartTrackTypeCircuits<Circuits.Network>("PropagateFromHere");
            StartTrackTypeCircuits<Circuits.Network>("ReleaseCharge");
            StartTrackTypeCircuits<ModuleCircuitNode>("UpdateConnexionLinks");
            StartTrackTypeCircuits<ModuleCircuitDispensor>("ResetCachedInfo");
            StartTrackTypeCircuits<ModuleCircuitDispensor>("DispenseCharge");
            StartTrackTypeCircuits<ModuleCircuitDispensor>("OnCircuitVisualUpdate");
            StartTrackTypeCircuits<ModuleCircuitReceiver>("OnPostSlowUpdate");
            StartTrackTypeCircuits<ModuleCircuitReceiver>("OnStartChargeUpdate");
            StartTrackTypeCircuits<ModuleCircuitReceiver>("OnEndChargeUpdate");
            StartTrackTypeCircuits<ModuleCircuitReceiver>("AddChargeToCache");
            StartTrackTypeCircuits<TechCircuits>("OnStartChargeUpdate");
            StartTrackTypeCircuits<TechCircuits>("RebuildCircuitNetworksForDirtyConnexions");
            StartTrackTypeCircuits<TechCircuits>("OnReleaseChargeUpdate");

            // ------------------------------------------------------------

            BeginTrackingOther0<Physics>();
            EndTrackingOther0<Physics>();

            // ------------------------------------------------------------

            StartTrackTypeUpdate<uScript_Update>("Update");

            StartTrackTypeUpdate<ManVisible.SearchIterator>("InitSearch");
            StartTrackTypeUpdate<ManVisible.SearchIterator>("MoveNext");
            StartTrackTypeUpdate<ManMods>("Update");
            StartTrackTypeUpdate<ManCombat>("Update");
            StartTrackTypeUpdate<ManEncounterPlacement>("Update");
            StartTrackTypeUpdate<ManFreeSpace>("Update");
            StartTrackTypeUpdate<ManGravity>("Update");
            StartTrackTypeUpdate<ManHUD>("Update");
            StartTrackTypeUpdate<ManUI>("Update");
            StartTrackTypeUpdate<ManMap>("Update");
            StartTrackTypeUpdate<ManWheels>("Update");
            StartTrackTypeUpdate<ManPointer>("UpdateTargetsFromNearby");
            StartTrackTypeUpdate<ManPlayer>("Update");
            StartTrackTypeUpdate<ManPop>("Update");
            StartTrackTypeUpdate<ManPurchases>("Update");
            StartTrackTypeUpdate<ManSceneryAnimation>("Update");
            StartTrackTypeUpdate<ManSpawn>("Update");
            StartTrackTypeUpdate<ManTechMaterialSwap>("Update");
            StartTrackTypeUpdate<ManTechSwapper>("Update");
            StartTrackTypeUpdate<ManTimeOfDay>("Update");
            StartTrackTypeUpdate<ManWeather>("Update");
            StartTrackTypeUpdate<ManWorldTreadmill>("Update");
            StartTrackTypeUpdate<ManPath>("Update");
            StartTrackTypeUpdate<uScript_RandDScriptEvent>("Update");
            //TrackTypeUpdate<ManTileLoader>("Update");// Doesn't exists
            StartTrackTypeUpdate<TileManager>("UpdateTileCache");

            BeginTrackingUpdate0<ManUpdate>();
            StartTrackTypeUpdate<BulletCasing>("StaticUpdateCasings");
            StartTrackTypeUpdate<ManMap>("UpdateExploredArea_Schedule");
            StartTrackTypeUpdate<ModuleWheels>("UpdateWheelParticles");

            StartTrackTypeUpdate<Tank>("Update");
            StartTrackTypeUpdate<TechWeapon>("OnUpdate");
            StartTrackTypeUpdate<TechVision>("GetFirstVisibleTechIsEnemy");
            StartTrackTypeUpdate<TechEnergy>("Update");

            StartTrackTypeUpdate<MB_Update>("Update");
            StartTrackTypeUpdate<SwitchableUpdater>("Update");
            StartTrackTypeUpdate<AudioProvider>("Update");

            StartTrackTypeUpdate<FanJet>("OnUpdate");
            StartTrackTypeUpdate<BoosterJet>("OnUpdate");
            StartTrackTypeUpdate<HoverJet>("OnUpdate");

            StartTrackTypeUpdate<UIMiniMapDisplay>("Update");

            // ------------------------------------------------------------

            //TrackTypeFixed<Physics>("SyncTransforms");
            StartTrackTypeFixed<Physics>("Simulate");
            StartTrackTypeFixed<uScript_Update>("FixedUpdate");

            StartTrackTypeFixed<ManMods>("FixedUpdate");
            StartTrackTypeFixed<ManStatusEffects>("FixedUpdate");
            StartTrackTypeFixed<ManTimedEvents>("FixedUpdate");
            StartTrackTypeFixed<ManGravity>("FixedUpdate");

            BeginTrackingFixedUpdate0<ManUpdate>();
            EndTrackingFixedUpdate0<ManUpdate>();
            StartTrackTypeFixed<ManDamage>("ProcessPendingDamage");
            StartTrackTypeFixed<ManTechs>("OnFirstFixedUpdate");
            StartTrackTypeFixed<ManWheels>("OnFirstFixedUpdate");
            StartTrackTypeFixed<ManWheels>("OnLastFixedUpdate");
            StartTrackTypeFixed<ModuleWheels>("UpdateTireTracks");

            StartTrackTypeFixed<Tank>("FixedUpdate");
            /*
            TrackTypeFixed(typeof(Tank), "OnCollisionEnter");
            TrackTypeFixed(typeof(Tank), "OnCollisionStay");
            TrackTypeFixed(typeof(Tank), "OnTriggerEnter");
            TrackTypeFixed(typeof(Tank), "OnTriggerExit");
            */
            StartTrackTypeFixed<TankCamera>("FixedUpdate");
            StartTrackTypeFixed<TankBeam>("OnFixedUpdate");
            StartTrackTypeFixed<TechBooster>("OnFixedUpdate");

            StartTrackTypeFixed<MB_FixedUpdate>("FixedUpdate");
            StartTrackTypeUpdate<SwitchableUpdater>("FixedUpdate");
            StartTrackTypeFixed<ModuleGyro>("OnFixedUpdate");

            StartTrackTypeFixed<FanJet>("OnFixedUpdate");
            StartTrackTypeFixed<BoosterJet>("OnFixedUpdate");
            StartTrackTypeFixed<HoverJet>("OnFixedUpdate");
            //TrackTypeFixed(typeof(Visible), "OnFixedUpdate");
            StartTrackTypeFixed<Explosion>("FixedUpdate");

            StartTrackTypeFixed<VisiblePhysicsWakerVolume>("FixedUpdate");

            // ------------------------------------------------------------

            StartTrackTypeLate<uScript_Update>("LateUpdate");

            StartTrackTypeLate<ManWorldTreadmill>("LateUpdate");

            BeginTrackingLateUpdate0<ManUpdate>();
            EndTrackingLateUpdate0<ManUpdate>();
            StartTrackTypeLate<ManMap>("UpdateExploredArea_Apply");

            StartTrackTypeLate<MB_LateUpdate>("LateUpdate");

            StartTrackTypeLate<FollowTransform>("LateUpdate");
            StartTrackTypeLate<FollowSuspension>("LateUpdate");
            StartTrackTypeLate<RotationAligner>("LateUpdate");
            //TrackTypeLate<TriggerCatcher>("DestroyQueuedUpListeners");

            // ------------------------------------------------------------
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
            //TrackTypeUpdate(typeof(CustomModules.GameObjectExtensions), "RecursiveFindWithProperties");
        }


        private static void DoThePatch(MethodBase target, MethodInfo MI1, MethodInfo MI2)
        {
            AllPatched.Add(new PatchedParam()
            {
                target = target,
                thePatch = KickStart.harmonyInstance.Patch(target, new HarmonyMethod(MI1), new HarmonyMethod(MI2)),
            });
        }
        private static void DeInit()
        {
            if (!profile)
                return;
            DebugRandAddi.Log(KickStart.ModName + ": DeInit " + nameof(Optimax));
            profile = false;

            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.First, OnStaticUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.First, EndTrackUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.Last, BeginTrackUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.Update, ManUpdate.Order.Last, EndTrackUpdateLATE);

            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, OnStaticFixedUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.First, EndTrackFixedUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, BeginTrackFixedUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.FixedUpdate, ManUpdate.Order.Last, EndTrackFixedUpdatePHY);

            ManUpdate.inst.RemoveAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.First, OnStaticLateUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.First, EndTrackLateUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, BeginTrackLateUpdate);
            ManUpdate.inst.RemoveAction(ManUpdate.Type.LateUpdate, ManUpdate.Order.Last, EndTrackLateUpdateREND);

            return; // Stupid unity crashes here
            if (inst == null)
                return;
            foreach (var item in AllPatched)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Unpatching " + item.target + " both Prefix and Postfix");
                KickStart.harmonyInstance.Unpatch(item.target, item.thePatch);
            }
            AllPatched.Clear();
            AllPatched = null;

            Destroy(inst.gameObject);
            inst = null;
        }
        public static bool State { get; private set; }
        public static void SetActive(bool state)
        {
            if (State != state)
            {
                State = state;
                if (state)
                    Init();
                else
                    DeInit();
                if (inst)
                    inst.gameObject.SetActive(state);
            }
        }


        private static bool openU = true;
        private static bool openF = true;
        private static bool openL = true;
        private static bool openC = true;
        private static bool openO = true;


        private static int cycleCounter = 20;
        private static long combinedU = 0;
        private static long combinedF = 0;
        private static long combinedL = 0;
        private static long combinedC = 0;
        private static long combinedO = 0;

        private const int peakCallDuration = 80;
        private const string stringFormat = "00.000";
        private const string stringFormatLong = "0000.000";
        private const string stringFormatPercent = "P";
        private const float rescaleWindow = 0.6f;
        private static Vector2 scroll = default;   // the "window"
        private static UIScaler scaler = new UIScaler(rescaleWindow);
        private static Rect HotWindow = new Rect(0, 0, 400 * scaler.UIScaleInv, 500 * scaler.UIScaleInv);   // the "window"

        private static string[] nbumber = new string[10]
            {
                "0",
                "1",
                "2",
                "3",
                "4",
                "5",
                "6",
                "7",
                "8",
                "9",
            };
        private static void NumericalDisplay(float number)
        {
            GUILayout.Label(number.ToString(stringFormatLong));
            /*
            GUILayout.BeginHorizontal();
            NumericalDisplay_Internal4(number);
            GUILayout.EndHorizontal();//*/
        }
        private static void NumericalDisplay(int number)
        {
            GUILayout.Label(number.ToString());
            /*
            GUILayout.BeginHorizontal();
            NumericalDisplay_Internal(number);
            GUILayout.EndHorizontal();//*/
        }
        private static void NumericalDisplayPercent(int number)
        {
            GUILayout.Label(number.ToString("P"));
            /*
            GUILayout.BeginHorizontal();
            NumericalDisplay_Internal(number);
            GUILayout.EndHorizontal();//*/
        }
        private static void NumericalDisplayPercent(float number)
        {
            GUILayout.Label(number.ToString(stringFormatPercent));
            /*
            GUILayout.BeginHorizontal();
            NumericalDisplay_Internal3(number);
            GUILayout.Label("%");
            GUILayout.EndHorizontal();//*/
        }
        private static void NumericalDisplay_Internal3(float number)
        {
            int numInt = Mathf.RoundToInt(number * 1000);
            NumericalDisplay_Internal((numInt / 1000) * 1000, 4);
            GUILayout.Label(".");
            NumericalDisplay_Internal(numInt % 1000, 3);
        }
        private static void NumericalDisplay_Internal4(float number)
        {
            int numInt = Mathf.RoundToInt(number * 1000);
            NumericalDisplay_Internal((numInt / 1000) * 1000, 4);
            GUILayout.Label(".");
            NumericalDisplay_Internal(numInt % 1000, 3);
        }
        private static void NumericalDisplay_Internal(int number, int digitsMin)
        {
            int digitsCur = 0;
            while (number > 0)
            {
                digitsCur++;
                number /= 10;
            }
            digitsMin -= digitsCur;
            for (; 0 < digitsMin; digitsMin--)
                NumericalDisplaySeg_Internal(0);
            NumericalDisplay_Internal(number);
        }
        private static void NumericalDisplay_Internal(int number)
        {
            while (number > 0)
            {
                NumericalDisplaySeg_Internal(number % 10);
                number /= 10;
            }
        }
        private static void NumericalDisplaySeg_Internal(int number)
        {
            GUILayout.Label(nbumber[number]);
        }

        public void OnGUI()
        {
            EndTrackingOther0<Renderer>();
            BeginTrackOptimax();
            HotWindow = scaler.Window(13213124, HotWindow, GUIHandler,
                "Processing", 0.75f, CloseMenu, true, true);
            EndTrackOptimax();
        }
        private void DisplayListGUI(Dictionary<int, Dictionary<Type, DurationTracker>> man)
        {
            foreach (var item in man)
                foreach (var item2 in item.Value)
                {
                    DurationTracker DT = item2.Value;
                    if (DT.totalLifetime > 0)
                    {
                        GUILayout.BeginHorizontal(AltUI.TextfieldBlack);
                        GUILayout.Label(DT.DisplayName, GUILayout.Width(40));
                        GUILayout.FlexibleSpace();
                        NumericalDisplay(DT.totalLifetime / 1000f);
                        AltUI.Tooltip.GUITooltip("Lifetime");
                        GUILayout.Label("<b> | </b>");
                        NumericalDisplay(DT.totalPeak / 1000f);
                        AltUI.Tooltip.GUITooltip("Peak in the last " + peakCallDuration + " calls");
                        GUILayout.Label("<b> | </b>");
                        NumericalDisplay(DT.totalCycle / 1000f);
                        AltUI.Tooltip.GUITooltip("This cycle");
                        GUILayout.EndHorizontal();
                    }
                }
        }
        public void GUIHandler(int ID)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            if (AltUI.Button("C&S", ManSFX.UISfxType.CheckBox, OnStaticCircuitsError != null ? AltUI.LabelRedTitle : AltUI.LabelBlackTitle))
                openC = !openC;
            if (OnStaticCircuitsError != null)
                AltUI.Tooltip.GUITooltip(OnStaticCircuitsError.ToString());
            float expectedFrameDurationMS = Time.maximumDeltaTime * 1000;
            GUILayout.FlexibleSpace();
            //GUILayout.Label(Time.deltaTime.ToString(stringFormat));
            NumericalDisplay(combinedC / 1000f);
            //GUILayout.Label((combinedC / 1000f).ToString(stringFormat));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked Circuits functions every 40 cycles");
            GUILayout.Label("<b> | </b>", AltUI.LabelBlue);
            NumericalDisplayPercent(combinedC / expectedFrameDurationMS);
            //GUILayout.Label((combinedC / expectedFrameDurationMS).ToString(stringFormatPercent));
            AltUI.Tooltip.GUITooltip("Total tracked time / Expected frame duration.  Time.maximumDeltaTime: " +
                Time.maximumDeltaTime.ToString(stringFormat));
            GUILayout.EndHorizontal();
            if (openC)
            {
                GUILayout.BeginHorizontal(AltUI.TextfieldBlack);
                GUILayout.Label("C&S Stats");
                GUILayout.FlexibleSpace();
                NumericalDisplay(Circuits.StartChargeUpdate.GetSubscriberCount());
                //GUILayout.Label(Circuits.StartChargeUpdate.GetSubscriberCount().ToString());
                AltUI.Tooltip.GUITooltip("StartChargeUpdate");
                GUILayout.Label("<b> | </b>");
                NumericalDisplay(Circuits.GenerateChargeUpdate.GetSubscriberCount());
                //GUILayout.Label(Circuits.GenerateChargeUpdate.GetSubscriberCount().ToString());
                AltUI.Tooltip.GUITooltip("GenerateChargeUpdate");
                GUILayout.Label("<b> | </b>");
                NumericalDisplay(Circuits.EndChargeUpdate.GetSubscriberCount());
                //GUILayout.Label(Circuits.EndChargeUpdate.GetSubscriberCount().ToString());
                AltUI.Tooltip.GUITooltip("EndChargeUpdate");
                GUILayout.Label("<b> | </b>");
                NumericalDisplay(Circuits.PostSlowUpdate.GetSubscriberCount());
                //GUILayout.Label(Circuits.PostSlowUpdate.GetSubscriberCount().ToString());
                AltUI.Tooltip.GUITooltip("PostSlowUpdate");
                GUILayout.EndHorizontal();

                DisplayListGUI(CircuitsDeltaTime);
            }

            GUILayout.BeginHorizontal();
            if (AltUI.Button("Other", ManSFX.UISfxType.CheckBox, OnStaticOtherError != null ? AltUI.LabelRedTitle : AltUI.LabelBlackTitle))
                openO = !openO;
            if (OnStaticOtherError != null)
                AltUI.Tooltip.GUITooltip(OnStaticOtherError.ToString());
            GUILayout.FlexibleSpace();
            NumericalDisplay(combinedO / 1000f);
            //GUILayout.Label((combinedO / 1000f).ToString(stringFormat));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked other functions every 40 cycles");
            GUILayout.Label("<b> | </b>", AltUI.LabelBlue);
            NumericalDisplayPercent(combinedO / expectedFrameDurationMS);
            //GUILayout.Label((combinedO / expectedFrameDurationMS).ToString(stringFormatPercent));
            AltUI.Tooltip.GUITooltip("Total tracked time / Expected frame duration.  Time.maximumDeltaTime: " +
                Time.maximumDeltaTime.ToString(stringFormat));
            GUILayout.EndHorizontal();
            if (openO)
                DisplayListGUI(OtherDeltaTime);

            GUILayout.BeginHorizontal();
            if (AltUI.Button("Update", ManSFX.UISfxType.CheckBox, OnStaticUpdateError != null ? AltUI.LabelRedTitle : AltUI.LabelBlackTitle))
                openU = !openU;
            if (OnStaticUpdateError != null)
                AltUI.Tooltip.GUITooltip(OnStaticUpdateError.ToString());
            GUILayout.FlexibleSpace();
            NumericalDisplay(combinedU / 1000f);
            //GUILayout.Label((combinedU / 1000f).ToString(stringFormat));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked Update() every 40 cycles");
            GUILayout.Label("<b> | </b>", AltUI.LabelBlue);
            NumericalDisplayPercent(combinedU / expectedFrameDurationMS);
            //GUILayout.Label((combinedU / expectedFrameDurationMS).ToString(stringFormatPercent));
            AltUI.Tooltip.GUITooltip("Total tracked time / Expected frame duration.  Time.maximumDeltaTime: " +
                Time.maximumDeltaTime.ToString(stringFormat));
            GUILayout.EndHorizontal();
            if (openU)
                DisplayListGUI(DeltaTime);

            GUILayout.BeginHorizontal();
            if (AltUI.Button("Fixed", ManSFX.UISfxType.CheckBox, OnStaticFixedUpdateError != null ? AltUI.LabelRedTitle : AltUI.LabelBlackTitle))
                openF = !openF;
            if (OnStaticFixedUpdateError != null)
                AltUI.Tooltip.GUITooltip(OnStaticFixedUpdateError.ToString());
            GUILayout.FlexibleSpace();
            //GUILayout.Label(Time.fixedDeltaTime.ToString("0.000"));
            NumericalDisplay(combinedF / 1000f);
            //GUILayout.Label((combinedF / 1000f).ToString(stringFormat));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked FixedUpdate()");
            GUILayout.Label("<b> | </b>", AltUI.LabelBlue);
            NumericalDisplayPercent(combinedF / expectedFrameDurationMS);
            //GUILayout.Label((combinedF / expectedFrameDurationMS).ToString(stringFormatPercent));
            AltUI.Tooltip.GUITooltip("Total tracked time / Expected frame duration.  Time.maximumDeltaTime: " +
                Time.maximumDeltaTime.ToString(stringFormat));
            GUILayout.EndHorizontal();
            if (openF)
                DisplayListGUI(FixedDeltaTime);

            GUILayout.BeginHorizontal();
            if (AltUI.Button("Late", ManSFX.UISfxType.CheckBox, OnStaticLateUpdateError != null ? AltUI.LabelRedTitle : AltUI.LabelBlackTitle))
                openL = !openL;
            if (OnStaticLateUpdateError != null)
                AltUI.Tooltip.GUITooltip(OnStaticLateUpdateError.ToString());
            GUILayout.FlexibleSpace();
            NumericalDisplay(combinedL / 1000f);
            //GUILayout.Label((combinedL / 1000f).ToString(stringFormat));
            AltUI.Tooltip.GUITooltip("Total time in seconds for all tracked LateUpdate()");
            GUILayout.Label("<b> | </b>", AltUI.LabelBlue);
            NumericalDisplayPercent(combinedL / expectedFrameDurationMS);
            //GUILayout.Label((combinedL / expectedFrameDurationMS).ToString(stringFormatPercent));
            AltUI.Tooltip.GUITooltip("Total tracked time / Expected frame duration.  Time.maximumDeltaTime: " +
                Time.maximumDeltaTime.ToString(stringFormat));
            GUILayout.EndHorizontal();
            if (openL)
                DisplayListGUI(LateDeltaTime);

            if (GUILayout.Button("RESET INLAYED"))
                ResetInlayedTracking();
            if (GUILayout.Button("RESET LIFETIMES"))
                ResetLifetimeTracking();
            /*
            if (GUILayout.Button("UPDATE COLLIDERS"))
                UpdateColliders();//*/
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
        public void CloseMenu()
        {
            inst.gameObject.SetActive(false);
            State = false;
        }

        private void ResetInlayedTracking()
        {
            foreach (var item in DeltaTime)
                foreach (var item2 in item.Value)
                {
                    item2.Value.checkPhase = 0;
                    item2.Value.timersInlayed.Clear();
                }
            foreach (var item in FixedDeltaTime)
                foreach (var item2 in item.Value)
                {
                    item2.Value.checkPhase = 0;
                    item2.Value.timersInlayed.Clear();
                }
            foreach (var item in LateDeltaTime)
                foreach (var item2 in item.Value)
                {
                    item2.Value.checkPhase = 0;
                    item2.Value.timersInlayed.Clear();
                }
            foreach (var item in CircuitsDeltaTime)
                foreach (var item2 in item.Value)
                {
                    item2.Value.checkPhase = 0;
                    item2.Value.timersInlayed.Clear();
                }
            foreach (var item in OtherDeltaTime)
                foreach (var item2 in item.Value)
                {
                    item2.Value.checkPhase = 0;
                    item2.Value.timersInlayed.Clear();
                }
        }
        private void ResetLifetimeTracking()
        {
            foreach (var item in DeltaTime)
                foreach (var item2 in item.Value)
                    item2.Value.totalLifetime = 0;
            foreach (var item in FixedDeltaTime)
                foreach (var item2 in item.Value)
                    item2.Value.totalLifetime = 0;
            foreach (var item in LateDeltaTime)
                foreach (var item2 in item.Value)
                    item2.Value.totalLifetime = 0;
            foreach (var item in CircuitsDeltaTime)
                foreach (var item2 in item.Value)
                    item2.Value.totalLifetime = 0;
            foreach (var item in OtherDeltaTime)
                foreach (var item2 in item.Value)
                    item2.Value.totalLifetime = 0;
        }


        private static Stack<DurationTracker> Running = new Stack<DurationTracker>();
        private static DurationTracker InsureExistTimer(Dictionary<int, Dictionary<Type, DurationTracker>> man, Type type, int index)
        {
            DurationTracker DT = null;
            if (!man.TryGetValue(index, out var inline))
            {
                inline = new Dictionary<Type, DurationTracker>();
                man.Add(index, inline);
            }
            if (!inline.TryGetValue(type, out DT))
            {
                DT = new DurationTracker
                {
                    targetMethod = "Base",
                    DisplayName = type.Name + ".Base",
                    timer = new Stopwatch(),
                };
                inline.Add(type, DT);
            }
            return DT;
        }
        private static void StartPushTimer(DurationTracker sanity)
        {
            Running.Push(sanity);
            sanity.timer.Start();
            if (Running.Count > 125)
            {
                Running.Clear();
                throw new InvalidOperationException("The " + nameof(Optimax) + " runstack reached 125.  This should not be happening");
            }
        }
        private static void SanityCheckStopPopCalcTimer(DurationTracker sanity)
        {
            if (Running.Peek() == sanity)
            {
                sanity.timer.Stop();
                var cur = Running.Pop();
                if (sanity.checkPhase < 200)
                {
                    /*
                        if (cur.DisplayName != sanity.DisplayName)
                            throw new InvalidOperationException("Expected " + sanity.ToString() + ", got " + cur.ToString());
                    //*/
                    if (Running.Any())
                        foreach (var entry in Running)
                            sanity.timersInlayed.Add(entry);
                    sanity.checkPhase++;
                }
            }
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
        public static void BeginTrackingUpdate10<T>() => BeginTrackingUpdate_Internal<T>(10);
        public static void BeginTrackingUpdate11<T>() => BeginTrackingUpdate_Internal<T>(11);
        public static void BeginTrackingUpdate12<T>() => BeginTrackingUpdate_Internal<T>(12);
        public static void BeginTrackingUpdate13<T>() => BeginTrackingUpdate_Internal<T>(13);
        // DeltaTime
        private static void BeginTrackingUpdate_Internal<T>(int index)
        {
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(DeltaTime, typeof(T), index));
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
        public static void EndTrackingUpdate10<T>() => EndTrackingUpdate_Internal<T>(10);
        public static void EndTrackingUpdate11<T>() => EndTrackingUpdate_Internal<T>(11);
        public static void EndTrackingUpdate12<T>() => EndTrackingUpdate_Internal<T>(12);
        public static void EndTrackingUpdate13<T>() => EndTrackingUpdate_Internal<T>(13);
        private static void EndTrackingUpdate_Internal<T>(int index)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(DeltaTime, typeof(T), index));
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
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(FixedDeltaTime, typeof(T), index));
        }
        public static void EndTrackingFixedUpdate0<T>() => EndTrackingFixedUpdate_Internal<T>(0);
        public static void EndTrackingFixedUpdate1<T>() => EndTrackingFixedUpdate_Internal<T>(1);
        public static void EndTrackingFixedUpdate2<T>() => EndTrackingFixedUpdate_Internal<T>(2);
        public static void EndTrackingFixedUpdate3<T>() => EndTrackingFixedUpdate_Internal<T>(3);
        public static void EndTrackingFixedUpdate4<T>() => EndTrackingFixedUpdate_Internal<T>(4);
        public static void EndTrackingFixedUpdate5<T>() => EndTrackingFixedUpdate_Internal<T>(5);
        private static void EndTrackingFixedUpdate_Internal<T>(int index)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(FixedDeltaTime, typeof(T), index));
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
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(LateDeltaTime, typeof(T), index));
        }
        public static void EndTrackingLateUpdate0<T>() => EndTrackingLateUpdate_Internal<T>(0);
        public static void EndTrackingLateUpdate1<T>() => EndTrackingLateUpdate_Internal<T>(1);
        public static void EndTrackingLateUpdate2<T>() => EndTrackingLateUpdate_Internal<T>(2);
        public static void EndTrackingLateUpdate3<T>() => EndTrackingLateUpdate_Internal<T>(3);
        public static void EndTrackingLateUpdate4<T>() => EndTrackingLateUpdate_Internal<T>(4);
        public static void EndTrackingLateUpdate5<T>() => EndTrackingLateUpdate_Internal<T>(5);
        private static void EndTrackingLateUpdate_Internal<T>(int index)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(LateDeltaTime, typeof(T), index));
        }

        // Circuits
        public static void BeginTrackingCircuits0<T>() => BeginTrackingCircuits_Internal<T>(0);
        public static void BeginTrackingCircuits1<T>() => BeginTrackingCircuits_Internal<T>(1);
        public static void BeginTrackingCircuits2<T>() => BeginTrackingCircuits_Internal<T>(2);
        public static void BeginTrackingCircuits3<T>() => BeginTrackingCircuits_Internal<T>(3);
        public static void BeginTrackingCircuits4<T>() => BeginTrackingCircuits_Internal<T>(4);
        public static void BeginTrackingCircuits5<T>() => BeginTrackingCircuits_Internal<T>(5);
        private static void BeginTrackingCircuits_Internal<T>(int index)
        {
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(CircuitsDeltaTime, typeof(T), index));
        }
        public static void EndTrackingCircuits0<T>() => EndTrackingCircuits_Internal<T>(0);
        public static void EndTrackingCircuits1<T>() => EndTrackingCircuits_Internal<T>(1);
        public static void EndTrackingCircuits2<T>() => EndTrackingCircuits_Internal<T>(2);
        public static void EndTrackingCircuits3<T>() => EndTrackingCircuits_Internal<T>(3);
        public static void EndTrackingCircuits4<T>() => EndTrackingCircuits_Internal<T>(4);
        public static void EndTrackingCircuits5<T>() => EndTrackingCircuits_Internal<T>(5);
        private static void EndTrackingCircuits_Internal<T>(int index)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(CircuitsDeltaTime, typeof(T), index));
        }

        // Others
        public static void BeginTrackingOther0<T>() => BeginTrackingOther_Internal<T>(0);
        public static void BeginTrackingOther1<T>() => BeginTrackingOther_Internal<T>(1);
        public static void BeginTrackingOther2<T>() => BeginTrackingOther_Internal<T>(2);
        public static void BeginTrackingOther3<T>() => BeginTrackingOther_Internal<T>(3);
        public static void BeginTrackingOther4<T>() => BeginTrackingOther_Internal<T>(4);
        public static void BeginTrackingOther5<T>() => BeginTrackingOther_Internal<T>(5);
        private static void BeginTrackingOther_Internal<T>(int index)
        {
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(OtherDeltaTime, typeof(T), index));
        }
        private static void BeginTrackingOther(Type type, int index = 0)
        {
            if (!profile)
                return;
            StartPushTimer(InsureExistTimer(OtherDeltaTime, type, index));
        }
        public static void EndTrackingOther0<T>() => EndTrackingOther_Internal<T>(0);
        public static void EndTrackingOther1<T>() => EndTrackingOther_Internal<T>(1);
        public static void EndTrackingOther2<T>() => EndTrackingOther_Internal<T>(2);
        public static void EndTrackingOther3<T>() => EndTrackingOther_Internal<T>(3);
        public static void EndTrackingOther4<T>() => EndTrackingOther_Internal<T>(4);
        public static void EndTrackingOther5<T>() => EndTrackingOther_Internal<T>(5);
        private static void EndTrackingOther_Internal<T>(int index)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(OtherDeltaTime, typeof(T), index));
        }
        private static void EndTrackingOther(Type type, int index = 0)
        {
            if (!profile)
                return;
            SanityCheckStopPopCalcTimer(InsureExistTimer(OtherDeltaTime, type, index));
        }


        /// <summary>
        /// An absolutely terrible idea lol
        /// </summary>
        internal class Extender<T> { }
        private static void UpdateCalcValues(Dictionary<int, Dictionary<Type, DurationTracker>> man, ref Exception errorIfNeeded)
        {
            for (int i = 0; i < man.Count; i++)
            {
                var inline = man.ElementAt(i).Value;
                for (int j = 0; j < inline.Count; j++)
                {
                    var item = inline.ElementAt(j);
                    var secs = item.Value.CalcActualTime(item.Value.timer.ElapsedMilliseconds);
                    if (secs > 0)
                    {
                        item.Value.totalLifetime += secs;
                        if (item.Value.timer.IsRunning && errorIfNeeded == null)
                            errorIfNeeded = new Exception(item.Value.targetMethod);
                        item.Value.timer.Reset();
                    }
                    if (secs > item.Value.totalPeak)
                    {
                        item.Value.totalPeak = secs;
                        item.Value.lastPeakSteps = peakCallDuration;
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
                }
            }
        }
        private static void GetCombinedDurations()
        {
            BeginTrackOptimax();
            if (cycleCounter >= 40)
            {
                cycleCounter = 0;
                combinedU = 0;
                foreach (var item in DeltaTime)
                    foreach (var item2 in item.Value)
                        combinedU += item2.Value.totalCycle;
                combinedF = 0;
                foreach (var item in FixedDeltaTime)
                    foreach (var item2 in item.Value)
                        combinedF += item2.Value.totalCycle;
                combinedL = 0;
                foreach (var item in LateDeltaTime)
                    foreach (var item2 in item.Value)
                        combinedL += item2.Value.totalCycle;
                combinedC = 0;
                foreach (var item in CircuitsDeltaTime)
                    foreach (var item2 in item.Value)
                        combinedC += item2.Value.totalCycle;
                combinedO = 0;
                foreach (var item in OtherDeltaTime)
                    foreach (var item2 in item.Value)
                        combinedO += item2.Value.totalCycle;
            }
            cycleCounter++;
            EndTrackOptimax();
        }

        private static Exception OnStaticUpdateError = null;
        private static void OnStaticUpdate()
        {
            EndTrackingOther0<Physics>();
            OnStaticOtherUpdate();
            UpdateCalcValues(DeltaTime, ref OnStaticUpdateError);
            OnStaticCircuitsUpdate();
            GetCombinedDurations();
            BeginTrackingUpdate0<ManUpdate>();
        }
        private static void BeginTrackUpdate() => BeginTrackingUpdate0<ManUpdate>();
        private static void EndTrackUpdate() => EndTrackingUpdate0<ManUpdate>();
        private static void EndTrackUpdateLATE()
        {
            EndTrackingUpdate0<ManUpdate>();
            BeginTrackingOther0<Animation>();
        }


        private static Exception OnStaticFixedUpdateError = null;
        private static void OnStaticFixedUpdate()
        {
            EndTrackingOther0<Physics>();
            UpdateCalcValues(FixedDeltaTime, ref OnStaticFixedUpdateError);
            BeginTrackingFixedUpdate0<ManUpdate>();
        }
        private static void BeginTrackFixedUpdate() => BeginTrackingFixedUpdate0<ManUpdate>();
        private static void EndTrackFixedUpdate() => EndTrackingFixedUpdate0<ManUpdate>();
        private static void EndTrackFixedUpdatePHY()
        {
            EndTrackingFixedUpdate0<ManUpdate>();
            BeginTrackingOther0<Physics>();
        }


        private static Exception OnStaticLateUpdateError = null;
        private static void OnStaticLateUpdate()
        {
            EndTrackingOther0<Physics>();
            EndTrackingOther0<Animation>();
            UpdateCalcValues(LateDeltaTime, ref OnStaticLateUpdateError);
            BeginTrackingLateUpdate0<ManUpdate>();
        }
        private static void BeginTrackLateUpdate() => BeginTrackingLateUpdate0<ManUpdate>();
        private static void EndTrackLateUpdate() => EndTrackingLateUpdate0<ManUpdate>();
        private static void EndTrackLateUpdateREND()
        {
            EndTrackingLateUpdate0<ManUpdate>();
            BeginTrackingOther0<Renderer>();
        }


        private static Exception OnStaticCircuitsError = null;
        private static void OnStaticCircuitsUpdate()
        {
            UpdateCalcValues(CircuitsDeltaTime, ref OnStaticCircuitsError);
        }


        private static Exception OnStaticOtherError = null;
        private static void OnStaticOtherUpdate()
        {
            UpdateCalcValues(OtherDeltaTime, ref OnStaticOtherError);
        }
        private static void BeginTrackOptimax() => BeginTrackingOther(typeof(Optimax));
        private static void EndTrackOptimax() => EndTrackingOther(typeof(Optimax));



        public static void UpdateColliders()
        {
            //Physics.autoSimulation = !KickStart.ColliderDisable2 || ManNetwork.IsNetworked;
            //Physics.IgnoreLayerCollision(Globals.inst.layerTank, Globals.inst.layerTank, !(!KickStart.ColliderDisable2 || ManNetwork.IsNetworked));
            //*
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item != null)
                    RandomTank.Insure(item).UpdateColliderToggle();
            }//*/
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

        private static void StartTrackGeneral(Dictionary<int, Dictionary<Type, DurationTracker>> man, 
            string manName, Type ToPatch, string targetFunc)
        {
            try
            {
                MethodInfo target = AccessTools.Method(ToPatch, targetFunc);
                Type lookup = ToPatch;
                int depth = 0;
                if (ToPatch.IsAbstract)
                {
                    for (int step = 0; step < 6; step++)
                    {
                        lookup = fillers[step];
                        depth = 0;
                        for (; depth < 6; depth++)
                        {
                            if (!man.TryGetValue(depth, out var next) || !next.ContainsKey(ToPatch))
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
                    while (man.TryGetValue(depth, out var next) && next.ContainsKey(ToPatch))
                        depth++;
                }
                string dispName = ToPatch.Name + "." + targetFunc;
                man.AddInlined(depth, lookup, new DurationTracker
                {
                    targetClass = ToPatch,
                    targetMethod = targetFunc,
                    DisplayName = dispName.Length > 24 ? dispName.Substring(0, 24) : dispName,
                    timer = new Stopwatch(),
                });
                DoThePatch(target,
                    AccessTools.Method(typeof(Optimax), "BeginTracking" + manName + depth.ToString(), null, new Type[] { lookup }),
                    AccessTools.Method(typeof(Optimax), "EndTracking" + manName + depth.ToString(), null, new Type[] { lookup }));
                DebugRandAddi.Log(KickStart.ModName + ": (" + manName + ")Tracking " + ToPatch.Name + " for function - " + targetFunc);
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(KickStart.ModName + ": Failed to handle patch of " + ToPatch.Name + " for function - " + targetFunc + " - " + e);
            }
        }

        public static void StartTrackTypeUpdate<T>(string targetFunc)
        {
            StartTrackGeneral(DeltaTime, "Update", typeof(T), targetFunc);
        }
        public static void StartTrackTypeUpdate(Type ToPatch, string targetFunc)
        {
            StartTrackGeneral(DeltaTime, "Update", ToPatch, targetFunc);
        }
        public static void StartTrackTypeFixed<T>(string targetFunc)
        {
            StartTrackGeneral(FixedDeltaTime, "FixedUpdate", typeof(T), targetFunc);
        }
        public static void StartTrackTypeFixed(Type ToPatch, string targetFunc)
        {
            StartTrackGeneral(FixedDeltaTime, "FixedUpdate", ToPatch, targetFunc);
        }
        public static void StartTrackTypeLate<T>(string targetFunc)
        {
            StartTrackGeneral(LateDeltaTime, "LateUpdate", typeof(T), targetFunc);
        }
        public static void StartTrackTypeCircuits<T>(string targetFunc)
        {
            StartTrackGeneral(CircuitsDeltaTime, "Circuits", typeof(T), targetFunc);
        }
        public static void StartTrackTypeOther<T>(string targetFunc)
        {
            StartTrackGeneral(OtherDeltaTime, "Other", typeof(T), targetFunc);
        }
    }
}
