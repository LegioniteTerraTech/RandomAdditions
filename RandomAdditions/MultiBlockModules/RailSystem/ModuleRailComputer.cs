using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RandomAdditions.RailSystem;
using SafeSaves;
using TerraTechETCUtil;
using static FunctionTree;

public class ModuleRailComputer : RandomAdditions.ModuleRailComputer { }

namespace RandomAdditions
{
    /// <summary>
    /// WIP
    /// A computer that can smartly keep track of destinations and drive to them when needed over a predefined schedule.
    /// C&S
    ///   OUTPUTS:
    ///     # - A number for each station
    ///   INPUTS:
    ///     Arrow - Progress to the next station after stopping at current
    ///     [X] - (PULSE) Skip currently selected schedule
    /// </summary>
    [AutoSaveComponent]
    public class ModuleRailComputer : ExtModule
    {
        [SSaveField]
        public List<ScheduleData> Schedule = new List<ScheduleData>();

    }
    [Serializable]
    public enum ECondition
    {
        /// <summary> True if the ModuleRailComputer receives a C&S signal of > 0 </summary>
        CnSSignalSelf,
        /// <summary> True if the station's HALT controller receives a C&S signal of > 0 </summary>
        CnSSignalStation,
        /// <summary> True after delay which starts as soon as it arrives at the station </summary>
        Delay,
        /// <summary> True after not loading after some time </summary>
        ItemsLoading,
    }
    [Serializable]
    public class ScheduleData
    {
        [SSaveField]
        public int targetNode;
        [SSaveField]
        public string StationName;
        [SSaveField]
        public ECondition[] Conditions;
        [SSaveField]
        public float[] ConditionVars;

        public bool SetTarget(ModuleRailStation MRS)
        {
            if (MRS == null)
                return false;
            targetNode = MRS.NodeID;
            StationName = MRS.tank.name;
            return true;
        }
        public bool SetTarget(int nodeID)
        {
            TrackedVisible TV = ManVisible.inst.GetTrackedVisibleByHostID(nodeID);
            if (TV == null || ManVisible.inst.TryGetStoredTechData(TV, out TechData TD, out _))
                return false;
            targetNode = nodeID;
            StationName = TD.Name;
            return true;
        }
        public void Execute(TankLocomotive loco)
        {
            if (ManRails.AllRailNodes.TryGetValue(targetNode, out RailTrackNode RTN))
                throw new NullReferenceException("Target node " + targetNode + " does not exists");
            loco.RegisterAllLinkedLocomotives();
            TankLocomotive master = loco.GetMaster();
            DebugRandAddi.LogRails("(SCHEDULE) Train \"" + master.name + "\" with " + master.TrainLength + " is being sent to node " + targetNode);
            ManTrainPathing.TrainPathfindRailNetwork(loco.GetMaster(), RTN, null);
        }
    }
}
