﻿using UnityEngine;
using TerraTechETCUtil;

public class ModuleReinforced : RandomAdditions.ModuleReinforced { };
namespace RandomAdditions
{
    public class ModuleReinforced : MonoBehaviour, IInvokeGrabbable
    {
        /* Throw this within your JSONBLOCK
        "RandomAdditions.ModuleReinforced":{ // Add a special resistance to your block
            "CustomDamagableName": "Blast Plating", // The name of the damagable to display in various info in-game
            "CustomDamagableIcon": 0,          // DamageableType icon to use

            // The way damage is handled:
            // Normal applied damage value -> AoE Damage Multiplier -> Custom Damage Multiplier
            "DoDamagableSwitch": false, // Should we switch the DamageableType of this block?
            "TypeToSwitch": 0,          // DamageableType to switch to

            "ModifyAoEDamage": false,   // Enable AoE damage changing? (only handles Explosion, not shotguns)
            "DenyExplosion": false,     // Stop explosions from spreading on contact?
            "ExplosionMultiplier": 1,   // Multiplier for all AoE attacks dealt against this block

            "UseMultipliers": true,     // Should the multipliers below be used?
            //----- DamagableMultipliers -----
                "Standard":     1,      // Standard multiplier
                "Bullet":       1,      // Bullet multiplier
                "Energy":       1,      // Energy multiplier
                "Explosive":    1,      // Explosive multiplier
                "Impact":       1,      // Impact multiplier
                "Fire":         1,      // Fire multiplier
                "Cutting":      1,      // Cutting multiplier
                "Plasma":       1,      // Plasma multiplier
            //--------------------------------
        },
        */
        public string CustomDamagableName = null;
        public ManDamage.DamageableType CustomDamagableIcon = ManDamage.DamageableType.Standard;

        public bool DoDamagableSwitch = false;
        public ManDamage.DamageableType TypeToSwitch = ManDamage.DamageableType.Standard;

        public bool ModifyAoEDamage = false;
        public float ExplosionMultiplier = 1.0f;

        public bool DenyExplosion = false;
        public bool UseMultipliers = true;

            public float Standard = 1.0f;
            public float Bullet = 1.0f;
            public float Energy = 1.0f;
            public float Explosive = 1.0f;
            public float Impact = 1.0f;
            public float Fire = 1.0f;
            public float Cutting = 1.0f;
            public float Plasma = 1.0f;

        public void OnPool()
        {
            try
            {
                if (DenyExplosion)
                    gameObject.GetComponent<TankBlock>().SubToBlockAttachConnected(OnAttach, null);
            }
            catch { }
        }
        private static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleReinforced",
            "This block is " + AltUI.HighlightString("reinforced") + " and has special resistance properties.");
        public void OnGrabbed()
        {
            hint.Show();
        }
        private void OnAttach()
        {
            gameObject.GetComponent<TankBlock>().GetComponent<TankBlock>().UnSubToBlockAttachConnected(OnAttach, null);
        }

        private ManDamage.DamageInfo newDMG;
        public ManDamage.DamageInfo RecalcDamage(ref ManDamage.DamageInfo info)
        {
            newDMG = info;// Clone it here
            if ((bool)info.Source)
            {
                if (ModifyAoEDamage && info.Source.GetComponent<Explosion>())
                {
                    newDMG.ApplyDamageMultiplier(ExplosionMultiplier);
                }
            }
            if (UseMultipliers)
            {
                switch (newDMG.DamageType)
                {
                    case ManDamage.DamageType.Standard:
                        newDMG.ApplyDamageMultiplier(Standard);
                        break;
                    case (ManDamage.DamageType)1:
                        newDMG.ApplyDamageMultiplier(Bullet);
                        break;
                    case ManDamage.DamageType.Energy:
                        newDMG.ApplyDamageMultiplier(Energy);
                        break;
                    case (ManDamage.DamageType)3:// Blast
                        newDMG.ApplyDamageMultiplier(Explosive);
                        break;
                    case ManDamage.DamageType.Impact:
                        newDMG.ApplyDamageMultiplier(Impact);
                        break;
                    case ManDamage.DamageType.Cutting:
                        newDMG.ApplyDamageMultiplier(Cutting);
                        break;
                    case ManDamage.DamageType.Fire:
                        newDMG.ApplyDamageMultiplier(Fire);
                        break;
                    case ManDamage.DamageType.Plasma:
                        newDMG.ApplyDamageMultiplier(Plasma);
                        break;
                }
            }
            return newDMG;
        }



        // On request of Rafs (and likely every other JSON modder out there), this has been made obsolete to allow full customization
        /* Throw this within your JSONBLOCK
        "RandomAdditions.ModuleReinforced":{ // Add a special resistance to your block
            "TypeToResist": 0,          // The special resistance the block should have, DamageType
            "ResistMultiplier": 1.0,    // Multiplier for the damage
            "IsResistedProof": false,   // Does this block take no damage from the resisted type?
            //-------------------------------------------------------------------------------------
            "DoDamagableSwitch": false, // Should we switch the DamageableType of this block?
            "TypeToSwitch": 0,          // DamageableType to switch to
            //-------------------------------------------------------------------------------------
            "DoDamagablePenalty": false,// Should we nerf the other stats? (cannot buff using this)
            "PenaltyMultiplier": 1.0,   // Multiplier for the damage taken (will not accept below 1.0)
        },

        public ManDamage.DamageType TypeToResist = ManDamage.DamageType.Standard;
        public bool IsResistedProof = false;
        public float ResistMultiplier = 0.5f;
        public bool DoDamagableSwitch = false;
        public ManDamage.DamageableType TypeToSwitch = ManDamage.DamageableType.Standard;
        public bool DoDamagablePenalty = false;
        public float PenaltyMultiplier = 0.5f;
        */
    }
}
