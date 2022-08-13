using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    public class WeaponDongleStats
    {
        public ManDamage.DamageType SuggestType = ManDamage.DamageType.Standard;
        public float Responsivness = 0;
        public float Damage = 0;
        public float Speed = 0;
        public float Firerate = 0;
        public float Accuraccy = 0;
        public float Energy = 0;
        public float Homing = 0;
    }
    public class WeaponTypeStats
    {
        public ManDamage.DamageType SuggestType = ManDamage.DamageType.Standard;
        public float Damage = 0;
    }

    /// <summary>
    /// Slated for 2023 maybe, depends on various availability factors.
    /// Modular weapons system that supports cross-corp addons 
    /// </summary>
    public class ModulePartWeapon : ExtModule
    {

        internal static Dictionary<WeaponDongleType, WeaponDongleStats> defaultDongleStats = new Dictionary<WeaponDongleType, WeaponDongleStats>
        {
            { WeaponDongleType.Amplifiers, new WeaponDongleStats{
                SuggestType = ManDamage.DamageType.Energy,
                Speed = -0.5f,
                Damage = 1,
            } },
            { WeaponDongleType.Autoloader, new WeaponDongleStats{
                Responsivness = 0.5f,
                Speed = 3,
                Accuraccy = -1,
            } },
            { WeaponDongleType.Capacitors, new WeaponDongleStats{
                Damage = 5,
                Energy = 1,
            } },
            { WeaponDongleType.Computer, new WeaponDongleStats{
                Speed = -3,
                Homing = 1,
            } },
            { WeaponDongleType.Flywheel, new WeaponDongleStats{
                SuggestType = ManDamage.DamageType.Impact,
                Responsivness = -1.5f,
            } },
            { WeaponDongleType.Motor, new WeaponDongleStats{
                Accuraccy = -2f,
                Responsivness = 3,
            } },
            { WeaponDongleType.Payload, new WeaponDongleStats{
                SuggestType = ManDamage.DamageType.Explosive,
                Accuraccy = -1f,
                Damage = 1,
            } },
            { WeaponDongleType.Propellent, new WeaponDongleStats{
                Firerate = -1.5f,
                Speed = 3f,
            } },
            { WeaponDongleType.Shells, new WeaponDongleStats{
                SuggestType = ManDamage.DamageType.Bullet,
                Damage = -0.5f,
            } },
            { WeaponDongleType.EPMInjector, new WeaponDongleStats{
                SuggestType = ManDamage.DamageType.Plasma,
                Speed = -1f,
            } },
        };

        internal List<ModulePartWeaponDongle> attached = new List<ModulePartWeaponDongle>();

        public static Dictionary<WeaponDongleType, WeaponDongleStats> DongleStats = new Dictionary<WeaponDongleType, WeaponDongleStats>();


        public Dictionary<ManDamage.DamageType, WeaponTypeStats> DamageTypeDistrib = new Dictionary<ManDamage.DamageType, WeaponTypeStats>();
        public float BaseResponsivness = 8;
        public float BaseDamage = 4;
        public float BaseSpeed = 10;
        public float BaseFirerate = 6;
        public float BaseAccuraccy = 32;
        public float BaseEnergy = 0;
        public float BaseHoming = 0;

        private float AddedResponsivness = 0;
        private float AddedDamage = 0;
        private float AddedSpeed = 0;
        private float AddedFirerate = 0;
        private float AddedAccuraccy = 0;
        private float AddedEnergy = 0;
        private float AddedHoming = 0;

        protected float TotalResponsivness = 0;
        protected float TotalDamage = 0;
        protected float TotalSpeed = 0;
        protected float TotalFirerate = 0;
        protected float TotalAccuraccy = 0;
        protected float TotalEnergy = 0;
        protected float TotalHoming = 0;


        protected override void Pool()
        {
        }

        public override void OnAttach()
        {
            ReconnectAll();
            AttachEvent();
        }

        public override void OnDetach()
        {
            DisconnectAll();
            DetachEvent();
        }

        public virtual void AttachEvent()
        {
        }

        public virtual void DetachEvent()
        {
        }
        public virtual ManDamage.DamageType DefaultDamageType()
        {
            return ManDamage.DamageType.Standard;
        }



        public void Connect(ModulePartWeaponDongle dongle)
        {
            dongle.assigned = this;
            attached.Add(dongle);
            ConnectEvent(dongle);
            ReconnectAll();
            RecalcStats();
        }
        public void Disconnect(ModulePartWeaponDongle dongle)
        {
            DisconnectEvent(dongle);
            attached.Remove(dongle);
            dongle.assigned = null;
            ReconnectAll();
            RecalcStats();
        }

        public virtual void ConnectEvent(ModulePartWeaponDongle dongle)
        {
        }
        public virtual void DisconnectEvent(ModulePartWeaponDongle dongle)
        {
        }

        public virtual void RecalcStatsEvent()
        {
        }

        private void RecalcStats()
        {
            DamageTypeDistrib.Clear();
            AddedAccuraccy = 0;
            AddedEnergy = 0;
            AddedResponsivness = 0;
            AddedSpeed = 0;
            AddedDamage = 0;
            AddedHoming = 0;
            AddedFirerate = 0;
            RecalcStatsEvent();
            foreach (var item in attached)
            {
                WeaponDongleStats val;
                if (DongleStats.TryGetValue(item.Type, out val))
                {
                    AddedAccuraccy += val.Accuraccy;
                    AddedEnergy += val.Energy;
                    AddedResponsivness += val.Responsivness;
                    AddedSpeed += val.Speed;
                    AddedFirerate += val.Firerate;
                    AddedHoming += val.Homing;
                    if (val.SuggestType != ManDamage.DamageType.Standard && val.SuggestType != DefaultDamageType())
                    {
                        WeaponTypeStats stats;
                        if (DamageTypeDistrib.TryGetValue(val.SuggestType, out stats))
                        {
                            stats.Damage += val.Damage;
                        }
                        else
                        {
                            stats = new WeaponTypeStats {
                                SuggestType = val.SuggestType,
                                Damage = val.Damage,
                            };
                            DamageTypeDistrib.Add(val.SuggestType, stats);
                        }
                    }
                    else
                    {
                        AddedDamage += val.Damage;
                    }
                }
                else if (defaultDongleStats.TryGetValue(item.Type, out val))
                {
                    AddedAccuraccy += val.Accuraccy;
                    AddedEnergy += val.Energy;
                    AddedResponsivness += val.Responsivness;
                    AddedSpeed += val.Speed;
                    AddedFirerate += val.Firerate;
                    AddedHoming += val.Homing;
                    if (val.SuggestType != ManDamage.DamageType.Standard && val.SuggestType != DefaultDamageType())
                    {
                        WeaponTypeStats stats;
                        if (DamageTypeDistrib.TryGetValue(val.SuggestType, out stats))
                        {
                            stats.Damage += val.Damage;
                        }
                        else
                        {
                            stats = new WeaponTypeStats
                            {
                                SuggestType = val.SuggestType,
                                Damage = val.Damage,
                            };
                            DamageTypeDistrib.Add(val.SuggestType, stats);
                        }
                    }
                    else
                    {
                        AddedDamage += val.Damage;
                    }
                }
            }
            TotalAccuraccy = BaseAccuraccy + AddedAccuraccy;
            TotalSpeed = BaseSpeed + AddedSpeed;
            TotalDamage = BaseDamage + AddedDamage;
            TotalResponsivness = BaseResponsivness + AddedResponsivness;
            TotalEnergy = BaseEnergy + AddedEnergy;
            TotalFirerate = BaseFirerate + AddedFirerate;
            TotalHoming = BaseHoming + AddedHoming;
        }


        private void DisconnectAll()
        {
            List<ModulePartWeaponDongle> temp = new List<ModulePartWeaponDongle>(attached);
            foreach (var item in temp)
            {
                Disconnect(item);
            }
            attached.Clear();
        }
        private void ConnectAll()
        {
            EventNoParams eventCase = new EventNoParams();
            RecurseConnectivity(eventCase);
            eventCase.Send();
            eventCase.EnsureNoSubscribers();
        }
        private void ReconnectAll()
        {
            DisconnectAll();
            ConnectAll();
        }

        private bool RecurseConnectivity(EventNoParams act)
        {
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeaponDongle>();
                    if (module && !module.recursed)
                    {
                        module.recursed = true;
                        act.Subscribe(module.EndRecurse);
                        if (module.RecurseCheckConnected(act))
                            return true;
                    }
                }
            }
            return false;
        }
    }

}
