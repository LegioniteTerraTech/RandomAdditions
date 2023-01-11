using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;
using SafeSaves;

public class ModuleRailSignal : RandomAdditions.ModuleRailSignal { };
namespace RandomAdditions
{
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed
    [AutoSaveComponent]
    public class ModuleRailSignal : ModuleRailPoint
    {
        private Transform RailLightRedTrans;
        private Light RailLightRed;
        private Transform RailLightWarnTrans;
        private Light RailLightWarn;
        private Transform RailLightGreenTrans;
        private Light RailLightGreen;
        protected override void Pool()
        {
            enabled = true;
            GetSignal();
            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                LogHandler.ThrowWarning("RandomAdditions: ModuleRailSignal cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }
        protected void GetSignal()
        {
            try
            {
                RailLightRedTrans = KickStart.HeavyObjectSearch(transform, "_trackSignalOn");
                if (RailLightRedTrans)
                    RailLightRed = RailLightRedTrans.GetComponent<Light>();
                RailLightWarnTrans = KickStart.HeavyObjectSearch(transform, "_trackSignalWarn");
                if (RailLightWarnTrans)
                    RailLightWarn = RailLightWarnTrans.GetComponent<Light>();
                RailLightGreenTrans = KickStart.HeavyObjectSearch(transform, "_trackSignalOff");
                if (RailLightGreenTrans)
                    RailLightGreen = RailLightGreenTrans.GetComponent<Light>();
            }
            catch { }
        }

        public List<ModuleRailSignal> GetOtherSignals()
        {
            List<ModuleRailSignal> points = new List<ModuleRailSignal>();
            TrainInStretch = false;
            if (Node != null)
            {
                RailTrackNode RTN = Node;
                foreach (var item in RTN.GetAllConnectedLinks())
                {
                    RailTrackNode RTNO = RTN.GetConnection(item).GetOtherSideNode(RTN);
                    if (RTNO != RTN && RTNO.Point != null && RTNO.Point != this && RTNO.Point is ModuleRailSignal MRS)
                        points.Add(MRS);
                }
            }
            return points;
        }

        private int pastLightStatus = 0;
        protected override void PostUpdate(int lightStatus)
        {
            if (pastLightStatus != lightStatus)
            {
                pastLightStatus = lightStatus;
                switch (lightStatus)
                {
                    case 2:
                        if (RailLightRedTrans)
                        {
                            RailLightRedTrans.gameObject.SetActive(true);
                            if (RailLightRed)
                                RailLightRed.enabled = true;
                        }
                        if (RailLightWarnTrans)
                        {
                            RailLightWarnTrans.gameObject.SetActive(false);
                            if (RailLightWarn)
                                RailLightWarn.enabled = false;
                        }
                        if (RailLightGreenTrans)
                        {
                            RailLightGreenTrans.gameObject.SetActive(false);
                            if (RailLightGreen)
                                RailLightGreen.enabled = false;
                        }
                        break;
                    case 1:
                        if (RailLightRedTrans)
                        {
                            RailLightRedTrans.gameObject.SetActive(false);
                            if (RailLightRed)
                                RailLightRed.enabled = false;
                        }
                        if (RailLightWarnTrans)
                        {
                            RailLightWarnTrans.gameObject.SetActive(true);
                            if (RailLightWarn)
                                RailLightWarn.enabled = true;
                        }
                        if (RailLightGreenTrans)
                        {
                            RailLightGreenTrans.gameObject.SetActive(false);
                            if (RailLightGreen)
                                RailLightGreen.enabled = false;
                        }
                        break;
                    case 0:
                        if (RailLightRedTrans)
                        {
                            RailLightRedTrans.gameObject.SetActive(false);
                            if (RailLightRed)
                                RailLightRed.enabled = false;
                        }
                        if (RailLightWarnTrans)
                        {
                            RailLightWarnTrans.gameObject.SetActive(false);
                            if (RailLightWarn)
                                RailLightWarn.enabled = false;
                        }
                        if (RailLightGreenTrans)
                        {
                            RailLightGreenTrans.gameObject.SetActive(true);
                            if (RailLightGreen)
                                RailLightGreen.enabled = true;
                        }
                        break;
                }
            }
        }
    }
}
