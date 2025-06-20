using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    public enum TrajectoryVisibility : byte
    { 
        Always,
        SameTeam,
        CurrentTech
    }
    /// <summary>
    /// Shows a rough line of the optimal predicted flight path of a ballistic projectile [WIP]
    /// </summary>
    [Doc("Shows a rough line of the optimal predicted flight path of a ballistic projectile")]
    public class ModuleTrajectory : ExtModule
    {

        private LineRenderer Liner;
        private Vector3[] pos;
        private float distTimeDeltaCalc;
        private float distGravTimeDeltaCalc;
        private GravitateProjectile gp;

        public TrajectoryVisibility VisibleCondition = TrajectoryVisibility.Always;
        public bool HideOnLockOn = true;
        public float Distance = 10;
        public float LaunchVelocity = 10;
        public float GravityMultiplier = -1;
        private static LocExtStringMod LOC_ModuleTrajectory_desc = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "This block displays the approximate arc of the attached " + AltUI.HighlightString("Weapon's") +
                        " projectile."},
            { LocalisationEnums.Languages.Japanese, "このブロックは、取り付けられた武器の軌道を表示します"},
        });
        public static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleTrajectory", LOC_ModuleTrajectory_desc);


        public void OnPool()
        {
            Liner = transform.HeavyTransformSearch("_tracer")?.GetComponent<LineRenderer>();
            var MWG = GetComponent<ModuleWeaponGun>();
            var FD = GetComponent<FireData>();
            if (Liner == null)
            {
                Liner = new GameObject("TracerLine").AddComponent<LineRenderer>();
                Liner.material = ResourcesHelper.GetMaterialFromBaseGameAllFast("default");
                Liner.positionCount = 16;
                Liner.startColor = new Color(1, 1, 1, 0.8f);
                Liner.endColor = new Color(1, 1, 1, 0.8f);
                Liner.startWidth = 0.35f;
                Liner.endWidth = 0.5f;
                if (MWG?.GetFireTransform() && FD.m_BulletPrefab?.GetComponent<Rigidbody>())
                {
                    var fTrans = MWG.GetFireTransform();
                    Liner.transform.SetParent(fTrans, false);
                    Liner.transform.localRotation = Quaternion.identity;
                    Liner.transform.localPosition = Vector3.zero;
                    Liner.transform.localScale = Vector3.one;
                    var proj = FD.m_BulletPrefab;
                    if (proj.GetComponent<Rigidbody>().useGravity)
                    {
                        GravityMultiplier = 1;
                    }
                    else
                        GravityMultiplier = 0;
                }
                else
                {
                    if (GravityMultiplier < 0)
                        GravityMultiplier = 0;
                }
            }
            else
            {
                if (GravityMultiplier < 0)
                    GravityMultiplier = 0;
            }
            if (FD.m_BulletPrefab)
                gp = FD.m_BulletPrefab.GetComponent<GravitateProjectile>();
            pos = new Vector3[Liner.positionCount];
            distTimeDeltaCalc = Distance / (Liner.positionCount * LaunchVelocity);
            distGravTimeDeltaCalc = distTimeDeltaCalc * GravityMultiplier;
        }

        public override void OnAttach()
        {
            hint.Show();
            Revalidate();
            switch (VisibleCondition)
            {
                case TrajectoryVisibility.SameTeam:
                    ManTechs.inst.TankTeamChangedEvent.Subscribe(Revalidate);
                    break;
                case TrajectoryVisibility.CurrentTech:
                    ManTechs.inst.TankDriverChangedEvent.Subscribe(Revalidate);
                    break;
            }
        }
        public override void OnDetach()
        {
            SetShown(false);
            switch (VisibleCondition)
            {
                case TrajectoryVisibility.SameTeam:
                    ManTechs.inst.TankTeamChangedEvent.Unsubscribe(Revalidate);
                    break;
                case TrajectoryVisibility.CurrentTech:
                    ManTechs.inst.TankDriverChangedEvent.Unsubscribe(Revalidate);
                    break;
            }
        }

        public void SetShown(bool state)
        { 
            Liner.gameObject.SetActive(state);
            enabled = state;
        }


        public void Revalidate(Tank cTank, ManTechs.TeamChangeInfo info) => Revalidate(cTank);
        public void Revalidate(Tank cTank)
        {
            if (cTank == tank)
                Revalidate();
        }
        public void Revalidate()
        {
            switch (VisibleCondition)
            {
                case TrajectoryVisibility.SameTeam:
                    if (tank.Team == ManPlayer.inst.PlayerTeam)
                    {
                        SetShown(true);
                        return;
                    }
                    break;
                case TrajectoryVisibility.CurrentTech:
                    if (tank == Singleton.playerTank)
                    {
                        SetShown(true);
                        return;
                    }
                    break;
                case TrajectoryVisibility.Always:
                default:
                    SetShown(true);
                    return;
            }
            SetShown(false);
        }


        public void FixedUpdate()
        {
            Vector3 precalcPos = gameObject.transform.position;
            Vector3 veloFrame = gameObject.transform.forward * LaunchVelocity;
            pos[0] = precalcPos;
            for (int i = 1; i < pos.Length; i++)
            {
                veloFrame += Physics.gravity * distGravTimeDeltaCalc;
                if (gp)
                {
                    veloFrame += gp.PredictPosVelo(precalcPos) * distTimeDeltaCalc;
                    if (gp.WorldAugmentedDragEnabled)
                        veloFrame *= 1f - gp.WorldAugmentedDragStrength;
                }
                precalcPos += veloFrame;
                pos[i] = precalcPos;
            }
            Liner.SetPositions(pos);
        }
    }
}
