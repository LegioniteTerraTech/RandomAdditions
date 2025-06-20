using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LocalisationEnums;
using TerraTechETCUtil;
using static TerraTechETCUtil.ManIngameWiki;
using UnityEngine;

namespace RandomAdditions
{
    internal class RandAddiExtendWiki
    {
        public const string Venom = "☣VΣИΩM☣";
        public static LocExtStringMod LOC_Advanced = new LocExtStringMod(new Dictionary<Languages, string>
            {{ Languages.US_English, "Advanced" },
            {Languages.Japanese, "高度な概念" }});
        public static LocExtStringMod LOC_Advanced_desc = new LocExtStringMod(new Dictionary<Languages, string>
            {{ Languages.US_English, "Advanced Summary" },
            {Languages.Japanese, "高度な概念の概要" }});
        public static LocExtStringMod LOC_MultiTechs = new LocExtStringMod(new Dictionary<Languages, string>
            {{ Languages.US_English, "Multi-Techs" },
            {Languages.Japanese, "マルチテック" }});
        public static LocExtStringMod LOC_MultiTechs_desc = new LocExtStringMod(new Dictionary<Languages, string>
            {{ Languages.US_English, "Multi-Techs Summary" },
            {Languages.Japanese, "マルチテックの概要" }});
        internal static void InitWiki()
        {
            ExtendedWiki.OnExtendWikiCall.Subscribe(AutoPopulateWikiExtras);
        }
        internal static void DeInit()
        {
            ExtendedWiki.OnExtendWikiCall.Unsubscribe(AutoPopulateWikiExtras);
        }
        internal static void AutoPopulateWikiExtras(Wiki wiki)
        {
            WikiPageGroup WPGAdvanced = new WikiPageGroup(wiki.ModID, LOC_Advanced);
            var advMain = new WikiPageInfo(wiki.ModID, LOC_Advanced_desc, null, DisplayAdvMain, WPGAdvanced);
            WPGAdvanced.onOpen = () => { advMain.GoHere(); };
            AutoPopulateWikiAdvanced(wiki, WPGAdvanced);
            AutoPopulateWikiMultiTechs(wiki, WPGAdvanced);
        }

        private static void DisplayAdvMain()
        {
            GUILayout.Label("This section goes over advanced concepts in Terra Tech!", AltUI.LabelBlackTitle);
            GUILayout.Label("Terra Tech's physics is very precise compared to other vehicle building games.", AltUI.LabelBlack);
            GUILayout.Label("Two vehicles can interact with each other with high consistancy.", AltUI.LabelBlack);
            GUILayout.Label("There's an advanced technique called \"Multi-Teching\" using this mechanic.", AltUI.LabelBlack);
        }

        private static void AutoPopulateWikiAdvanced(Wiki wiki, WikiPageGroup group)
        {
        }







