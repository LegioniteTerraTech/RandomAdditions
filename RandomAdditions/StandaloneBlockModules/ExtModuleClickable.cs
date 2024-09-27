using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using SafeSaves;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    public abstract class ExtModuleClickable : ExtModule, ManPointer.OpenMenuEventConsumer
    {
        private static FieldInfo cont = typeof(TankBlock).GetField("m_ContextMenuType", BindingFlags.NonPublic | BindingFlags.Instance);

        private static ExtModuleClickable openMouseStartTarg = null;
        private static Vector2 openMouseStart = Vector2.zero;
        private static float openMouseTime = 0;

        public static void OnClick(bool RMB, bool down, RaycastHit rayman)
        {
            if (!RMB)
                return;
            if (down && rayman.collider)
            {
                var vis = ManVisible.inst.FindVisible(rayman.collider);
                if (vis)
                {
                    var targVis = vis.block;
                    if (targVis)
                    {
                        var EMC = targVis.GetComponent<ExtModuleClickable>();
                        if (EMC && EMC.UseDefault)
                            EMC.ShowDelayedNoCheck();
                    }
                }
            }
        }

        public bool UseClick = true;
        protected bool Pooled = false;
        public abstract bool UseDefault { get; }

        protected virtual void PoolInsure()
        {
            if (Pooled)
                return;
            Pooled = true;
            block.TrySetBlockFlag(TankBlock.Flags.HasContextMenu, true);
            DebugRandAddi.Info("PoolInsure() Has HasContextMenu value: " + block.HasContextMenu);
            block.m_ContextMenuForPlayerTechOnly = false;
            cont.SetValue(block, UIHelpersExt.customElement);
        }

        public bool CanOpenMenu(bool radial) => tank != null && ManPlayer.inst.PlayerTeam == tank.Team;
        /// <summary>
        /// Impossible to figure out why it's soo slow - OnOpenMenuEvent is delayed for non-native DLLs and I can't find any reason for it to do so.
        /// </summary>
        /// <param name="OMED"></param>
        /// <returns></returns>
        public bool OnOpenMenuEvent(OpenMenuEventData OMED)
        {
            if (OMED.m_AllowRadialMenu)
            {
                if (UseClick)
                    ShowDelayedNoCheck();
                return true;
            }
            else
                return false;
        }
        public void ShowDelayedNoCheck()
        {
            if (openMouseStartTarg == null)
            {
                openMouseStartTarg = this;
                openMouseStart = ManHUD.inst.GetMousePositionOnScreen();
                openMouseTime = Time.time + UIHelpersExt.ROROpenTimeDelay;
                DebugRandAddi.Info("ShowDelayedNoCheck() - " + Time.time);
                //ManSFX.inst.PlayUISFX(ManSFX.UISfxType.LockOn);
            }
        }
        internal static void UpdateThis()
        {
            if (openMouseStartTarg != null && Time.time > openMouseTime)
            {
                if ((openMouseStart - ManHUD.inst.GetMousePositionOnScreen()).sqrMagnitude < UIHelpersExt.ROROpenAllowedMouseDeltaSqr
                && ManInput.inst.GetRadialInputController(ManInput.RadialInputController.Mouse).IsSelecting())
                    openMouseStartTarg.OnShow();
                openMouseStartTarg = null;
                //DebugRandAddi.Log("QueueDelayedOpen(end) - " + Time.time);
            }
        }
        public abstract void OnShow();
    }
}
