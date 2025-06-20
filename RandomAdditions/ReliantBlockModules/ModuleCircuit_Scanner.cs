using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;

namespace RandomAdditions
{
    public enum ScannerModeSet : int 
    {
        Int = 0,
        Int999 = 1,
        Analog = 2
    }
    /// <summary>
    /// Scans blocks (of the SAME TECH) positioned in front of it on it's FIRST AP.
    ///   First AP is the scanner.
    /// </summary>
    [RequireComponent(typeof(ModuleCircuitNode))]
    [AutoSaveComponent]
    public class ModuleCircuit_Scanner : ExtModule, ICircuitDispensor
    {
        public bool OnlyGetBest = false;
        [SSaveField]
        private int ScannerMode = 0;
        [SSaveField]
        private int SelectedAnalyzer = 0;
        private int ActiveAnalyzer = 0;

        // Logic
        private bool LogicConnected = false;

        private int OutputThisFrame = 0;
        private TankBlock targBlock = null;
        private readonly List<BlockScannerTarg> Analyzers = new List<BlockScannerTarg>();
        private BlockScannerTarg Analyzer = null;
        private MeshRenderer AnalyzerIcon = null;
        protected ModuleUIButtons buttonGUI;

        protected override void Pool()
        {
            try
            {
                AnalyzerIcon = KickStart.HeavyTransformSearch(transform, "_scannerIcon").GetComponent<MeshRenderer>();
            }
            catch { }
            if (AnalyzerIcon == null)
            {
                block.damage.SelfDestruct(0.1f);
                BlockDebug.ThrowWarning(true, "ModuleCircuit_Analyzer needs a valid GameObject in hiearchy called \"_scannerIcon\" with a vaild Mesh!");
                return;
            }
            InsureGUI();
        }
        private static LocExtStringMod LOC_SetScannerMode = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Set Scanner Mode"},
            { LocalisationEnums.Languages.Japanese, "スキャナーモードの選択"},
        });
        private static LocExtStringMod LOC_OutputMode = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Output Mode"},
            { LocalisationEnums.Languages.Japanese, "出力モードの選択"},
        });
        public void InsureGUI()
        {
            if (!OnlyGetBest && buttonGUI == null)
            {
                buttonGUI = ModuleUIButtons.AddInsure(gameObject, "Scanner", true);
                buttonGUI.AddElement(LOC_SetScannerMode, RequestSet, GetIcon, GetDesc);
                buttonGUI.AddElement(LOC_OutputMode, RequestAnalog, GetIconAnalog, GetDescAnalog);
                buttonGUI.OnGUIOpenAttemptEvent.Subscribe(OnGUIOpen);
            }
        }
        private void OnGUIOpen()
        {
            if (ActiveAnalyzer == -1 || Analyzers.Count == 0)
                buttonGUI.DenyShow();
        }
        private string GetDesc()
        {
            return Analyzer != null ? Analyzer.GetName() : "None!";
        }
        private Sprite GetIcon()
        {
            return UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");//ManUI.inst.GetBlockCatIcon(BlockCategories.Control);
        }
        private Sprite GetIconAnalog()
        {
            return ScannerMode == 0 ? UIHelpersExt.GetGUIIcon("GUI_Reset") : UIHelpersExt.GetGUIIcon("GUI_Power");
        }
        private float RequestSet(float val)
        {
            if (!float.IsNaN(val))
            {
                SelectedAnalyzer = Mathf.RoundToInt(val * (Analyzers.Count - 1));
                SetupAnalyzer();
            }
            return Mathf.Clamp01((float)SelectedAnalyzer / Enum.GetValues(typeof(ScannerModeSet)).Length);
        }
        private static LocExtStringMod LOC_OutputERROR = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "ERROR"},
            { LocalisationEnums.Languages.Japanese, "壊す"},
        });
        private string GetDescAnalog()
        {
            switch ((ScannerModeSet)ScannerMode)
            {
                case ScannerModeSet.Int:
                    return "Full Int";
                case ScannerModeSet.Int999:
                    return "999";
                case ScannerModeSet.Analog:
                    return "Signed";
                default:
                    return LOC_OutputERROR;
            }
        }
        private float RequestAnalog(float val)
        {
            if (!float.IsNaN(val))
            {
                ScannerMode = Mathf.RoundToInt(val * (Analyzers.Count - 1));
            }
            return Mathf.Clamp01(ScannerMode / 3);
        }

        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleRailEngine");
            if (CircuitExt.LogicEnabled)
            {
                LogicConnected = true;
                Circuits.PreSlowUpdate.Subscribe(UpdateScan);
                block.NeighbourAttachedEvent.Subscribe(OnNeighboorAttach);
                block.NeighbourDetachedEvent.Subscribe(OnNeighboorDetached);
                block.NeighbourDetachingEvent.Subscribe(OnNeighboorDetach);
                InsureGUI();
            }
            UpdateAnalyzers();
            block.serializeEvent.Subscribe(OnSerialize);
            enabled = true;
        }
        public override void OnDetach()
        {
            enabled = false;
            block.serializeEvent.Unsubscribe(OnSerialize);
            ReleaseAnalyzers();
            if (LogicConnected)
            {
                block.NeighbourDetachingEvent.Unsubscribe(OnNeighboorDetach);
                block.NeighbourDetachedEvent.Unsubscribe(OnNeighboorDetached);
                block.NeighbourAttachedEvent.Unsubscribe(OnNeighboorAttach);
                Circuits.PreSlowUpdate.Unsubscribe(UpdateScan);
            }
            LogicConnected = false;
        }

        public void OnNeighboorAttach(TankBlock block)
        {
            UpdateAnalyzers();
        }
        public void OnNeighboorDetach(TankBlock block)
        {
            ReleaseAnalyzers();
        }
        public void OnNeighboorDetached(TankBlock block)
        {
            UpdateAnalyzers();
        }

        public void UpdateAnalyzers()
        {
            var scanBlock = block.ConnectedBlocksByAP[0];
            if (scanBlock)
            {
                if (targBlock != scanBlock)
                {
                    if (Analyzers.Count > 0)
                        ReleaseAnalyzers();
                    targBlock = scanBlock;
                    if (OnlyGetBest)
                        Analyzers.Add(BlockScannerUtil.FindBestScanner(scanBlock.gameObject));
                    else
                        BlockScannerUtil.FindAllAnalyzeables(scanBlock.gameObject, Analyzers);
                    SetupAnalyzer();
                }
            }
            else if (Analyzers.Count > 0)
                ReleaseAnalyzers();
        }

        public void SetupAnalyzer()
        {
            ActiveAnalyzer = Mathf.Clamp(SelectedAnalyzer, -1, Analyzers.Count -1);
            if (ActiveAnalyzer != -1)
            {
                Analyzer = Analyzers[ActiveAnalyzer];
                if (AnalyzerIcon && Analyzer != null)
                    SetIconTextureFromSprite(Analyzer.GetIcon());
            }
            else
            {
                Analyzer = null;
                if (AnalyzerIcon)
                    SetIconTextureFromSprite(ManUI.inst.GetSprite(ObjectTypes.Block, -1));
            }
        }
        public void ReleaseAnalyzers()
        {
            Analyzer = null;
            foreach (var AnalyzerC in Analyzers)
            {
                AnalyzerC.OnRelease();
            }
            Analyzers.Clear();
            if (AnalyzerIcon)
                SetIconTextureFromSprite(ManUI.inst.GetSprite(ObjectTypes.Block, -1));
            targBlock = null;
        }
        public void SetIconTextureFromSprite(Sprite spr)
        {
            Texture2D tex = new Texture2D(Mathf.RoundToInt(spr.textureRect.width),Mathf.RoundToInt(spr.textureRect.height), 
                spr.texture.format, false);
            Graphics.CopyTexture(spr.texture, 0, 0, Mathf.RoundToInt(spr.textureRect.x), Mathf.RoundToInt(spr.textureRect.y),
                Mathf.RoundToInt(spr.textureRect.width), Mathf.RoundToInt(spr.textureRect.height), tex, 0,0,0,0);
            //tex.Apply(false, false);
            AnalyzerIcon.material.SetTexture("_MainTex", tex);
        }

        public void UpdateScan()
        {
            if (Analyzer == null)
                OutputThisFrame = 0;
            else
            {
                switch ((ScannerModeSet)ScannerMode)
                {
                    case ScannerModeSet.Int:
                        OutputThisFrame = Analyzer.GetSignalInt();
                        break;
                    case ScannerModeSet.Int999:
                        OutputThisFrame = Analyzer.GetSignal999();
                        break;
                    case ScannerModeSet.Analog:
                        OutputThisFrame = Analyzer.GetSignalAnalog();
                        break;
                }
            }
        }
        public int GetDispensableCharge()
        {
            if (CircuitExt.LogicEnabled)
                return OutputThisFrame;
            return 0;
        }

        /// <summary>
        /// Directional!
        /// </summary>
        public int GetDispensableCharge(Vector3 APOut)
        {
            if (CircuitExt.LogicEnabled)
                return OutputThisFrame;
            return 0;
        }

        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            try
            {
                if (saving)
                {
                    this.SerializeToSafe();
                }
                else
                {
                    this.DeserializeFromSafe();
                }
            }
            catch { }
        }
    }
}
