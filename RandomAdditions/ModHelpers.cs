using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// This class uses parts from the Official Mod Tool to render NuterraSteam blocks ingame.
    /// </summary>
    public class ModHelpers : MonoBehaviour
    {   
        // Startup
        private static ModHelpers inst;
        private static bool hooked = false;
        /// <summary>
        /// True (Right, Left) False,
        /// True   (Down, Up)  False
        /// </summary>
        public static Event<bool, bool> ClickNoCheckEvent = new Event<bool, bool>();
        public static bool MouseLeftDown => mouseLeftDown;
        private static bool mouseLeftDown = false;
        public static bool MouseRightDown => mouseRightDown;
        private static bool mouseRightDown = false;
        public static void Initiate()
        {
            if (hooked)
                return;
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            inst = new GameObject("LazyRender").AddComponent<ModHelpers>();
            inst.enabled = true;
            hooked = true;
        }
        public static void UpdateThis()
        {
            bool lD = Input.GetMouseButton(0);
            if (lD != mouseLeftDown)
            {
                mouseLeftDown = lD;
                if (lD)
                    ClickNoCheckEvent.Send(false, true);
                else
                    ClickNoCheckEvent.Send(false, false);
            }
            bool RD = Input.GetMouseButton(1);
            if (RD != mouseRightDown)
            {
                mouseRightDown = RD;
                if (RD)
                    ClickNoCheckEvent.Send(true, true);
                else
                    ClickNoCheckEvent.Send(true, false);
            }
        }


        // Events
        private static bool allowQuickSnap = true;
        private static bool cooldown = false;
        private static TankBlock target;
        public static void OnClick(ManPointer.Event mEvent, bool DOWN, bool yes2)
        {
            //DebugRandAddi.Log("OnClick() - " + mEvent.ToString() + " | " + DOWN + " | " + Time.time);
            Visible targVis = Singleton.Manager<ManPointer>.inst.targetVisible;
            if (targVis && targVis.block)
            {
                if (DOWN && mEvent == ManPointer.Event.RMB)
                {
                    TankBlock TB = targVis.block;
                    if (TB.GetComponent<ModulePartWeapon>())
                    {
                        TB.GetComponent<ModulePartWeapon>().HighlightEntireWeapon(true);
                    }
                    else if (TB.GetComponent<ModulePartWeaponBarrel>())
                    {
                        var MPWB = TB.GetComponent<ModulePartWeaponBarrel>();
                        if (MPWB.AssignedMPW)
                            MPWB.AssignedMPW.HighlightEntireWeapon(true);
                    }
                    else if (TB.GetComponent<ModulePartWeaponDongle>())
                    {
                        var MPWD = TB.GetComponent<ModulePartWeaponDongle>();
                        if (MPWD.AssignedMPW)
                            MPWD.AssignedMPW.HighlightEntireWeapon(true);
                    }
                    else if (Input.GetKey(KeyCode.LeftShift) && TB.GetComponent<ModuleCircuit_Display_Text>())
                    {
                        var MCN = TB.CircuitNode;
                        if (MCN && MCN.Receiver)
                        {
                            var WP = WorldPosition.FromScenePosition(TB.trans.position);
                            var charge = MCN.Receiver.CurrentChargeData;
                            if (charge != null)
                            {
                                if (charge.ChargeStrength == int.MinValue)
                                    RailSystem.ManTrainPathing.TrainStatusPopup("int.MinValue", WP);
                                else
                                    RailSystem.ManTrainPathing.TrainStatusPopup(charge.ChargeStrength.ToString(), WP);
                            }
                            else
                            {
                                RailSystem.ManTrainPathing.TrainStatusPopup("No Charge", WP);
                            }
                        }
                    }
                }
                if (allowQuickSnap && !cooldown && mEvent == ManPointer.Event.LMB && Input.GetKey(KickStart.SnapBlockButton))
                {
                    if ((bool)targVis.block)
                    {
                        target = targVis.block;
                        ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                        inst.Invoke("BeforeSnap", 1f);
                        cooldown = true;
                    }
                }
                ModuleHangar.OnBlockSelect(targVis, mEvent, DOWN, yes2);
            }
        }
        internal void BeforeSnap()
        {
            ManUI.inst.DoFlash(0.05f, 0.1f);
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Snapshot);
            inst.Invoke("MakeBlockPreviewJSONSimple", 0.05f);
            cooldown = false;
        }

        private static string expDirect = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "AutoBlockPNG");
        internal void MakeBlockPreviewJSONSimple()
        {   // The block preview is dirty, so we need to re-render a preview icon
            if (!target)
                return;
            TankBlock block = target;
            if (!Directory.Exists(expDirect))
                Directory.CreateDirectory(expDirect);
            string png = expDirect + "\\" + StringLookup.GetItemName(new ItemTypeInfo(ObjectTypes.Block, (int)block.BlockType)) + "_preview.png";
            Bounds bounds = block.BlockCellBounds;
            //float maxDimension = UnityEngine.Random.Range(1f, 3f) * Mathf.RoundToInt(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.5f) + 0.5f);

            Vector3 blockOGPos = block.transform.position;
            Quaternion blockOGRot = block.transform.rotation;
            Vector3 lookAngle = Camera.main.transform.position - (block.trans.position + (block.trans.rotation * bounds.center));
            lookAngle *= 0.6f;
            block.transform.position = new Vector3(0, 0, 2500);
            //block.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            Vector3 OGPos = Camera.main.transform.position;
            Camera.main.transform.position = lookAngle + block.trans.position;
            //Vector3 lookAngle = new Vector3(Mathf.Clamp(UnityEngine.Random.Range(-10000, 10000), -1, 1), UnityEngine.Random.Range(-1.5f, 1.5f), 1f).normalized * Vector3.one.magnitude;
            //Camera.main.transform.position = (bounds.center + lookAngle * maxDimension * 0.6f) + block.trans.position;
            Camera.main.transform.LookAt((block.trans.rotation * bounds.center) + block.trans.position, Vector3.up);
            float cache1 = Camera.main.farClipPlane;
            float cache2 = Camera.main.nearClipPlane;

            Camera.main.farClipPlane = 1000f;
            Camera.main.nearClipPlane = 0.1f;
            

            // Give the camera a render texture of fixed size
            RenderTexture rendTex = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32);
            RenderTexture.active = rendTex;
            RenderTexture old = Camera.main.targetTexture;

            // Render the block
            Camera.main.targetTexture = rendTex;
            Camera.main.Render();

            // Copy it into our target texture
            Texture2D preview = new Texture2D(1024, 1024);
            preview.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);

            // Write the target texture to disk
            FileUtils.SaveTexture(preview, png);

            // Return the camera to its previous settings
            Camera.main.targetTexture = old;
            Camera.main.farClipPlane = cache1;
            Camera.main.nearClipPlane = cache2;
            Camera.main.transform.position = OGPos;
            block.transform.position = blockOGPos;
            block.transform.rotation = blockOGRot;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rendTex);
        }


    }
}
