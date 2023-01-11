using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;

public class ModuleRailStation : RandomAdditions.ModuleRailStation { };
namespace RandomAdditions
{
    /// <summary>
    /// The point where trains can start and stop.
    /// </summary>
    public class ModuleRailStation : ModuleRailPoint
    {
        private TankLocomotive trainEnRoute = null;
        protected override void Pool()
        {
            enabled = true;
            try
            {
            }
            catch { }

            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailPoint cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }


        public void TryCallTrain()
        {
            if (trainEnRoute)
            {
                trainEnRoute.FinishPathing(TrainArrivalStatus.Cancelled);
                trainEnRoute = null;
            }
            else
            {
                DebugRandAddi.Log("\nStation \"" + block.tank.name + "\" requesting nearest train on network");
                ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Craft);
                ManTrainPathing.TrainStatusPopup("Calling...", WorldPosition.FromScenePosition(transform.position + Vector3.up));
                ManTrainPathing.QueueFindNearestTrainInRailNetworkAsync(Node, OnTrainFound);
                //ManTrainPathing.TrainStatusPopup("Calling!", WorldPosition.FromScenePosition(transform.position));
            }
        }

        private float timeCase = 0;
        public void OnTrainFound(TankLocomotive engine)
        {
            if (this == null || !engine)
            {
                ManTrainPathing.TrainStatusPopup("No Trains!", WorldPosition.FromScenePosition(transform.position));
                return; // Can't call to an unloaded station! 
            }
            engine = engine.GetMaster();
            DebugRandAddi.Log("\nTrain " + engine.name + " is riding to station");
            engine.AutopilotFinishedEvent.Subscribe(OnTrainDrivingEnd);
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AIFollow);
            ManTrainPathing.TrainStatusPopup("Called!", WorldPosition.FromScenePosition(engine.tank.boundsCentreWorld));
            ManTrainPathing.TrainStatusPopup(engine.name +" - OMW", WorldPosition.FromScenePosition(transform.position));
            timeCase = Time.time;
            trainEnRoute = engine;
        }

        public void OnTrainDrivingEnd(TrainArrivalStatus success)
        {
            if (this == null)
            {
                DebugRandAddi.Assert("\nModuleRailStation - STATION IS NULL");
                return; // Can't call to an unloaded station! 
            }
            trainEnRoute = null;
            switch (success)
            {
                case TrainArrivalStatus.Arrived:
                    DebugRandAddi.Log("\nTrain arrived at station at " + (Time.time - timeCase) + " seconds");
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.EarnXP);
                    ManTrainPathing.TrainStatusPopup("Train Arrived", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.Cancelled:
                    DebugRandAddi.Assert("Train trip cancelled at " + (Time.time - timeCase) + " seconds");
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                    ManTrainPathing.TrainStatusPopup("Cancelled", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.NoPath:
                    DebugRandAddi.Assert("Train could not find path to station at " + (Time.time - timeCase) + " seconds");
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Train Stopped", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.Derailed:
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Train Derailed", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.Destroyed:
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Train Exploded", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.TrackSabotaged:
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Tracks Damaged", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.TrainBlockingPath:
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Trains Stuck", WorldPosition.FromScenePosition(transform.position));
                    break;
                case TrainArrivalStatus.PlayerHyjacked:
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AnchorFailed);
                    ManTrainPathing.TrainStatusPopup("Player Interrupted", WorldPosition.FromScenePosition(transform.position));
                    break;
                default:
                    DebugRandAddi.Assert("\nModuleRailStation - Invalid TrainArrivalStatus " + success);
                    break;
            }
        }
    }
}
