using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;
using RandomAdditions.Scanners;

namespace RandomAdditions
{
    public interface BlockScannerTarg
    {
        int GetSignalInt();
        int GetSignal999();
        int GetSignalAnalog();
        string GetName();
        Sprite GetIcon();
        void OnRelease();
    }
    public class BlockScannerUtil
    {
        public static BlockScannerTarg FindBestScanner(GameObject targInst)
        {
            // Top Priority
            if (ScannerAutominer.CanUse(targInst))
                return new ScannerAutominer(targInst);
            if (ScannerCab.CanUse(targInst))
                return new ScannerCab(targInst);

            // High Priority
            if (ScannerHolder.CanUse(targInst))
                return new ScannerHolder(targInst);
            if (ScannerTargeter.CanUse(targInst))
                return new ScannerTargeter(targInst);

            // Mid Priority
            if (ScannerModeSwitch.CanUse(targInst))
                return new ScannerModeSwitch(targInst);
            if (ScannerWeapon.CanUse(targInst))
                return new ScannerWeapon(targInst);
            if (ScannerMelee.CanUse(targInst))
                return new ScannerMelee(targInst);
            if (ScannerSilo.CanUse(targInst))
                return new ScannerSilo(targInst);

            // Low-Mid Priority
            if (ScannerBattery.CanUse(targInst))
                return new ScannerBattery(targInst);
            if (ScannerBattery2.CanUse(targInst))
                return new ScannerBattery2(targInst);
            if (ScannerFuel.CanUse(targInst))
                return new ScannerFuel(targInst);
            if (ScannerFuel2.CanUse(targInst))
                return new ScannerFuel2(targInst);

            // Low Priority
            if (ScannerAltitude.CanUse(targInst))
                return new ScannerAltitude(targInst);
            if (ScannerSpeed.CanUse(targInst))
                return new ScannerSpeed(targInst);
            if (ScannerTargeter.CanUse(targInst))
                return new ScannerTargeter(targInst);
            if (ScannerWheel.CanUse(targInst))
                return new ScannerWheel(targInst);
            if (ScannerBooster.CanUse(targInst))
                return new ScannerBooster(targInst);

            // Last Priority
            if (ScannerAnchor.CanUse(targInst))
                return new ScannerAnchor(targInst);
            if (ScannerNight.CanUse(targInst))
                return new ScannerNight(targInst);
            if (ScannerHealth.CanUse(targInst))
                return new ScannerHealth(targInst);
            return null;
        }
        public static void FindAllAnalyzeables(GameObject targInst, List<BlockScannerTarg> list)
        {
            // Top Priority
            if (ScannerAutominer.CanUse(targInst))
                list.Add(new ScannerAutominer(targInst));
            if (ScannerCab.CanUse(targInst))
                list.Add(new ScannerCab(targInst));

            // High Priority
            if (ScannerHolder.CanUse(targInst))
                list.Add(new ScannerHolder(targInst));
            if (ScannerTargeter.CanUse(targInst))
                list.Add(new ScannerTargeter(targInst));

            // Mid Priority
            if (ScannerModeSwitch.CanUse(targInst))
                list.Add(new ScannerModeSwitch(targInst));
            if (ScannerWeapon.CanUse(targInst))
                list.Add(new ScannerWeapon(targInst));
            if (ScannerMelee.CanUse(targInst))
                list.Add(new ScannerMelee(targInst));
            if (ScannerSilo.CanUse(targInst))
                list.Add(new ScannerSilo(targInst));

            // Low-Mid Priority
            if (ScannerBattery.CanUse(targInst))
                list.Add(new ScannerBattery(targInst));
            if (ScannerBattery2.CanUse(targInst))
                list.Add(new ScannerBattery2(targInst));
            if (ScannerFuel.CanUse(targInst))
                list.Add(new ScannerFuel(targInst));
            if (ScannerFuel2.CanUse(targInst))
                list.Add(new ScannerFuel2(targInst));

            // Low Priority
            if (ScannerAltitude.CanUse(targInst))
                list.Add(new ScannerAltitude(targInst));
            if (ScannerSpeed.CanUse(targInst))
                list.Add(new ScannerSpeed(targInst));
            if (ScannerTargeter.CanUse(targInst))
                list.Add(new ScannerTargeter(targInst));
            if (ScannerWheel.CanUse(targInst))
                list.Add(new ScannerWheel(targInst));
            if (ScannerBooster.CanUse(targInst))
                list.Add(new ScannerBooster(targInst));

            // Last Priority
            if (ScannerAnchor.CanUse(targInst))
                list.Add(new ScannerAnchor(targInst));
            if (ScannerNight.CanUse(targInst))
                list.Add(new ScannerNight(targInst));
            if (ScannerHealth.CanUse(targInst))
                list.Add(new ScannerHealth(targInst));
        }
    }
}
namespace RandomAdditions.Scanners
{
    internal abstract class ScannerTarget<T> : BlockScannerTarg where T : MonoBehaviour
    {
        protected readonly T Target;
        protected readonly TankBlock block;
        protected Tank tank => block.tank;

