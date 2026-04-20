using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SafeSaves;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// WIP!!!!!
    /// </summary>
    [AutoSaveComponent]
    [Doc("Alternate ModuleTrajectory for Replicas")]
    public class ModuleTrajectoryReplica : ModuleTrajectory, ReplicaControllable
    {
        [SSaveField]
        public string blockTypeName = string.Empty;
        [SSaveField]
        public float launchVelo = 10;
        public string setCache = string.Empty;
        protected override void Pool()
        { 
            base.Pool();
            VisibleCondition = TrajectoryVisibility.Always;
            HideOnLockOn = false;
            HideOnDetach = false;
        }
        public void TryAttachWeaponStats()
        {
            BlockTypes curSeshBT = BlockIndexer.GetBlockIDLogFree(blockTypeName);
            if (curSeshBT != BlockTypes.GSOAIController_111)
            {
                var tankblock = ManSpawn.inst.GetBlockPrefab(curSeshBT);
                if (tankblock != null)
                {
                    RecalibrateLine(tankblock.gameObject);
                }
            }
        }
        public void GetOurButtons(ModuleUIButtons buttons)
        {
            buttons.AddElement(() => "Open Menu", ToggleOpenGUI,
                () => UIHelpersExt.GetGUIIcon("Icon_Options"));
        }
        public bool SettingAimLoc = false;
        public float ToggleOpenGUI(float unused)
        {
            ManModGUI.AddEscapeableCallback(CancelSetAim, true);
            SettingAimLoc = true;
            return 1;
        }
        private void CancelSetAim()
        {
            SettingAimLoc = false;
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
            ManModGUI.RemoveEscapeableCallback(CancelSetAim, true);
        }
        public void DisplayOnReplicaGUI(ModuleReplica MR)
        {
            if (TempPortedGUI.DisplayFloat(launchVelo, ref setCache, out float launchVelo2))
                launchVelo = launchVelo2;
        }
        private static int layerMask = Globals.inst.layerTank.mask | Globals.inst.layerTerrain.mask;
        public void Update()
        {
            if (SettingAimLoc)
            {   // update the raycast realtime
                if (Physics.Raycast(ManUI.inst.ScreenPointToRay(Input.mousePosition),
                    out var rayman, 500, layerMask, QueryTriggerInteraction.Ignore))
                {
                    
                    if (Input.GetMouseButtonDown(1))
                    {
                        //rayman.point
                    }
                }
            }
        }

    }
}
