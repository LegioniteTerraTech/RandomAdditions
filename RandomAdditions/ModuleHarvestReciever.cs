﻿using System;

namespace RandomAdditions
{
    public class ModuleHarvestReciever : Module
    {
        TankBlock TankBlock;
        // Returns the position of itself in the world as a point the AI can pathfind to

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.AttachEvent.Subscribe(new Action(OnAttach));
            TankBlock.DetachEvent.Subscribe(new Action(OnDetach));
        }
        public void OnAttach()
        {
            var thisInst = gameObject.GetComponent<ModuleHarvestReciever>();
            AI.AIEnhancedCore.Depots.Add(thisInst.transform);
        }
        public void OnDetach()
        {
            var thisInst = gameObject.GetComponent<ModuleHarvestReciever>();
            AI.AIEnhancedCore.Depots.Remove(thisInst.transform);
        }
    }
}