        protected ScannerTarget(GameObject target)
        {
            Target = target.GetComponent<T>();
            block = target.GetComponent<TankBlock>();
            if (block == null)
                throw new NullReferenceException("ModuleCircuit_Scanner.ScannerTarget<" + typeof(T) + "> encountered an invalid target that has no TankBlock!");

        }
        public virtual string GetName()
        {
            return "NULL!";
        }
        public virtual Sprite GetIcon()
        {
            return ManUI.inst.GetSprite(ObjectTypes.Block, -1);
        }
        public virtual void OnRelease()
        {
        }
        public static bool CanUse(GameObject targ)
        {
            return targ.GetComponent<T>();
        }
        public static bool CanUse(MonoBehaviour targInst)
        {
            return targInst is T;
        }

        public virtual int GetSignalInt()
        {
            return 0;
        }
        public virtual int GetSignal999()
        {
            return 0;
        }
        public virtual int GetSignalAnalog()
        {
            return 0;
        }
    }


    internal class ScannerHolder : ScannerTarget<ModuleItemHolder>
    {
        internal ScannerHolder(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Items";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.ResourceBased);
        }

        public override int GetSignalInt()
        {
            return Target.NumContents;
        }
        public override int GetSignal999()
        {
            return CircuitExt.UIntSignalFromUInt(Target.NumContents);
        }
        public override int GetSignalAnalog()
        {
            return CircuitExt.AnalogSignalFromUInt(Target.NumContents);
        }
    }
    internal class ScannerSilo : ScannerTarget<ModuleItemSilo>
    {
        internal ScannerSilo(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Items";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.ResourceBased);
        }

        public override int GetSignalInt()
        {
            return Target.GetSavedCount;
        }
        public override int GetSignal999()
        {
            return CircuitExt.UIntSignalFromUInt(Target.GetSavedCount);
        }
        public override int GetSignalAnalog()
        {
            return CircuitExt.AnalogSignalFromUInt(Target.GetSavedCount);
        }
    }
    internal class ScannerBattery : ScannerTarget<ModuleEnergyStore>
    {
        internal ScannerBattery(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Battery";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.PowerStorage);
        }

