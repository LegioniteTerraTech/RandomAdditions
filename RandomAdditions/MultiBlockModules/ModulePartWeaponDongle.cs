using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    public enum WeaponDongleType
    {
        // GSO
        Payload,    // +Explosive Damage    -Load Time
        Propellent, // +Shell Speed         -Load Time
        // GC
        Flywheel,   // +Impact Damage       -Responsivness
        Motor,      // +Max Speed           -Load Time
        // VEN
        Shells,     // +Bullet Damage       -Alpha Strike
        Autoloader, // +Responsivness       -Accuraccy
        // HE
        Computer,   // +Homing Ability      -Max Speed
        // BF
        Amplifiers, // +Energy Damage       -Accuraccy
        // RR'
        Capacitors, // +++Fire Damage       -Power Use
        // TAC?
        EPMInjector,// +Plasma Damage       -
    }

    public class ModulePartWeaponDongle : ExtModule
    {
        public bool AlignWithController = false;
        public WeaponDongleType Type = WeaponDongleType.Payload;

        internal ModulePartWeapon assigned;
        private Transform VisualMesh;

        protected override void Pool()
        {
            VisualMesh = KickStart.HeavyObjectSearch(transform, "_MainMesh");
        }

        public override void OnAttach()
        {
            HandleSearch();
            if (assigned)
            {
                if (AlignWithController && VisualMesh)
                {
                    VisualMesh.rotation = assigned.transform.rotation;
                }
            }
        }

        public override void OnDetach()
        {
            if (AlignWithController && VisualMesh)
            {
                VisualMesh.localRotation = Quaternion.identity;
            }

            if (assigned)
            {
                assigned.attached.Remove(this);
                assigned = null;
            }
        }


        /// <summary>
        /// Gets all of the adjacent ModulePartWeaponDongles to see if any is connected and if yes, relays
        ///  this connectivity to others that aren't
        /// </summary>
        private void HandleSearch(ModulePartWeaponDongle ignore = null)
        {
            if (assigned)
                return;
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module)
                    {
                        assigned = module;
                        assigned.attached.Add(this);
                        NotifyOthersConnect();
                    }
                    else
                    {
                        var module2 = item.GetComponent<ModulePartWeaponDongle>();
                        if (module2 && module2 != ignore)
                        {
                            if (module2.assigned)
                            {
                                assigned = module2.assigned;
                                assigned.attached.Add(this);
                                NotifyOthersConnect();
                            }
                            else
                            {
                                //module2.NotifyOthersConnect();
                            }
                        }
                    }
                }
            }
        }
        private void NotifyOthersConnect()
        {
            if (assigned)
                return;
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeaponDongle>();
                    if (module && !module.assigned)
                    {
                        assigned = module.assigned;
                        assigned.attached.Add(this);
                        module.NotifyOthersConnect();
                    }
                }
            }
        }

        private bool IsConnected()
        {
            EventNoParams eventCase = new EventNoParams();
            bool success = RecurseCheckConnected(eventCase);
            eventCase.Send();
            eventCase.EnsureNoSubscribers();
            return success;
        }

        internal bool recursed = false;
        internal bool RecurseCheckConnected(EventNoParams act)
        {
            var neighboors = GetAllAttachedAPNeighboors();
            if (neighboors != null)
            {
                foreach (var item in neighboors)
                {
                    var module = item.GetComponent<ModulePartWeapon>();
                    if (module && module == assigned)
                    {
                        return true;
                    }
                    else
                    {
                        var module2 = item.GetComponent<ModulePartWeaponDongle>();
                        if (module2 && !recursed)
                        {
                            module2.recursed = true;
                            act.Subscribe(module2.EndRecurse);
                            if (module2.RecurseCheckConnected(act))
                                return true;
                        }
                    }
                }
            }
            return false;
        }
        internal void EndRecurse()
        {
            recursed = false;
        }
    }
}
