using System;

namespace RandomAdditions.RailSystem
{
    public enum RailSpace : byte
    {
        /// <summary> Rail that is fixed to the world </summary>
        World,
        /// <summary> Rail that is fixed to the world but floating in the air </summary>
        WorldFloat,
        /// <summary> Rail that is in the world but sharply angled </summary>
        WorldAngled,
        /// <summary> Rail that connects between World and Local</summary>
        LocalUnstable,
        /// <summary> Rail that is directly mounted to a block/Tech </summary>
        Local,
        /// <summary> Rail that is directly mounted to a block/Tech and sharply angled </summary>
        LocalAngled,
    }
}