        public override int GetSignalInt()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return Mathf.RoundToInt(reg.storageTotal - reg.spareCapacity);
            }
            catch
            {
                return 0;
            }
        }
        public override int GetSignal999()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return CircuitExt.UIntSignalFromFloat1((reg.storageTotal - reg.spareCapacity) / reg.storageTotal);
            }
            catch
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return CircuitExt.AnalogSignalFromFloat1((reg.storageTotal - reg.spareCapacity) / reg.storageTotal);
            }
            catch
            {
                return 0;
            }
        }
    }
    internal class ScannerBattery2 : ScannerTarget<ModulePowerGauge>
    {
        internal ScannerBattery2(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Battery";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.PowerStorage);
        }
        
        public override int GetSignalInt()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return Mathf.RoundToInt(reg.storageTotal - reg.spareCapacity);
            }
            catch
            {
                return 0;
            }
        }
        public override int GetSignal999()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return CircuitExt.UIntSignalFromFloat1((reg.storageTotal - reg.spareCapacity) / reg.storageTotal);
            }
            catch
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            var reg = block.tank.EnergyRegulator.Energy(TechEnergy.EnergyType.Electric);
            try
            {
                return CircuitExt.AnalogSignalFromFloat1((reg.storageTotal - reg.spareCapacity) / reg.storageTotal);
            }
            catch
            {
                return 0;
            }
        }
    }
    internal class ScannerFuel : ScannerTarget<ModuleFuelTank>
    {
        internal ScannerFuel(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Fuel";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.FuelStorage);
        }

        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            try
            {
                return CircuitExt.UIntSignalFromFloat1(block.tank.Boosters.FuelLevel);
            }
            catch (Exception)
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                return CircuitExt.AnalogSignalFromFloat1(block.tank.Boosters.FuelLevel);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
    internal class ScannerFuel2 : ScannerTarget<ModuleFuelGauge>
    {
        internal ScannerFuel2(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Fuel";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.FuelStorage);
        }

        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            try
            {
                return CircuitExt.UIntSignalFromFloat1(block.tank.Boosters.FuelLevel);
            }
            catch (Exception)
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                return CircuitExt.AnalogSignalFromFloat1(block.tank.Boosters.FuelLevel);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
    internal class ScannerBooster : ScannerTarget<ModuleBooster>
    {
        internal ScannerBooster(GameObject target) : base(target) { }
        public override string GetName()
        {
            if (Target.IsRocketBooster)
                return "Booster";
            return "Propeller";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Flight);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            if (Target.IsRocketBooster)
                return Target.IsFiringBooster ? 1 : 0;
            return Target.IsFiring ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            if (Target.IsRocketBooster)
                return Target.IsFiringBooster ? 1 : 0;
            return Target.IsFiring ? 1 : 0;
        }
    }
    internal class ScannerWheel : ScannerTarget<ModuleWheels>
    {
        private float lastDrive = 0;
        internal ScannerWheel(GameObject target) : base(target) 
        {
            block.tank.control.driveControlEvent.Subscribe(UpdateSignal);
        }
        public override string GetName()
        {
            return "Drive";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Wheels);
        }

        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            try
            {
                //DebugRandAddi.Log("ScannerWheel " + Mathf.Clamp(Mathf.FloorToInt(lastDrive * 100.5f), 0, 100).ToString());
                return CircuitExt.UIntSignalFromFloat1(Mathf.Clamp(Mathf.Abs(lastDrive), 0, 1));
            }
            catch
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                //DebugRandAddi.Log("ScannerWheel " + Mathf.Clamp(Mathf.FloorToInt(lastDrive * 100.5f), 0, 100).ToString());
                return CircuitExt.AnalogSignalFromFloat1(Mathf.Clamp(lastDrive, -1, 1));
            }
            catch
            {
                return 0;
            }
        }
        public void UpdateSignal(TankControl.ControlState state)
        {
            lastDrive = state.InputMovement.z + state.Throttle.z;
        }
        public override void OnRelease()
        {
            block.tank.control.driveControlEvent.Unsubscribe(UpdateSignal);
        }
    }
    internal class ScannerSpeed : ScannerTarget<ModuleSpeedo>
    {
        internal ScannerSpeed(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Forwards Speed";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Wheels);
        }
        public override int GetSignalInt()
        {
            return Mathf.Abs(Mathf.FloorToInt(block.tank.GetForwardSpeed()));
        }
        public override int GetSignal999()
        {
            try
            {
                return CircuitExt.UIntSignalFromUInt(GetSignalInt());
            }
            catch (Exception)
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                return CircuitExt.AnalogSignalFromInt(Mathf.FloorToInt(block.tank.GetForwardSpeed()));
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
    internal class ScannerAltitude : ScannerTarget<ModuleAltimeter>
    {
        internal ScannerAltitude(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Altitude";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Accessories);
        }
        public override int GetSignalInt()
        {
            return Mathf.FloorToInt(block.tank.boundsCentreWorld.y + KickStart.TerrainLowestAlt);
        }
        public override int GetSignal999()
        {
            try
            {
                return CircuitExt.UIntSignalFromUInt(GetSignalInt());
            }
            catch (Exception)
            {
                return CircuitExt.MaxLogicRange;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                return CircuitExt.AnalogSignalFromUInt(Mathf.FloorToInt(block.tank.boundsCentreWorld.y + KickStart.TerrainLowestAlt));
            }
            catch (Exception)
            {
                return CircuitExt.MaxLogicRange;
            }
        }
    }
    internal class ScannerCab : ScannerTarget<ModuleTechController>
    {
        private int lastController = 0;
        internal ScannerCab(GameObject target) : base(target) { }
        public override string GetName()
        {
            switch (lastController)
            {
                case 1:
                    return "Player Driver";
                case 2:
                    return "A.I. Driver";
                default:
                    return "No Driver";
            }
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.AI);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            if (block.tank.PlayerFocused)
                lastController = 1;
            else if (block.tank.AI.TryGetCurrentAIType(out var AI))
                lastController = AI != AITreeType.AITypes.Idle ? 2 : 0;
            else
                lastController = 0;
            return lastController;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerAnchor : ScannerTarget<ModuleAnchor>
    {
        internal ScannerAnchor(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Anchors";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.Anchored);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            return block.tank.IsAnchored ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerNight : ScannerTarget<ModuleLight>
    {
        private bool lastNight = false;
        internal ScannerNight(GameObject target) : base(target) { }
        public override string GetName()
        {
            if (lastNight)
                return "Is Night";
            return "Is Day";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Control);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            lastNight = ManTimeOfDay.inst.NightTime;
            return lastNight ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerTargeter : ScannerTarget<ModuleRadar>
    {
        private int lastTarg = 0;
        internal ScannerTargeter(GameObject target) : base(target) { }
        public override string GetName()
        {
            switch (lastTarg)
            {
                case 1:
                    return "Targeting";
                case 2:
                    return "Locked-On";
                default:
                    return "No Targets";
            }
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.PlayerCab);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            if (tank.Weapons.GetManualTarget() != null)
                lastTarg = 2;
            lastTarg = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team) != null ? 1 : 0;
            return lastTarg;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerAutominer : ScannerTarget<ModuleItemProducer>
    {
        private bool lastMining = false;
        internal ScannerAutominer(GameObject target) : base(target) { }
        public override string GetName()
        {
            if (lastMining)
                return "Mining";
            return "Depleted";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockAttributeIcon(BlockAttributes.Mining);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            lastMining = Target.IsProducing;
            return lastMining ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerMelee : ScannerTarget<ModuleMeleeWeapon>
    {
        internal ScannerMelee(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Active";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetDamageTypeIcon(ManDamage.DamageType.Cutting);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            return Target.IsActive ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerWeapon : ScannerTarget<ModuleWeapon>
    {
        private bool lastFired = false;
        internal ScannerWeapon(GameObject target) : base(target) 
        {
            tank.Weapons.WeaponsFiredEvent.Subscribe(OnFired);
        }
        public override string GetName()
        {
            return "Firing";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Weapons);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            bool fired = lastFired;
            lastFired = false;
            return fired ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
        public void OnFired()
        {
            lastFired = true;
        }
        public override void OnRelease()
        {
            tank.Weapons.WeaponsFiredEvent.Unsubscribe(OnFired);
        }
    }
    internal class ScannerModeSwitch : ScannerTarget<ModuleModeSwitch>
    {
        internal ScannerModeSwitch(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Mode Switch";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetBlockCatIcon(BlockCategories.Weapons);
        }
        public override int GetSignalInt()
        {
            return GetSignal999();
        }
        public override int GetSignal999()
        {
            return Target.ModeSwitched ? 1 : 0;
        }
        public override int GetSignalAnalog()
        {
            return GetSignal999();
        }
    }
    internal class ScannerHealth : ScannerTarget<Damageable>
    {
        internal ScannerHealth(GameObject target) : base(target) { }
        public override string GetName()
        {
            return "Health";
        }
        public override Sprite GetIcon()
        {
            return ManUI.inst.GetDamageableTypeIcon(Target.DamageableType);
        }
        public override int GetSignalInt()
        {
            return Mathf.RoundToInt(Target.Health);
        }
        public override int GetSignal999()
        {
            try
            {
                return CircuitExt.UIntSignalFromFloat1(Target.Health / Target.MaxHealth);
            }
            catch (Exception)
            {
                return 0;
            }
        }
        public override int GetSignalAnalog()
        {
            try
            {
                return CircuitExt.AnalogSignalFromFloat1(Target.Health / Target.MaxHealth);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
