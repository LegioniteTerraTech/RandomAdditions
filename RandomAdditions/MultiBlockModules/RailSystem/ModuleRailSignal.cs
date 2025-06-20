using System;
using System.Collections.Generic;
using UnityEngine;
using RandomAdditions.RailSystem;
using SafeSaves;
using TerraTechETCUtil;

public class ModuleRailSignal : RandomAdditions.ModuleRailSignal { };
namespace RandomAdditions
{
    // Used to keep "trains" on the rails, might come in 2024, we'll see
    //  Connects to other segments in the world, loading the tiles if needed
    [AutoSaveComponent]
    public class ModuleRailSignal : ModuleRailPoint
    {
        public bool StopWhenWarn = false; // Enable this for HIGH-SPEED lines like Venture's Wheesh Rails

        private Transform RailLightRedTrans;
        private Light RailLightRed;
        private Transform RailLightWarnTrans;
        private Light RailLightWarn;
        private Transform RailLightGreenTrans;
        private Light RailLightGreen;
        private Transform semaphore;
        protected override void Pool()
        {
            ManRails.InitExperimental();
            enabled = true;
            GetSignal();
            GetTrackHubs();
            if (LinkHubs.Count > 2)
            {
                block.damage.SelfDestruct(0.1f);
                BlockDebug.ThrowWarning(true, "RandomAdditions: ModuleRailSignal cannot host more than two \"_trackHub\" GameObjects.  Use ModuleRailJunction instead.\nThis operation cannot be handled automatically.\nCause of error - Block " + gameObject.name);
                return;
            }
        }
        private static LocExtStringMod LOC_ModuleRailPoint_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,
                AltUI.HighlightString("Guides") + " designate a path for " + AltUI.ObjectiveString("Tracks") +
            " to follow. " + AltUI.HighlightString("Right-Click") + " or use the map to link " +
            AltUI.ObjectiveString("Tracks") + "."},
            { LocalisationEnums.Languages.Japanese,
                AltUI.HighlightString("『Guide』") + "は、トラックがたどる道筋を示す。 " + AltUI.HighlightString("右クリック") +
                            "またはマップを使用してトラックをリンクします"},
        });
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleRailPoint", LOC_ModuleRailPoint_desc);
        public override void OnGrabbed()
        {
            hint.Show();
        }
        protected void GetSignal()
        {
            RailLightRedTrans = KickStart.HeavyTransformSearch(transform, "_trackSignalOn");
            if (RailLightRedTrans)
                RailLightRed = RailLightRedTrans.GetComponent<Light>();
            RailLightWarnTrans = KickStart.HeavyTransformSearch(transform, "_trackSignalWarn");
            if (RailLightWarnTrans)
                RailLightWarn = RailLightWarnTrans.GetComponent<Light>();
            RailLightGreenTrans = KickStart.HeavyTransformSearch(transform, "_trackSignalOff");
            if (RailLightGreenTrans)
                RailLightGreen = RailLightGreenTrans.GetComponent<Light>();
            semaphore = KickStart.HeavyTransformSearch(transform, "_semaphore");
        }

        public void GetOtherSignals(List<ModuleRailSignal> cache)
        {
            if (Node != null)
            {
                RailTrackNode RTN = Node;
                foreach (var item in RTN.GetAllConnectedLinks())
                {
                    RailTrackNode RTNO = item.GetOtherSideNode();
                    if (RTNO != RTN && RTNO.Point != null && RTNO.Point != this && RTNO.Point is ModuleRailSignal MRS)
                        cache.Add(MRS);
                }
            }
        }

        private int pastLightStatus = -1;
        private float curAngle = 0;
        private float aimedAngle = 0;
        public void Update()
        {
            if (curAngle != aimedAngle && semaphore)
            {
                if (curAngle.Approximately(aimedAngle, 0.05f))
                    curAngle = aimedAngle;
                else
                    curAngle = Mathf.LerpAngle(curAngle, aimedAngle, Time.deltaTime * 2);
                semaphore.localEulerAngles = semaphore.localEulerAngles.SetX(curAngle);
            }
        }
        protected override void PostUpdate(int lightStatus)
        {

            if (StopWhenWarn && lightStatus == 1)
                lightStatus = 2;
            if (pastLightStatus != lightStatus)
            {
                //DebugRandAddi.Assert("PostUpdate");
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
                        aimedAngle = 0;
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
                        aimedAngle = 45;
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
                        aimedAngle = 45;
                        break;
                }
            }
        }
    }
}
