using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TerraTechETCUtil;
using UnityEngine;
using static TerraTechETCUtil.ManIngameWiki;

namespace RandomAdditions
{
    internal class TonyRailsWiki
    {
        private static string modID => "Tony Rails";
        private static Sprite nullSprite;
        private static Sprite RailSprite;

        internal static void InitWiki()
        {
            if (nullSprite != null)
                return;
            nullSprite = ManUI.inst.GetSprite(ObjectTypes.Block, -1);
            ModContainer MC = ManMods.inst.FindMod("Random Additions");
            RailSprite = UIHelpersExt.GetIconFromBundle(MC, "GUI_Connect");
            InitMechanics();
        }

        internal static void InitMechanics()
        {
            new WikiPageInfo(modID, "Summary", RailSprite, PageTracks);
        }
        internal static void PageTracks()
        {
            GUILayout.BeginHorizontal();
            AltUI.Sprite(RailSprite, AltUI.TextfieldBorderedBlue, GUILayout.Height(64), GUILayout.Width(64));
            GUILayout.Label("Tony Rails", AltUI.LabelBlackTitle);
            GUILayout.EndHorizontal();
            GUILayout.Label("Tony Rails is an expansive mod that aims to bring a fully-operational train system to TerraTech.", AltUI.LabelBlack);

        }
    }
}
