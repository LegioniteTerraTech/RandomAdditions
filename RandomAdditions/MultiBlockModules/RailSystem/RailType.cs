using System;

namespace RandomAdditions.RailSystem
{
    public enum RailType
    {
        // All trains accept any blocks in the game. Props and even normal wheels will work.
        //    Anchoring will disable the train.  These are just ideas and may or may not become a part of the mod.
        //--------------------------------------------------------------------------------------------------
        /// <summary> Extremely fast train with low capacity.  Banking turns. </summary>
        LandGauge2, // Venture One-Set-Bogie
                    //    Bogey has APs top.  Extremely difficult to de-rail.
        /// <summary> Middle-ground with everything.  Low turns.  Bogie has APs top </summary>
        LandGauge3, // GSO / Hawkeye Two-Set-Bogie
                    //    Cheap or well armored bogies effective for combat.
        /// <summary> Slow with massive weight capacity.  Low turns.  Bogie has APs top </summary>
        LandGauge4, // GeoCorp Three-Set-Bogie
                    //    5x5 top area presented by track width presents high stability.
        /// <summary> Rides an elevated beam rail determined by station positioning. </summary>
        BeamRail,   // Better Future Halo Bogie
                    //    Bogie has APs top and bottom.  Keeps at least 12 blocks off the ground.
        /// <summary> Can rotate along it's line based on the rotation of the rail nodes and the ring's own rotation. </summary>
        Revolver,   // RR Rotating Ring Bogie
                    //    Limited in customization. Very weak to attacks.
        /// <summary> Has extremely high torque and breaking capacity, but low top speed.  Can be completely vertical. </summary>
        Funicular,//  ???
        /// <summary> Transfers Centipede Trains by lending them over to other Spine Guides. </summary>
        Spines,     // Legion Spine Crawler
                    //    Does not work with couplers.
                    //    Can attack with a devastating high-knockback whip attack but will prioritize trains over attacking.
                    //    The ONLY track system with limited linking range since Spine Guides can only reach so far.
                    //    Spine Crawlers operate just like normal walker legs on terrain.
    }
}
