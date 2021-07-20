using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions.AI
{
    class AIEnhancedCore
    {
        //Deprecated class, kept for cleanup reasons
        public enum DediAIType
        {   //like the old plans, we make the AI do stuff
            // COMBAT
            Escort,     // Good ol' player defender                     (Classic player defense numbnut)
            Assault,    // Run off and attack the enemies on your radar (Runs off (beyond radar range!) to attack enemies)
            Aegis,      // Protects the nearest non-player allied Tech  (Follows nearest ally, will chase enemy some distance)

            // RESOURCES
            Prospector, // Harvest Chunks and return them to base       (Returns chunks when full to nearest receiver)
            Scrapper,   // Grab loose blocks but avoid combat           (Return to nearest base when threatened)
            Energizer,  // Charges up and/or heals other techs          (Return to nearest base when out of power)

            // MISC        (MultiTech) - BuildBeam disabled, will fire at any angle.
            MTTurret,   // Only turns to aim at enemy                   
            MTSlave,    // Does not move on own but does shoot back     
            MTMimic,    // Copies the actions of the closest non-MT Tech in relation     

            // ADVANCED    (REQUIRES TOUGHER ENEMIES TO USE!)           (can't just do the same without the enemies attacking these ways as well...)
            Aviator,    // Flies aircraft, death from above, nuff said  (Flies above ground, by the player and keeps distance) [unload distance will break!]
            Buccaneer,  // Sails ships amongst ye seas~                 (Avoids terrain above water level)
            Astrotech,  // Flies hoverships and kicks Tech              (Follows player a certain distance above ground level and can follow into the sky)
        }
        public class ModuleAIExtension : Module
        {
            //Deprecated module, kept for cleanup reasons
            TankBlock TankBlock;

            public void OnPool()
            {
                TankBlock = gameObject.GetComponent<TankBlock>();
                TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
                TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
            }
            public void OnAttach()
            {
                TankBlock.serializeEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                TankBlock.serializeTextEvent.Subscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }
            public void OnDetach()
            {
                TankBlock.serializeEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
                TankBlock.serializeTextEvent.Unsubscribe(new Action<bool, TankPreset.BlockSpec>(OnSerialize));
            }

            [Serializable]
            private new class SerialData : SerialData<SerialData>
            {
                public DediAIType savedMode;
            }

            private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
            {
                if (saving)
                {   //Save to snap
                    //SAVE NOTHING!  OG code is below
                    /*
                    SerialData serialData = new SerialData()
                    {
                        savedMode = TankBlock.transform.root.GetComponent<AI.AIECore.TankAIHelper>().DediAI
                    };
                    serialData.Store(blockSpec.saveState);
                    Debug.Log("TACtical AI: Saved " + SavedAI.ToString() + " in gameObject " + gameObject.name);
                     */
                }
                else
                {   //Load from snap
                    try
                    {
                        SerialData serialData2 = SerialData<SerialData>.Retrieve(blockSpec.saveState);
                        if (serialData2 != null)
                        {
                            Debug.Log("TACtical AI: CLEANING UP OLD CODE!!!");
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
