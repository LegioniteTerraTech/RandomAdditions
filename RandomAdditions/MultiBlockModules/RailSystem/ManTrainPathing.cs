using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions.RailSystem
{
    public enum TrainArrivalStatus
    {
        Arrived,
        Cancelled,
        NoPath,
        TrackSabotaged,
        Derailed,
        Destroyed,
        TrainBlockingPath,
        PlayerHyjacked,
    }
    public class ManTrainPathing
    {
        public const float StationaryTrainObstructionPenalty = 360000;
        public const float MovingTrainObstructionPenalty = 2500;
        internal static int QueueStepRepeatTimes = 3;
        private static int FinderQueueStepRepeatTimes => QueueStepRepeatTimes;
        private static int PatherQueueStepRepeatTimes => QueueStepRepeatTimes;
        private static int NodePatherQueueStepRepeatTimes => QueueStepRepeatTimes;

        private static Color PathDebugColor = new Color(0.9f, 0.9f, 0.2f, 1);
        private static float PathDebugUpdateTime = 1;

        private static bool shouldLog = false;
        private static Dictionary<int, int> PathsToResume = null;

        // Train Pathing Saver & Resumer
        internal static KeyValuePair<int,int>[] GetAllPathfindingRequestsToSave()
        {
            List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>();
            foreach (var item in CallQueue)
            {
                if (item.train != null)
                    list.Add(new KeyValuePair<int, int>(item.train.tank.visible.ID, item.target.NodeID));
            }
            foreach (var item in patherQueue)
            {
                if (item.Key.train != null)
                    list.Add(new KeyValuePair<int, int>(item.Key.train.tank.visible.ID, item.Key.target.NodeID));
            }
            if (list.Count == 0)
                return null;
            return list.ToArray();
        }
        public static void ResumePathingFromSave(KeyValuePair<int, int>[] pathsSaved)
        {
            PathsToResume = new Dictionary<int, int>();
            foreach (var item in pathsSaved)
            {
                if (!PathsToResume.ContainsKey(item.Key))
                {
                    PathsToResume.Add(item.Key, item.Value);
                }
            }
        }
        public static void TryFetchTrainsToResumePathing()
        {
            if (PathsToResume == null)
                return;
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (PathsToResume.TryGetValue(item.visible.ID, out int targetNode))
                {
                    var comp = item.GetComponent<TankLocomotive>();
                    if (comp && ManRails.AllRailNodes.TryGetValue(targetNode, out RailTrackNode node))
                    {
                        TrainPathfindRailNetwork(comp, node, null);
                        DebugRandAddi.Log("ManTrainPathing - Resumed pathing of " + item.name + " to node ID " + targetNode);
                    }
                    else
                    {
                        PathsToResume.Remove(item.visible.ID);
                        DebugRandAddi.Log("ManTrainPathing - Cancelled pathing of " + item.name + " - node " + targetNode + " no longer exists");
                    }
                }
            }
            DebugRandAddi.Log("ManTrainPathing: TryFetchTrainsToResumePathing - Waiting on " + PathsToResume.Count + " trains to load...");
        }


        private static GameObject AllyTextStor;
        private static FloatingTextOverlayData AllyTextData;
        public static void TrainStatusPopup(string desc, WorldPosition pos)
        {
            if (AllyTextStor == null)
                AllyTextStor = AltUI.CreateCustomPopupInfo("TrainCall", new Color(1f, 0.65f, 0f), out AllyTextData);
            AltUI.PopupCustomInfo(desc, pos, AllyTextData);
        }



        // Train Finder Async
        private static List<TrainCallRequest> CallQueue = new List<TrainCallRequest>();
        private static int finderQueueStep = 0;
        private static int finderQueueStepRepeater = 0;
        public static void QueueFindNearestTrainInRailNetworkAsync(RailTrackNode RTN, Action<TankLocomotive> Callback)
        {
            if (AllyTextStor == null)
                AltUI.CreateCustomPopupInfo("TrainCall", new Color(0.85f, 0.65f, 0.65f), out AllyTextData);
            CallQueue.Add(new TrainCallRequest(RTN, Callback));
        }
        public static void CancelFindNearestTrainInRailNetworkAsync(RailTrackNode RTN)
        {
            if (AllyTextStor == null)
                AltUI.CreateCustomPopupInfo("TrainCall", new Color(0.85f, 0.65f, 0.65f), out AllyTextData);
            CallQueue.Find(delegate (TrainCallRequest cand) {
                return cand != null && cand.target == RTN;
            });
        }

        internal static void AsyncManageStationTrainSearch()
        {
            while (CallQueue.Count() > finderQueueStep)
            {
                var item = CallQueue[finderQueueStep];
                if (!item.AsyncManageTrainCall())
                {
                    CallQueue.RemoveAt(finderQueueStep);
                    finderQueueStepRepeater = 0;
                }
                else if (finderQueueStepRepeater > FinderQueueStepRepeatTimes)
                    finderQueueStepRepeater++;
                else
                {
                    finderQueueStep++;
                    finderQueueStepRepeater = 0;
                }
            }
            finderQueueStep = 0;
        }



        // Train Pather Async
        private static List<KeyValuePair<TrainPathRequest, Action<bool>>> patherQueue = new List<KeyValuePair<TrainPathRequest, Action<bool>>>();
        private static int patherQueueStep = 0;
        private static int patherQueueStepRepeater = 0;
        public static void TrainPathfindRailNetwork(TankLocomotive train, RailTrackNode destination,
            Action<bool> calledOnDestination)
        {
            patherQueue.Add(new KeyValuePair<TrainPathRequest, Action<bool>>(new TrainPathRequest(train, destination), calledOnDestination));
        }
        internal static void AsyncManageTrainPathing()
        {
            while (patherQueue.Count() > patherQueueStep)
            {
                var key = patherQueue[patherQueueStep].Key;
                if (key.PathStep())
                {
                    if (key.CalcResults())
                    {
                        DebugRandAddi.Log("AsyncManageTrainPathing - pathing for " + key.train.tank.name + " succeeded.");
                        key.SubmitResults();
                        if (patherQueue[patherQueueStep].Value != null)
                            patherQueue[patherQueueStep].Value.Invoke(true);
                    }
                    else
                    {
                        DebugRandAddi.Log("AsyncManageTrainPathing - pathing for " + key.train.tank.name + " failed.");
                        if (patherQueue[patherQueueStep].Value != null)
                            patherQueue[patherQueueStep].Value.Invoke(false);
                    }
                    patherQueue.RemoveAt(patherQueueStep);
                    patherQueueStepRepeater = 0;
                }
                else if (patherQueueStepRepeater < PatherQueueStepRepeatTimes)
                    patherQueueStepRepeater++;
                else
                {
                    patherQueueStep++;
                    patherQueueStepRepeater = 0;
                }
            }
            patherQueueStep = 0;
        }


        // Node To Node Pather Async
        private static Dictionary<int, KeyValuePair<RailPathRequest, Action<Dictionary<RailConnectInfo, int>>>> nodePatherQueue = 
            new Dictionary<int, KeyValuePair<RailPathRequest, Action<Dictionary<RailConnectInfo, int>>>>();
        private static int nodePatherQueueStep = 0;
        private static int nodePatherQueueStepRepeater = 0;
        public static void NodePathfindRailNetwork(int ID, RailTrackNode Start, RailTrackNode destination,
            Action<Dictionary<RailConnectInfo, int>> calledOnDestination)
        {
            nodePatherQueue.Add(ID, new KeyValuePair<RailPathRequest, Action<Dictionary<RailConnectInfo, int>>>(
                new RailPathRequest(Start, destination), calledOnDestination));
        }
        public static void CancelPathfindRailNetwork(int ID)
        {
            nodePatherQueue.Remove(ID);
        }
        internal static void AsyncManageNodeRailPathing()
        {
            while (nodePatherQueue.Count() > nodePatherQueueStep)
            {
                var stepD = nodePatherQueue.ElementAt(nodePatherQueueStep);
                var key = stepD.Value.Key;
                if (key.PathStep())
                {
                    if (key.CalcResults())
                    {
                        DebugRandAddi.Log("AsyncManageNodeRailPathing - pathing succeeded.");
                        if (nodePatherQueue[nodePatherQueueStep].Value != null)
                            nodePatherQueue[nodePatherQueueStep].Value.Invoke(key.SubmitResults());
                    }
                    else
                        DebugRandAddi.Log("AsyncManageNodeRailPathing - pathing failed.");
                    nodePatherQueue.Remove(stepD.Key);
                    nodePatherQueueStepRepeater = 0;
                }
                else if (nodePatherQueueStepRepeater < NodePatherQueueStepRepeatTimes)
                    nodePatherQueueStepRepeater++;
                else
                {
                    nodePatherQueueStep++;
                    nodePatherQueueStepRepeater = 0;
                }
            }
            nodePatherQueueStep = 0;
        }


        private static void Log(string message)
        {
            if (!shouldLog)
                return;
            Debug.Log(message);
        }


        internal class GUIManaged
        {
            internal enum TrainPathingType
            {
                None,
                CallTrain,
                PathTracks,
                PathNodes,
            }

            private static bool display = false;
            private static bool displayBWD = false;

            private static TrainPathingType displayedPather = TrainPathingType.None;
            private static TrainCallRequest dispCallPatherCache = null;
            private static TrainPathRequest dispTrackPatherCache = default;
            private static RailPathRequest dispNodePatherCache = default;
            public static void GUIShowWorld()
            {
                try
                {
                    switch (displayedPather)
                    {
                        case TrainPathingType.None:
                            break;
                        case TrainPathingType.CallTrain:
                            dispCallPatherCache.Debug_SHOW(displayBWD);
                            break;
                        case TrainPathingType.PathTracks:
                            dispTrackPatherCache.Debug_SHOW(displayBWD);
                            break;
                        case TrainPathingType.PathNodes:
                            dispNodePatherCache.Debug_SHOW(displayBWD);
                            break;
                        default:
                            break;
                    }
                }
                catch (ExitGUIException e) { throw e; }
                catch { }
            }
            public static void GUIGetTotalManaged()
            {
                if (!ManRails.IsInit)
                {
                    GUILayout.Box("--- Train Pathing [DISABLED] --- ");
                    return;
                }
                GUILayout.Box("--- Train Pathing --- ");
                display = AltUI.Toggle(display, "Show: ");
                if (display)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(" CallTrain Count: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(CallQueue.Count.ToString());
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(" PathTracks Count: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(patherQueue.Count.ToString());
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(" PathNodes Count: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(nodePatherQueue.Count.ToString());
                    GUILayout.EndHorizontal();

                    displayBWD = AltUI.Toggle(displayBWD, "Backwards: ");
                    displayedPather = (TrainPathingType)GUILayout.Toolbar(
                        (int)displayedPather, Enum.GetNames(typeof(TrainPathingType)));
                    switch (displayedPather)
                    {
                        case TrainPathingType.None:
                            break;
                        case TrainPathingType.CallTrain:
                            if (dispCallPatherCache == null)
                            {
                                for (int step = 0; step < CallQueue.Count; step++)
                                {
                                    var item = CallQueue.ElementAt(step);
                                    if (GUILayout.Button(item.target.Point ?
                                        item.target.Point.tank.name : item.target.NodeID.ToString()))
                                    {
                                        dispCallPatherCache = item;
                                    }
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("Back"))
                                    dispCallPatherCache = null;
                                else
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label("  Target Node ID: ");
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Label(dispCallPatherCache.target.NodeID.ToString());
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label("  Target Node Coordinates: ");
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Label(dispCallPatherCache.target.GetLinkCenter(0).GameWorldPosition.ToString());
                                    GUILayout.EndHorizontal();

                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label("  Target Node Present? ");
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Label(dispCallPatherCache.target.Point ? "True" : "False");
                                    GUILayout.EndHorizontal();

                                    var train = dispCallPatherCache.train;
                                    if (dispCallPatherCache.train)
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Found? ");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label("True");
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Coordinates:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label(WorldPosition.FromScenePosition(train.tank.boundsCentreWorldNoCheck).GameWorldPosition.ToString());
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Speed:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label(train.lastForwardSpeed.ToString("F"));
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Velocity:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label(train.lastCenterSpeed.ToString("F"));
                                        GUILayout.EndHorizontal();
                                    }
                                    else
                                    {
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Found? ");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label("False");
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Coordinates:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label("NULL");
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Speed:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label("NULL");
                                        GUILayout.EndHorizontal();

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("  Train Velocity:");
                                        GUILayout.FlexibleSpace();
                                        GUILayout.Label("NULL");
                                        GUILayout.EndHorizontal();
                                    }
                                }
                            }
                            break;
                        case TrainPathingType.PathTracks:
                            for (int step = 0; step < patherQueue.Count; step++)
                            {
                                var item = patherQueue.ElementAt(step);
                                var ele = patherQueue.ElementAt(step).Key;
                                if (GUILayout.Button(item.Key.ToString()))
                                {
                                    dispTrackPatherCache = ele;
                                }
                            }
                            break;
                        case TrainPathingType.PathNodes:
                            for (int step = 0; step < nodePatherQueue.Count; step++)
                            {
                                var item = nodePatherQueue.ElementAt(step);
                                var ele = nodePatherQueue.ElementAt(step).Value.Key;
                                if (GUILayout.Button(item.Key.ToString()))
                                {
                                    dispNodePatherCache = ele;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    displayedPather = TrainPathingType.None;
                }
            }
        }


        public class TrainCallRequest
        {
            private readonly float startTime;
            private readonly int requestTeam;
            public readonly RailTrackNode target;
            private readonly ManRails.RailTrackIterator trainSearcher;
            private readonly Action<TankLocomotive> finishedEvent;
            private readonly HashSet<TankLocomotive> FinishedSearches;
            private List<KeyValuePair<TankLocomotive, TrainPathRequest>> FinishedRequests;

            public TankLocomotive train; 
            private TrainPathRequest trainPather;
            private int attempts;

            public TrainCallRequest(RailTrackNode Target, Action<TankLocomotive> OnFinished)
            {
                startTime = Time.time;
                try
                {
                    requestTeam = target.Team;
                }
                catch 
                {
                    requestTeam = ManPlayer.inst.PlayerTeam;
                }
                target = Target;
                train = null;
                trainPather = default;
                finishedEvent = OnFinished;
                FinishedSearches = new HashSet<TankLocomotive>();
                FinishedRequests = new List<KeyValuePair<TankLocomotive, TrainPathRequest>>();
                trainSearcher = new ManRails.RailTrackIterator(target, x => IsValidTrainOnTrack(x));
                attempts = 0;
            }

            public bool IsValidTrainOnTrack(RailTrack x)
            {
                return x != null && x.Exists() && x.ActiveBogeys.Count() > 0 &&
                x.ActiveBogeys.FirstOrDefault(delegate (ModuleRailBogie.RailBogie cand) {
                    if (cand == null || !cand.engine || cand.engine.tank.Team != requestTeam)
                        return false;
                    TankLocomotive master = cand.engine.GetMaster();
                    return master.CanCall() && !FinishedSearches.Contains(master);
                }) != null;
            }
            public bool AsyncManageTrainCall()
            {
                if (train == null)
                {
                    return AsyncFindTrain();
                }
                else
                {
                    return AsyncTrainTryPathfindBack();
                }
            }
            private bool AsyncFindTrain()
            {
                if (trainSearcher.Current != null && !trainSearcher.Current.Removing)
                {
                    foreach (var item in trainSearcher.Current.ActiveBogeys)
                    {
                        TankLocomotive master = item.engine.GetMaster();
                        if (!master.AutopilotActive && !FinishedSearches.Contains(master))
                        {
                            this.train = master;
                            if (!target.Registered() || !train)
                            {
                                DebugRandAddi.Log("\nTarget RailTrackNode does not exist after " + (Time.time - startTime) + " seconds");
                                if (finishedEvent != null)
                                    finishedEvent.Invoke(null);
                                return false; // Can't call to an unloaded station!
                            }

                            if (train)
                            {
                                train.RegisterAllLinkedLocomotives();
                                train = train.GetMaster();
                                DebugRandAddi.Log("\nTrain \"" + train.name + "\" with " + train.TrainLength + " total cars was found after " + (Time.time - startTime) + " seconds");
                                trainPather = new TrainPathRequest(train, target);
                                return true;
                            }
                            else
                                DebugRandAddi.Log("\nTrain does not exist after " + (Time.time - startTime) + " seconds");
                        }
                        else
                            DebugRandAddi.Log("trainSearcher fail on " + attempts + ":  Name - " + master.name + " | Auto - " + master.AutopilotActive + " | Already Queued - " + FinishedSearches.Contains(master) + " | Queued Count - " + FinishedSearches.Count);
                    }
                    DebugRandAddi.Log("trainSearcher found no valid bogie on attempt " + attempts);
                }
                else
                {
                    DebugRandAddi.Log("trainSearcher current is null " + (trainSearcher.Current == null) + " on attempt " + attempts);
                }
                if (!trainSearcher.nodeTrack)
                    attempts++;
                if (ManRails.ManagedTracks.Count * 2 < attempts)
                {
                    if (finishedEvent != null)
                        finishedEvent.Invoke(null);
                    DebugRandAddi.Assert("Attempts bypassed number of managed tracks x2. " + attempts + " vs " + ManRails.ManagedTracks.Count);
                    return false;
                }
                if (trainSearcher.MoveNextStep())
                {
                    return true;
                }
                DebugRandAddi.Log("trainSearcher gave up after " + attempts + " attempts.\n");
                //DebugRandAddi.Assert("Station could not find train in " + trainSearcher.TotalTracksIteratedCount() + " tracks.  There are " + ManRails.ManagedTracks.Count + " total managed tracks.");

                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                return ConcludeIfNeeded();
            }

            private bool AsyncTrainTryPathfindBack()
            {
                if (trainPather.PathStep())
                {
                    if (trainPather.CalcResults())
                    {   // Could pathfind to the station
                        if (target.Registered() && train)
                        {
                            if (!trainPather.CompletelyObstructed)
                            {
                                trainPather.SubmitResults();
                                DebugRandAddi.Log("\nTrain is called to station at " + (Time.time - startTime) + " seconds\n");
                                if (finishedEvent != null)
                                    finishedEvent.Invoke(train.GetMaster());
                                return false;
                            }
                            DebugRandAddi.Log("\nTrain " + train.name + " found obstructed path at " + (Time.time - startTime) + " seconds\n");
                            FinishedSearches.Add(train);
                            FinishedRequests.Add(new KeyValuePair<TankLocomotive, TrainPathRequest>(train, trainPather));
                            train = null;   // Can't pathfind back fully, partial obstruction.
                            return ConcludeIfNeeded();
                        }
                    }
                    DebugRandAddi.Log("\nTrain " + train.name + " can't find path at " + (Time.time - startTime) + " seconds\n");
                    FinishedSearches.Add(train);
                    train = null;   // Can't pathfind back fully
                    return ConcludeIfNeeded();
                }
                return true;
            }
            private bool ConcludeIfNeeded()
            {
                if (trainSearcher.MoveNextStep())
                    return true;
                if (FinishedRequests.Count == 0)
                {
                    DebugRandAddi.Log("\nOut of possible options at " + (Time.time - startTime) + " seconds");
                    if (finishedEvent != null)
                        finishedEvent.Invoke(null);
                    return false;
                }
                // We have some results to look through
                FinishedRequests = FinishedRequests.OrderBy(x => x.Value.GetResults()).ToList();

                FinishedRequests.FirstOrDefault().Value.SubmitResults();
                DebugRandAddi.Log("\n(All possible routes busy or obstructed) Train is called to station at " + (Time.time - startTime) + " seconds");
                if (finishedEvent != null)
                    finishedEvent.Invoke(FinishedRequests.FirstOrDefault().Key.GetMaster());
                return false;
            }

            internal void Debug_SHOW(bool backwards)
            {
                if (train == null)
                {
                    trainSearcher.Debug_SHOW(PathDebugUpdateTime);
                }
                else
                {
                    trainPather.Debug_SHOW(backwards);
                }
            }
        }

        private static StringBuilder SB = new StringBuilder();
        public struct RailPathRequest
        {
            private static bool AllowReverseSearch = true;
            public readonly RailTrackNode start;
            public readonly RailTrackNode target;
            private readonly RailPathingTree requestFWD;
            private readonly RailPathingTree requestBKD;
            private RailPathingTree.RailPathBranch finishedRequestFWD;
            private RailPathingTree.RailPathBranch finishedRequestBKD;
            public float distFWD;
            public float distBKD;
            private bool FWDObst;
            private bool BKDObst;
            public bool CompletelyObstructed;

            public RailPathRequest(RailTrackNode start, RailTrackNode target)
            {
                this.start = start;
                this.target = target;
                var startTrack = start.GetAllConnectedLinks().FirstOrDefault().LinkTrack;
                requestFWD = new RailPathingTree(startTrack, true, target);
                requestBKD = new RailPathingTree(startTrack, false, target);
                finishedRequestFWD = null;
                finishedRequestBKD = null;
                distFWD = float.MaxValue;
                distBKD = float.MaxValue;
                FWDObst = false;
                BKDObst = false;
                CompletelyObstructed = false;
            }


            public bool PathStep()
            {
                if (!PathStepFWD())
                    return false;
                else if (AllowReverseSearch && !PathStepBKD())
                    return false;
                return true;
            }

            private bool PathStepFWD()
            { 
                switch (requestFWD.UpdatePathing(out RailPathingTree.RailPathBranch branch))
                {
                    case -1:
                        Log("RailPathRequest FAILED to find path for a bogie!");
                        return true;
                    case 1:
                        Log("RailPathRequest found a path for a bogie");
                        finishedRequestFWD = branch;
                        if (branch.Obstructed)
                            FWDObst = true;
                        return true;
                    default:
                        return false;
                }
            }
            private bool PathStepBKD()
            {
                switch (requestBKD.UpdatePathing(out RailPathingTree.RailPathBranch branch))
                {
                    case -1:
                        Log("RailPathRequest(R) FAILED to find path for a bogie!");
                        return true;
                    case 1:
                        Log("RailPathRequest(R) found a path for a bogie");
                        finishedRequestBKD = branch;
                        if (branch.Obstructed)
                            BKDObst = true;
                        return true;
                    default:
                        return false;
                }
            }


            public bool CalcResults()
            {
                distFWD = TryCalcDistFWD();
                distBKD = TryCalcDistBKD();
                if (distFWD == float.MaxValue && distBKD == float.MaxValue)
                    return false;
                if (FWDObst && BKDObst)
                    CompletelyObstructed = true;
                return true;
            }
            public float GetResults()
            {
                if (distFWD < distBKD)
                    return distFWD;
                return distBKD;
            }
            public Dictionary<RailConnectInfo, int> SubmitResults()
            {
                if (distFWD == float.MaxValue && distBKD == float.MaxValue)
                    throw new Exception("RailPathRequest called SubmitResults() with invalid distances!");
                if (distFWD < distBKD)
                {
                    var item = finishedRequestFWD;
                    Dictionary<RailConnectInfo, int> thePlan = new Dictionary<RailConnectInfo, int>();
                    foreach (var item2 in item.instructions)
                    {
                        thePlan.Add(item2.Key, item2.Value);
                        SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + "] => ");
                    }
                    SB.Append("[" + target.NodeID + "]");
                    DebugRandAddi.Log("RailPathRequest FWD plans: " + SB.ToString());
                    return thePlan;
                }
                else
                {
                    var item = finishedRequestBKD;
                    Dictionary<RailConnectInfo, int> thePlan = new Dictionary<RailConnectInfo, int>();
                    try
                    {
                        foreach (var item2 in item.instructions)
                        {
                            thePlan.Add(item2.Key, item2.Value);
                            SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + "] => ");
                        }
                        SB.Append("[" + target.NodeID + "]");
                        DebugRandAddi.Log("RailPathRequest BKD plans: " + SB.ToString());
                    }
                    finally
                    {
                        SB.Clear();
                    }
                    return thePlan;
                }
            }
            private float TryCalcDistFWD()
            {
                if (finishedRequestFWD == null)
                {
                    return float.MaxValue;
                }
                float accumlativeDist = 0;
                var item = finishedRequestFWD;

                try
                {

                    accumlativeDist += item.dist;
                    foreach (var item2 in item.instructions)
                    {
                        if (item2.Key != null)
                        {
                            if (!item2.Key.HostNode.Registered())
                            {
                                DebugRandAddi.Assert("RailPathRequest cancelled because track nodes changed");
                                return float.MaxValue;
                            }
                            SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + " | " + item.dist + "] => ");
                        }
                        else
                        {
                            DebugRandAddi.Assert("RailPathRequest cancelled because track changed");
                            return float.MaxValue;
                        }
                    }
                    SB.Append("[" + target.NodeID + " | " + accumlativeDist + "]");
                    Log("RailPathRequest FWD plans: " + SB.ToString());
                }
                finally
                {
                    SB.Clear();
                }

                return accumlativeDist;
            }
            private float TryCalcDistBKD()
            {
                if (finishedRequestBKD == null)
                {
                    return float.MaxValue;
                }
                float accumlativeDist = 0;
                var item = finishedRequestFWD;

                try
                {
                    accumlativeDist += item.dist;
                    foreach (var item2 in item.instructions)
                    {
                        if (item2.Key != null)
                        {
                            if (!item2.Key.HostNode.Registered())
                            {
                                DebugRandAddi.Assert("RailPathRequest cancelled because track nodes changed");
                                return float.MaxValue;
                            }
                            SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + " | " + item.dist + "] => ");
                        }
                        else
                        {
                            DebugRandAddi.Assert("RailPathRequest cancelled because track changed");
                            return float.MaxValue;
                        }
                    }
                    SB.Append("[" + target.NodeID + " | " + accumlativeDist + "]");
                    Log("RailPathRequest BKD plans: " + SB.ToString());
                }
                finally
                {
                    SB.Clear();
                }
                return accumlativeDist;
            }

            internal void Debug_SHOW(bool backwards)
            {
                int Index = 0;
                if (backwards)
                {
                    requestBKD.Debug_SHOW(true, ref Index, new Vector3(0, 1, 0));
                }
                else
                {
                    requestFWD.Debug_SHOW(true, ref Index, new Vector3(0, 1, 0));
                }
            }



            private struct RailPathingTree
            {
                private readonly RailTrackNode dest;
                private readonly List<RailPathBranch> curOps;
                private List<RailPathBranch> finished;

                public RailPathingTree(RailTrack startTrack, bool forwards, RailTrackNode destination)
                {
                    dest = destination;
                    curOps = new List<RailPathBranch>();
                    if (forwards)
                        curOps.Add(new RailPathBranch(startTrack, 0, null, null, true, "_F"));
                    else
                        curOps.Add(new RailPathBranch(startTrack, 0, null, null, false, "_R"));
                    finished = new List<RailPathBranch>();
                }

                public int UpdatePathing(out RailPathBranch branch)
                {
                    branch = null;
                    int path = UpdatePathing_Internal();
                    if (path == 0)
                        return 0;
                    if (finished.Count == 0)
                        return -1;
                    branch = finished.OrderBy(x => x.dist).FirstOrDefault();
                    return 1;
                }
                private int UpdatePathing_Internal()
                {
                    int steps = 0;
                    while (curOps.Count() > steps)
                    {
                        RailPathBranch val = curOps[steps];
                        switch (val.Pathfind(this))
                        {
                            case -1:
                                curOps.RemoveAt(steps);
                                break;
                            case 1:
                                finished.Add(val);
                                curOps.RemoveAt(steps);
                                break;
                            default:
                                steps++;
                                break;
                        }
                    }
                    return curOps.Count() == 0 ? 1 : 0;
                }

                internal void Debug_SHOW(bool backwards, ref int index, Vector3 upNum)
                {
                    if (backwards)
                    {
                        for (int step = 0; step < curOps.Count; step++)
                        {
                            curOps[step].Debug_SHOW(Color.red, ref index, upNum);
                        }
                    }
                    else
                    {
                        for (int step = 0; step < curOps.Count; step++)
                        {
                            curOps[step].Debug_SHOW(Color.green, ref index, upNum);
                        }
                    }
                }

                internal class RailPathBranch
                {
                    private bool Forward;
                    internal RailTrack curTrack;
                    internal HashSet<RailConnectInfo> passed;
                    internal List<KeyValuePair<RailConnectInfo, int>> instructions;
                    internal float dist;
                    internal bool Obstructed;
                    private int curSeg;
                    private string id;

                    public RailPathBranch(RailTrack start, float distance, HashSet<RailConnectInfo> passedNodes, List<KeyValuePair<RailConnectInfo, int>> prevNodes, bool fwd, string ID)
                    {
                        Forward = fwd;
                        curTrack = start;
                        curSeg = 0;
                        dist = distance;
                        Obstructed = false;
                        if (passedNodes == null)
                            passed = new HashSet<RailConnectInfo>();
                        else
                            passed = new HashSet<RailConnectInfo>(passedNodes);
                        if (prevNodes == null)
                            instructions = new List<KeyValuePair<RailConnectInfo, int>>();
                        else
                            instructions = new List<KeyValuePair<RailConnectInfo, int>>(prevNodes);
                        id = ID;
                    }

                    public int Pathfind(RailPathingTree RPT)
                    {
                        if (curTrack == null)
                        {
                            Log("BogiePath " + id + " next rail null");
                            return -1;
                        }
                        RailTrack prev = curTrack;
                        curTrack = curTrack.PathfindRailStepObeyOneWay(Forward, out bool inv, out RailConnectInfo info, ref curSeg);
                        if (info != null)
                        {
                            RailTrackNode node = info.HostNode;
                            if (node != null)
                            {
                                dist += curTrack.distance;
                                if (node == RPT.dest)
                                {
                                    Log("BogiePath " + id + " reached destination");
                                    return 1;
                                }
                                if (prev == null || !prev.IsNodeTrack)
                                {   // ignore NodeTracks, only LinkTracks count
                                    if (passed.Contains(info))
                                    {
                                        Log("BogiePath " + id + " looped on itself");
                                        return -1; // LOOP 
                                    }

                                    if (node.NodeType == RailNodeType.Junction && node.GetLinkTrackIndex(prev) == 0)
                                    {
                                        Log("BogiePath " + id + " split at junction node " + node.NodeID);
                                        foreach (var item in node.GetAllConnectedLinks())
                                        {
                                            if (item.Index != 0)
                                            {
                                                if (item.LinkTrack != prev)
                                                {
                                                    RailPathBranch path = new RailPathBranch(item.LinkTrack, dist, passed,
                                                        instructions, item.LowTrackConnection, id + "_" + node.NodeID + "_" + item);
                                                    path.passed.Add(info);
                                                    path.instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, item.Index));
                                                    RPT.curOps.Add(path);
                                                    Log("BogiePath " + id + " split added " + item);
                                                }
                                            }
                                        }
                                        passed.Add(info);
                                        instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, 0));
                                    }
                                    else
                                    {
                                        Log("BogiePath " + id + " passed a point node " + node.NodeID);
                                        passed.Add(info);
                                        instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, 0));
                                    }
                                }
                            }
                            else
                            {
                                DebugRandAddi.Assert("BogiePath " + id + " has valid RailConnectInfo but node is null");
                                return -1;
                            }
                        }
                        if (inv)
                            Forward = !Forward;
                        Log("BogiePath " + id + " working...");
                        return 0;
                    }

                    internal void Debug_SHOW(Color route, ref int index, Vector3 upNum)
                    {
                        RailConnectInfo RCI = null;
                        foreach (var item in instructions)
                        {
                            RailConnectInfo RCI2 = item.Key;
                            if (RCI == null)
                            {
                                RCI = RCI2;
                            }
                            else
                            {
                                DebugExtUtilities.DrawDirIndicator(RCI.RailEndPositionOnRailScene() + upNum,
                                    RCI2.RailEndPositionOnRailScene() + upNum,
                                    DebugExtUtilities.BlendColors(route, DebugExtUtilities.GetUniqueColor(index), 0.333f),
                                    PathDebugUpdateTime);
                                index++;
                                RCI = RCI2;
                            }
                        }
                    }
                }
            }
        }


            /// <summary>
            /// Only pathfinds the whole route in either forwards or backwards.  
            /// Picks the optimal, shortest route
            /// Does not switch drive directions at intersections since that drastically increases chances of a collision.
            /// </summary>
        public struct TrainPathRequest
        {
            private static bool AllowReverseSearch = true;
            public readonly TankLocomotive train;
            public readonly RailTrackNode target;
            private int expectedFinishedRequests;
            private readonly List<BogiePathingTree> requestsFWD;
            private readonly List<BogiePathingTree> requestsBKD;
            private readonly Dictionary<ModuleRailBogie.RailBogie, BogiePathingTree.BogiePathBranch> finishedRequestsFWD;
            private readonly Dictionary<ModuleRailBogie.RailBogie, BogiePathingTree.BogiePathBranch> finishedRequestsBKD;
            private readonly HashSet<ModuleRailBogie.RailBogie> Bogies;
            public float distFWD;
            public float distBKD;
            private bool FWDObst;
            private bool BKDObst;
            public bool CompletelyObstructed;

            public TrainPathRequest(TankLocomotive train, RailTrackNode target)
            {
                train = train.GetMaster();
                this.train = train;
                this.target = target;
                requestsFWD = new List<BogiePathingTree>();
                requestsBKD = new List<BogiePathingTree>();
                Bogies = new HashSet<ModuleRailBogie.RailBogie>();
                foreach (var item in train.MasterGetAllInterconnectedBogies())
                {
                    Bogies.Add(item);
                }
                expectedFinishedRequests = Bogies.Count;
                finishedRequestsFWD = new Dictionary<ModuleRailBogie.RailBogie, BogiePathingTree.BogiePathBranch>();
                finishedRequestsBKD = new Dictionary<ModuleRailBogie.RailBogie, BogiePathingTree.BogiePathBranch>();
                distFWD = float.MaxValue;
                distBKD = float.MaxValue;
                FWDObst = false;
                BKDObst = false;
                CompletelyObstructed = false;
                foreach (var item in Bogies)
                {
                    if (item.Track != null)
                    {
                        if (item.engine != train && train.GetTankDriveForwardsInRelationToMaster().z < 0)
                        {   // Reversed
                            requestsFWD.Add(new BogiePathingTree(this, item.Track, true, target, item));
                            requestsBKD.Add(new BogiePathingTree(this, item.Track, false, target, item));
                        }
                        else
                        {   // Forwards
                            requestsFWD.Add(new BogiePathingTree(this, item.Track, false, target, item));
                            requestsBKD.Add(new BogiePathingTree(this, item.Track, true, target, item));
                        }
                    }
                }
            }


            public bool PathStep()
            {
                if (!PathStepFWD())
                    return false;
                else if (AllowReverseSearch && !PathStepBKD())
                    return false;
                return true;
            }

            private bool PathStepFWD()
            {
                int bogieQueueStep = 0;
                while (requestsFWD.Count() > bogieQueueStep)
                {
                    var bogie = requestsFWD[bogieQueueStep];
                    switch (bogie.UpdatePathing(out BogiePathingTree.BogiePathBranch branch))
                    {
                        case -1:
                            Log("TrainPathRequest FAILED to find path for a bogie!");
                            requestsFWD.RemoveAt(bogieQueueStep);
                            break;
                        case 1:
                            Log("TrainPathRequest found a path for a bogie");
                            if (!finishedRequestsFWD.TryGetValue(bogie.bogie, out _))
                                finishedRequestsFWD.Add(bogie.bogie, branch);
                            else
                                DebugRandAddi.Assert("TrainPathRequest tried to submit a finished path for a bogie but that bogie already had a finished path!?");
                            requestsFWD.RemoveAt(bogieQueueStep);
                            if (branch.Obstructed)
                                FWDObst = true;
                            break;
                        default:
                            bogieQueueStep++;
                            break;
                    }
                }
                return requestsFWD.Count() == 0;
            }
            private bool PathStepBKD()
            {
                int bogieQueueStep = 0;
                while (requestsBKD.Count() > bogieQueueStep)
                {
                    var bogie = requestsBKD[bogieQueueStep];
                    switch (bogie.UpdatePathing(out BogiePathingTree.BogiePathBranch branch))
                    {
                        case -1:
                            Log("TrainPathRequest(R) FAILED to find path for a bogie!");
                            requestsBKD.RemoveAt(bogieQueueStep);
                            break;
                        case 1:
                            Log("TrainPathRequest(R) found a path for a bogie");
                            if (!finishedRequestsBKD.TryGetValue(bogie.bogie, out _))
                                finishedRequestsBKD.Add(bogie.bogie, branch);
                            else
                                DebugRandAddi.Assert("TrainPathRequest(R) tried to submit a finished path for a bogie but that bogie already had a finished path!?");
                            requestsBKD.RemoveAt(bogieQueueStep);
                            if (branch.Obstructed)
                                BKDObst = true;
                            break;
                        default:
                            bogieQueueStep++;
                            break;
                    }
                }
                return requestsBKD.Count() == 0;
            }


            public bool CalcResults()
            {
                distFWD = TryCalcDistFWD();
                distBKD = TryCalcDistBKD();
                if (distFWD == float.MaxValue && distBKD == float.MaxValue)
                    return false;
                if (FWDObst && BKDObst)
                    CompletelyObstructed = true;
                return true;
            }
            public float GetResults()
            {
                if (distFWD < distBKD)
                    return distFWD;
                return distBKD;
            }
            public void SubmitResults()
            {
                if (distFWD == float.MaxValue && distBKD == float.MaxValue)
                    throw new Exception("TrainPathRequest called SubmitResults() with invalid distances!");
                if (distFWD < distBKD)
                {
                    foreach (var item in finishedRequestsFWD)
                    {
                        Dictionary<RailConnectInfo, int> thePlan = new Dictionary<RailConnectInfo, int>();
                        try
                        {
                            foreach (var item2 in item.Value.instructions)
                            {
                                thePlan.Add(item2.Key, item2.Value);
                                SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + "] => ");
                            }
                            SB.Append("[" + target.NodeID + "]");
                            DebugRandAddi.Log("TrainPathRequest for \"" + item.Key.engine.tank.name + "\" plans: " + SB.ToString());
                        }
                        finally
                        {
                            SB.Clear();
                        }
                        item.Key.SetupPathing(target.NodeID, thePlan);
                    }
                    finishedRequestsFWD.FirstOrDefault().Key.engine.StartPathing(true);
                    DebugRandAddi.Log("\nTrainPathRequest commanded " + finishedRequestsFWD.Count + " bogies with reverse direction: " + false + "\n");
                }
                else
                {
                    foreach (var item in finishedRequestsBKD)
                    {
                        Dictionary<RailConnectInfo, int> thePlan = new Dictionary<RailConnectInfo, int>();
                        try
                        {
                            foreach (var item2 in item.Value.instructions)
                            {
                                thePlan.Add(item2.Key, item2.Value);
                                SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + "] => ");
                            }
                            SB.Append("[" + target.NodeID + "]");
                            DebugRandAddi.Log("TrainPathRequest for \"" + train.name + "\" plans: " + SB.ToString());
                        }
                        finally
                        {
                            SB.Clear();
                        }
                        item.Key.SetupPathing(target.NodeID, thePlan);
                    }
                    finishedRequestsBKD.FirstOrDefault().Key.engine.StartPathing(false);
                    DebugRandAddi.Log("\nTrainPathRequest commanded " + finishedRequestsBKD.Count + " bogies with reverse direction: " + true + "\n");
                }
            }
            private float TryCalcDistFWD()
            {
                if (finishedRequestsFWD.Count != expectedFinishedRequests)
                {
                    return float.MaxValue;
                }
                float accumlativeDist = 0;
                foreach (var item in finishedRequestsFWD)
                {
                    try
                    {
                        accumlativeDist += item.Value.dist;
                        foreach (var item2 in item.Value.instructions)
                        {
                            if (item2.Key != null)
                            {
                                if (!item2.Key.HostNode.Registered())
                                {
                                    DebugRandAddi.Assert("TrainPathRequest cancelled because track nodes changed");
                                    return float.MaxValue;
                                }
                                SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + " | " + item.Value.dist + "] => ");
                            }
                            else
                            {
                                DebugRandAddi.Assert("TrainPathRequest cancelled because track changed");
                                return float.MaxValue;
                            }
                        }
                        SB.Append("[" + target.NodeID + " | " + accumlativeDist + "]");
                        Log("TrainPathRequest for \"" + train.name + "\" FWD plans: " + SB.ToString());
                    }
                    finally
                    {
                        SB.Clear();
                    }
                }
                if (finishedRequestsFWD.Count == 0)
                    return float.MaxValue;
                return accumlativeDist;
            }
            private float TryCalcDistBKD()
            {
                if (finishedRequestsBKD.Count != expectedFinishedRequests)
                {
                    return float.MaxValue;
                }
                float accumlativeDist = 0;
                foreach (var item in finishedRequestsBKD)
                {
                    try
                    {
                        accumlativeDist += item.Value.dist;
                        foreach (var item2 in item.Value.instructions)
                        {
                            if (item2.Key != null)
                            {
                                if (!item2.Key.HostNode.Registered())
                                {
                                    DebugRandAddi.Assert("TrainPathRequest cancelled because track nodes changed");
                                    return float.MaxValue;
                                }
                                SB.Append("[" + item2.Key.HostNode.NodeID + " | " + item2.Value + " | " + item.Value.dist + "] => ");
                            }
                            else
                            {
                                DebugRandAddi.Assert("TrainPathRequest cancelled because track changed");
                                return float.MaxValue;
                            }
                        }
                        SB.Append("[" + target.NodeID + " | " + accumlativeDist + "]");
                        Log("TrainPathRequest for \"" + train.name + "\" BKD plans: " + SB.ToString());
                    }
                    finally
                    {
                        SB.Clear();
                    }
                }
                if (finishedRequestsBKD.Count == 0)
                    return float.MaxValue;
                return accumlativeDist;
            }

            internal void Debug_SHOW(bool backwards)
            {
                int Index = 0;
                if (backwards)
                {
                    for (int step = 0; step < requestsBKD.Count; step++)
                    {
                        requestsBKD[step].Debug_SHOW(true, ref Index, new Vector3(0, step +1, 0));
                    }
                }
                else
                {
                    for (int step = 0; step < requestsFWD.Count; step++)
                    {
                        requestsFWD[step].Debug_SHOW(true, ref Index, new Vector3(0, step + 1, 0));
                    }
                }
            }


            private struct BogiePathingTree
            {
                private readonly TrainPathRequest request;
                public readonly ModuleRailBogie.RailBogie bogie;
                private readonly RailTrackNode dest;
                private readonly List<BogiePathBranch> curOps;
                private List<BogiePathBranch> finished;

                public BogiePathingTree(TrainPathRequest requester, RailTrack start, bool reversed, RailTrackNode destination, ModuleRailBogie.RailBogie bogie)
                {
                    request = requester;
                    this.bogie = bogie;
                    dest = destination;
                    curOps = new List<BogiePathBranch>();
                    bool forwards = bogie.tank.rootBlockTrans.InverseTransformDirection(bogie.CurrentSegment.EvaluateForwards(bogie)).z >= 0;
                    if (reversed)
                        curOps.Add(new BogiePathBranch(start, bogie.CurrentSegmentIndex, 0, null, null, !forwards, "_R"));
                    else
                        curOps.Add(new BogiePathBranch(start, bogie.CurrentSegmentIndex, 0, null, null, forwards, "_F"));
                    finished = new List<BogiePathBranch>();
                }

                public int UpdatePathing(out BogiePathBranch branch)
                {
                    branch = null;
                    int path = UpdatePathing_Internal();
                    if (path == 0)
                        return 0;
                    if (finished.Count == 0)
                        return -1;
                    branch = finished.OrderBy(x => x.dist).FirstOrDefault();
                    return 1;
                }
                private int UpdatePathing_Internal()
                {
                    int steps = 0;
                    while (curOps.Count() > steps)
                    {
                        BogiePathBranch val = curOps[steps];
                        switch (val.Pathfind(this))
                        {
                            case -1:
                                curOps.RemoveAt(steps);
                                break;
                            case 1:
                                finished.Add(val);
                                curOps.RemoveAt(steps);
                                break;
                            default:
                                steps++;
                                break;
                        }
                    }
                    return curOps.Count() == 0 ? 1 : 0;
                }

                internal void Debug_SHOW(bool backwards, ref int index, Vector3 upNum)
                {
                    if (backwards)
                    {
                        for (int step = 0; step < curOps.Count; step++)
                        {
                            curOps[step].Debug_SHOW(Color.red, ref index, upNum);
                        }
                    }
                    else
                    {
                        for (int step = 0; step < curOps.Count; step++)
                        {
                            curOps[step].Debug_SHOW(Color.green, ref index, upNum);
                        }
                    }
                }

                internal class BogiePathBranch
                {
                    private bool Forward;
                    internal RailTrack curTrack;
                    internal HashSet<RailConnectInfo> passed;
                    internal List<KeyValuePair<RailConnectInfo, int>> instructions;
                    internal float dist;
                    internal bool Obstructed;
                    private int curSeg;
                    private string id;

                    public BogiePathBranch(RailTrack start, int currentSegment, float distance, HashSet<RailConnectInfo> passedNodes, List<KeyValuePair<RailConnectInfo, int>> prevNodes, bool fwd, string ID)
                    {
                        Forward = fwd;
                        curTrack = start;
                        curSeg = currentSegment;
                        dist = distance;
                        Obstructed = false;
                        if (passedNodes == null)
                            passed = new HashSet<RailConnectInfo>();
                        else
                            passed = new HashSet<RailConnectInfo>(passedNodes);
                        if (prevNodes == null)
                            instructions = new List<KeyValuePair<RailConnectInfo, int>>();
                        else
                            instructions = new List<KeyValuePair<RailConnectInfo, int>>(prevNodes);
                        id = ID;
                    }

                    public int Pathfind(BogiePathingTree request)
                    {
                        if (curTrack == null)
                        {
                            Log("BogiePath " + id + " next rail null");
                            return -1;
                        }
                        RailTrack prev = curTrack;
                        curTrack = curTrack.PathfindRailStepObeyOneWay(Forward, out bool inv, out RailConnectInfo info, ref curSeg);
                        if (curTrack == null)
                        {
                            Log("BogiePath " + id + " next rail null(2)");
                            return -1;
                        }
                        if (info != null)
                        {
                            RailTrackNode node = info.HostNode;
                            if (info.HostNode == null)
                                throw new NullReferenceException("Pathfind - Somehow HostNode is null");
                            if (node != null)
                            {
                                if (prev == null)
                                    throw new NullReferenceException("Pathfind - Somehow prev is null");
                                int valOut = PathfindNode(request, curTrack, prev, node, info);
                                if (valOut != 0)
                                    return valOut;
                            }
                            else
                            {
                                DebugRandAddi.Assert("BogiePath " + id + " has valid RailConnectInfo but node is null");
                                return -1;
                            }
                        }
                        if (inv)
                            Forward = !Forward;
                        Log("BogiePath " + id + " working...");
                        return 0;
                    }

                    public int PathfindNode(BogiePathingTree request, RailTrack curTrack,
                        RailTrack prev, RailTrackNode node, RailConnectInfo info)
                    {
                        dist += curTrack.distance;
                        if (curTrack.ActiveBogeys == null)
                            throw new NullReferenceException("Pathfind - Somehow ActiveBogeys is null");
                        foreach (var item in curTrack.ActiveBogeys)
                        {
                            if (!request.request.Bogies.Contains(item))
                            {
                                Obstructed = true;
                                if (item.engine.AutopilotActive)
                                    dist += MovingTrainObstructionPenalty;
                                else
                                    dist += StationaryTrainObstructionPenalty;
                            }
                        }
                        if (node == request.dest)
                        {
                            Log("BogiePath " + id + " reached destination");
                            return 1;
                        }
                        if (prev == null || !prev.IsNodeTrack)
                        {   // ignore NodeTracks, only LinkTracks count
                            if (passed.Contains(info))
                            {
                                Log("BogiePath " + id + " looped on itself");
                                return -1; // LOOP 
                            }

                            if (node.NodeType == RailNodeType.Junction && node.GetLinkTrackIndex(prev) == 0)
                            {
                                Log("BogiePath " + id + " split at junction node " + node.NodeID);
                                foreach (var item in node.GetAllConnectedLinks())
                                {
                                    if (item.Index != 0 && item.LinkTrack != null)
                                    {
                                        if (item.LinkTrack != prev)
                                        {
                                            BogiePathBranch path = new BogiePathBranch(item.LinkTrack, curSeg, dist, passed,
                                                instructions, item.LowTrackConnection, id + "_" + node.NodeID + "_" + item);
                                            path.passed.Add(info);
                                            path.instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, item.Index));
                                            request.curOps.Add(path);
                                            Log("BogiePath " + id + " split added " + item);
                                        }
                                    }
                                }
                                passed.Add(info);
                                instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, 0));
                            }
                            else
                            {
                                Log("BogiePath " + id + " passed a point node " + node.NodeID);
                                passed.Add(info);
                                instructions.Add(new KeyValuePair<RailConnectInfo, int>(info, 0));
                            }
                        }
                        return 0;
                    }
                    internal void Debug_SHOW(Color route, ref int index, Vector3 upNum)
                    {
                        RailConnectInfo RCI = null;
                        foreach (var item in instructions)
                        {
                            RailConnectInfo RCI2 = item.Key;
                            if (RCI == null)
                            {
                                RCI = RCI2;
                            }
                            else
                            {
                                DebugExtUtilities.DrawDirIndicator(RCI.RailEndPositionOnRailScene() + upNum, 
                                    RCI2.RailEndPositionOnRailScene() + upNum,
                                    DebugExtUtilities.BlendColors(route, DebugExtUtilities.GetUniqueColor(index), 0.333f), 
                                    PathDebugUpdateTime);
                                index++;
                                RCI = RCI2;
                            }
                        }
                    }
                }
            }
        }
    }
}