        private static void AutoPopulateWikiMultiTechs(Wiki wiki, WikiPageGroup group)
        {
            WikiPageGroup WPGMultiTechs = new WikiPageGroup(wiki.ModID, LOC_MultiTechs, null, group);
            var MTMain = new WikiPageInfo(wiki.ModID, LOC_MultiTechs_desc, null, DisplayMTMain, WPGMultiTechs);
            WPGMultiTechs.onOpen = () => { MTMain.GoHere(); };
            new WikiPageInfo(wiki.ModID, "Tread-Tech", null, DisplayTreadTech, WPGMultiTechs);
            new WikiPageInfo(wiki.ModID, "Axial Stabilizer", null, DisplayAxialStabilizer, WPGMultiTechs);
            new WikiPageInfo(wiki.ModID, "HaVCS", null, DisplayHaVCS, WPGMultiTechs);
            new WikiPageInfo(wiki.ModID, "AGALS", null, DisplayAGALS, WPGMultiTechs);
            new WikiPageInfo(wiki.ModID, "YWVS", null, DisplayYWVS, WPGMultiTechs);
        }
        private static void DisplayMTMain()
        {
            GUILayout.Label(LOC_MultiTechs + " [MT]", AltUI.LabelBlackTitle);
            GUILayout.Label("The art of two or more Techs working in inter-connected harmony to work as an advanced mechanism.", AltUI.LabelBlack);
            GUILayout.Label("a lost art of ancient kingdoms. Many modern prospectors have studied it extensively, " +
                "yet none have come close to anything other than imperfect replicas, often coined as \"Modern Multi-Techs.\"", AltUI.LabelBlack);
            GUILayout.Label("The first recorded use was by ReaperX1, ", AltUI.LabelBlack);
            GUILayout.Label("The largest MT ever recorded was made by use thus far was made by " + Venom + "!", AltUI.LabelBlack);
            GUILayout.Label("Additionally there's two time periods - During " + Venom + " and After " + Venom + ", DV and AV respectively.", AltUI.LabelBlack);
        }
        private static void DisplayTreadTech()
        {
            GUILayout.Label("Tread-Tech", AltUI.LabelBlackTitle);
            GUILayout.Label("DV - During " + Venom, AltUI.LabelGold);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tread-Tech is the broadspread use of compressed ", AltUI.LabelBlack);
            if (GUILayout.Button("GSO Techa Treads.", AltUI.LabelBlue))
                GetBlockPage(StringLookup.GetItemName(ObjectTypes.Block, (int)BlockTypes.GSOTrack_112)).GoHere();
            GUILayout.EndHorizontal();
            GUILayout.Label("to make an excessively powerful movement force!", AltUI.LabelBlack);
            GUILayout.Label("It is a staple technology of most radical methods of Multi-Teching.", AltUI.LabelBlack);
        }
        private static void DisplayAxialStabilizer()
        {
            GUILayout.Label("Axial Stabilizer", AltUI.LabelBlackTitle);
            GUILayout.Label("DV - During " + Venom, AltUI.LabelGold);
            GUILayout.Label("The Axial Stabilizer relies on the power of both gyroscopes and Tread Tech to apply an uprighting force.", AltUI.LabelBlack);
            GUILayout.Label("It's main use was to balance things that were too heavy for gyros to handle.  Many old generations of large walkers utilised this.", AltUI.LabelBlack);
        }
        private static void DisplayAGALS()
        {
            GUILayout.Label("Anti-Gravity Atmospheric Lift System [AGALS]", AltUI.LabelBlackTitle);
            GUILayout.Label("DV - During " + Venom, AltUI.LabelGold);
            GUILayout.Label("The AGALS is a high-load flight method with practically unlimited weight tolerence.", AltUI.LabelBlack);
            GUILayout.Label("It has a powerful horizontal braking force above the ground!", AltUI.LabelBlack);
            GUILayout.Label("There is no drifting with this design, with stability superior to the Better Future Stabiliser Computer.", AltUI.LabelBlack);
        }
        private static void DisplayHaVCS()
        {
            GUILayout.Label("Horizontal and Vertical Control System [HaVCS]", AltUI.LabelBlackTitle);
            GUILayout.Label("DV - During " + Venom, AltUI.LabelGold);
            GUILayout.Label("The HaVCS is the next step of the AGALS which acts as a versitile flight method that can be controlled not only horizontally, but vertically too!", AltUI.LabelBlack);
            GUILayout.Label("It is succeeded by the YWVS system, which is far easier to control.", AltUI.LabelBlack);
            GUILayout.Label("It can apply braking forces in all directions above the ground!", AltUI.LabelBlack);
            GUILayout.Label("There is reduced drifting with this design, keeping the benefits of the AGALS, but with some vertical braking forces.", AltUI.LabelBlack);
        }
        private static void DisplayYWVS()
        {
            GUILayout.Label("Yuki Wheel Vertical-Stabilizer [YWVS]", AltUI.LabelBlackTitle);
            GUILayout.Label("DV - During " + Venom, AltUI.LabelGold);
            GUILayout.Label("Named after forum user \"Yuki\", this is an experimental adaptation of their resource-based inertia dampener system, which this uses wheels instead.", AltUI.LabelBlack);
            GUILayout.Label("It is extremely versatile and is commonly used in most forms of spaceships.", AltUI.LabelBlack);
            GUILayout.Label("It can apply braking forces in all directions above the ground!", AltUI.LabelBlack);
            GUILayout.Label("There remains a subtle drift with all existing iterations of this design.  " +
                "Legends speak of the next generation - a system so perfectly stable it never drifts at all!", AltUI.LabelBlack);
        }
    }
}
