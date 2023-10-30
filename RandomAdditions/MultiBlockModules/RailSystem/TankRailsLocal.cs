using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.RailSystem
{
    /// <summary>
    /// For techs that have rails mounted to them
    /// </summary>
    public class TankRailsLocal : MonoBehaviour, ITankCompAuto<TankRailsLocal, ModuleRailPoint>, IWorldTreadmill
    {
        public static ITankCompAuto<TankRailsLocal, ModuleRailPoint> stat => null;
        public TankRailsLocal Inst => this;
        public Tank tank { get; set; }
        public HashSet<ModuleRailPoint> Modules { get; set; } = new HashSet<ModuleRailPoint>();

        private Vector3 lastPos = Vector3.zero;
        private bool hasFunicularRailPoint = false;

        internal TankLocomotive train;


        public bool IsFunicular => hasFunicularRailPoint;
        public bool CanUtilizeEnginePower => train && train.HasOwnEngine;

        public void StartManagingPre()
        {
            //tank.CollisionEvent.Subscribe(rails.HandleCollision);
            ManRails.AllRailTechs.Add(this);
            ManWorldTreadmill.inst.AddListener(this);
            train = tank.GetComponent<TankLocomotive>();
        }
        public void StartManagingPost()
        {
        }
        public void StopManaging()
        {
            ManWorldTreadmill.inst.RemoveListener(this);
            ManRails.AllRailTechs.Remove(this);
            //tank.CollisionEvent.Unsubscribe(HandleCollision);
        }
        public void AddModule(ModuleRailPoint point)
        {
            point.rails = this;
        }
        public void RemoveModule(ModuleRailPoint point)
        {
            point.rails = null;
        }

        public void HandleCollision(Tank.CollisionInfo collide, Tank.CollisionInfo.Event whack)
        {
            if (tank.rbody == null || whack != Tank.CollisionInfo.Event.Enter)
                return;
            Tank.CollisionInfo.Obj other;
            Vector3 impulse;
            if (collide.a.tank == tank)
            {
                other = collide.b;
            }
            else
            {
                other = collide.a;
            }
        }
        public void OnMoveWorldOrigin(IntVector3 move)
        {
            lastPos += move;
        }


        public ModuleRailPoint GetFirstPoint()
        {
            return Modules.FirstOrDefault();
        }

    }
}
