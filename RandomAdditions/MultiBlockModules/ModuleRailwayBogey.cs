using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    // Might come in 2024, we'll see
    // Connects a Tech to the rails
    public class ModuleRailwayBogey : ExtModule
    {
        public ManRails.RailSegment curRail;
        public int CurrentRail = 0;

        /// <summary>
        /// Force the controller to float over the rails
        /// </summary>
        private void FixedUpdate()
        {
            if (curRail)
            { 
            }
        }
    }
}
