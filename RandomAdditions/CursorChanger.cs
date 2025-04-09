using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    public class CursorChanger : MonoBehaviour
    {
        // static FieldInfo existingCursors = typeof(MousePointer).GetField("m_CursorDataSets", BindingFlags.NonPublic | BindingFlags.Instance);

        /*
            // NEW
            PlaceTracks   0
        */
        public static CursorChangeHelper.CursorChangeCache Cache;
        public static bool AddedNewCursors = false;
        public static CursorChangeHelper.CursorChangeCache CursorIndexCache => Cache.CursorIndexCache;

        public static void AddNewCursors()
        {
            if (AddedNewCursors)
                return;
            if (ResourcesHelper.TryGetModContainer(KickStart.ModID, out ModContainer MC))
            {
                Cache = CursorChangeHelper.GetCursorChangeCache((new DirectoryInfo(MC.AssetBundlePath)).Parent.ToString(), "RA_Icons", MC,
                    new KeyValuePair<string, bool>("TrackBuilder", false)
                    );
            }
            else
                DebugRandAddi.Assert(true, "CursorChanger: AddNewCursors - Could not find ModContainer for " + KickStart.ModID + "!");

            AddedNewCursors = true;
        }
    }
}
