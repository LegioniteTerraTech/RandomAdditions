using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomAdditions
{
    public enum WeaponDongleType
    {
        // GSO - Modular Cannon
        BarrelFlute,// +Recoil              -Shell Speed
        BarrelGauge,// +Damage(General)     -Responsivness
        Payload,    // +Explosive Damage    -Cooldown
        Propellent, // +Shell Speed         -Cooldown
        // GC - Modular Drill
        BarrelShaft,// +nothing             -nothing
        BarrelBore, // +Cutting Damage      -WEIGHT
        Flywheel,   // +Impact Damage       -Responsivness
        Motor,      // +Responsivness       -Cooldown
        // VEN - Modular Autocannon
        BarrelRifle,// +Shell Speed         -Damage(General)
        BarrelSplit,// +Bullet Firerate     -Alpha Strike
        Shells,     // +Bullet Damage       -Alpha Strike
        Autoloader, // +Cooldown            -Accuraccy
        // HE - Modular Missiles
        BarrelBox,  // +ARMORED             -Responsivness
        BarrelRail, // +Shell Speed         -FRAGILE
        AmmoRack,   // +Burst Reserve       -Cooldown
        Computer,   // +Homing Ability      -Max Speed
        // BF - Modular Laser Beam
        BarrelTube, // +Accuraccy           -Shell Speed
        Servo,      // +Accuraccy           -FRAGILE
        Amplifiers, // +Energy Damage       -Accuraccy
        // RR - Modular Tesla
        BarrelTesla,// +Raycast Range       ---Range
        Coil,       // +Damage(General)     -DANGER
        Capacitors, // +++Fire Damage       -Power Use
        // TAC?
        EPMInjector,// +Plasma Damage       -
    }

    public class WeaponDongleStats
    {
        private const float DamageVolume = 50;
        internal static Dictionary<WeaponDongleType, WeaponDongleStats> defaultDongleStats = new Dictionary<WeaponDongleType, WeaponDongleStats>
        {
            // Barrels
            { WeaponDongleType.BarrelBore, new WeaponDongleStats{
                Accuraccy = 2.5f,
                DamageType = ManDamage.DamageType.Cutting,
                Damage = DamageVolume,
                // HEAVY
            } },
            { WeaponDongleType.BarrelBox, new WeaponDongleStats{
                Accuraccy = 3f,
                // DURABLE
                // HEAVY
            } },
            { WeaponDongleType.BarrelFlute, new WeaponDongleStats{
                Accuraccy = 3f,
                Speed = -2,
            } },
            { WeaponDongleType.BarrelGauge, new WeaponDongleStats{
                Accuraccy = 3f,
                Damage = DamageVolume,
                Responsivness = -2,
            } },
            { WeaponDongleType.BarrelRail, new WeaponDongleStats{
                Accuraccy = 4f,
                Speed = 5,
                // FRAGILE
            } },
            { WeaponDongleType.BarrelRifle, new WeaponDongleStats{
                Damage = -50,
                Accuraccy = 3f,
                Speed = 2,
            } },
            { WeaponDongleType.BarrelShaft, new WeaponDongleStats{
                Accuraccy = 5f,
            } },
            { WeaponDongleType.BarrelSplit, new WeaponDongleStats{
                Special = 1,
                Accuraccy = 2.5f,
            } },
            { WeaponDongleType.BarrelTesla, new WeaponDongleStats{
                Accuraccy = 3.75f,
                Energy = 3,
                Speed = 10,
            } },
            { WeaponDongleType.BarrelTube, new WeaponDongleStats{
                Accuraccy = 4,
                Speed = -3,
            } },

            // Dongles
            { WeaponDongleType.AmmoRack, new WeaponDongleStats{
                Burst = 3,
            } },
            { WeaponDongleType.Amplifiers, new WeaponDongleStats{
                DamageType = ManDamage.DamageType.Energy,
                Damage = 100,
                Speed = -0.5f,
            } },
            { WeaponDongleType.Autoloader, new WeaponDongleStats{
                Responsivness = 0.5f,
                Firerate = 5,
                Accuraccy = -3,
            } },
            { WeaponDongleType.Capacitors, new WeaponDongleStats{
                Damage = DamageVolume * 3,
                Energy = 2,
            } },
            { WeaponDongleType.Computer, new WeaponDongleStats{
                Speed = -3,
                Homing = 1,
            } },
            { WeaponDongleType.Flywheel, new WeaponDongleStats{
                DamageType = ManDamage.DamageType.Impact,
                Damage = DamageVolume,
                Responsivness = -1.5f,
            } },
            { WeaponDongleType.Motor, new WeaponDongleStats{
                Accuraccy = -2f,
                Responsivness = 3,
            } },
            { WeaponDongleType.Payload, new WeaponDongleStats{
                DamageType = ManDamage.DamageType.Explosive,
                Damage = DamageVolume,
                Accuraccy = -1f,
            } },
            { WeaponDongleType.Propellent, new WeaponDongleStats{
                Firerate = -1.5f,
                Speed = 3f,
            } },
            { WeaponDongleType.Shells, new WeaponDongleStats{
                DamageType = ManDamage.DamageType.Bullet,
                Damage = DamageVolume,
            } },
            { WeaponDongleType.EPMInjector, new WeaponDongleStats{
                DamageType = ManDamage.DamageType.Plasma,
                Damage = DamageVolume,
                Speed = -1f,
            } },
        };


        /// <summary>Damage to deal</summary>
        public ManDamage.DamageType DamageType = ManDamage.DamageType.Standard;
        /// <summary>How much damage to deal</summary>
        public float Damage = 0;
        /// <summary>How accurate it is: 100 means perfect accuracy</summary>
        public float Accuraccy = 0;
        /// <summary>Velocity of attack</summary>
        public float Speed = 0;
        /// <summary>How frequently it fires</summary>
        public float Firerate = 0;
        /// <summary>How much energy it should consume</summary>
        public float Energy = 0;
        /// <summary>Responsivness - Related</summary>
        public float Responsivness = 0;
        /// <summary>Homing - Related</summary>
        public float Homing = 0;
        /// <summary>Split barrels!</summary>
        public float Special = 0;
        /// <summary>Burstfire!</summary>
        public float Burst = 0;
    }
}
