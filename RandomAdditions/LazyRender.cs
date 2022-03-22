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
    public class LazyRender : MonoBehaviour
    {   // crafting on fast just too slow?  enter *ludicrous speed*

        // Startup
        private static LazyRender inst;
        private static bool hooked = false;
        public static void Initiate()
        {
            if (hooked)
                return;
            Singleton.Manager<ManPointer>.inst.MouseEvent.Subscribe(OnClick);
            inst = new GameObject("LazyRender").AddComponent<LazyRender>();
            hooked = true;
        }


        // Events
        private static bool allowQuickSnap = true;
        private static bool cooldown = false;
        private static TankBlock target;
        public static void OnClick(ManPointer.Event mEvent, bool yes, bool yes2)
        {
            if (allowQuickSnap && !cooldown && mEvent == ManPointer.Event.LMB && Singleton.Manager<ManPointer>.inst.targetVisible)
            {
                if ((bool)Singleton.Manager<ManPointer>.inst.targetVisible.block && Input.GetKey(KeyCode.B))
                {
                    target = Singleton.Manager<ManPointer>.inst.targetVisible.block;
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                    inst.Invoke("BeforeSnap", 1f);
                    cooldown = true;
                }
            }
            // THIS IS FOR MODULE HANGAR

            if (Singleton.playerTank && mEvent == ManPointer.Event.LMB && Singleton.Manager<ManPointer>.inst.targetVisible)
            {
                Tank tech = Singleton.Manager<ManPointer>.inst.targetVisible.trans.root.GetComponent<Tank>();
                if (tech)
                {
                    if (tech.Team == Singleton.playerTank.Team && Singleton.playerTank != tech)
                    {
                        if (Input.GetKey(KeyCode.H))
                        {
                            foreach (TankBlock TB in Singleton.playerTank.blockman.IterateBlocks())
                            {
                                ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                                if (MH)
                                {
                                    if (MH.HasRoom && (!MH.IsDocking || Input.GetKey(KeyCode.LeftShift)))
                                    {
                                        if (MH.AssignToDock(tech))
                                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                                        else
                                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                                        break;
                                    }
                                }
                            }
                        }
                        /*
                        if (Input.GetKey(KeyCode.N))
                        {
                            foreach (TankBlock TB in Singleton.playerTank.blockman.IterateBlocks())
                            {
                                ModuleHangar MH = TB.GetComponent<ModuleHangar>();
                                if (MH)
                                {
                                    if (MH.HasRoom && (!MH.IsDocking || Input.GetKey(KeyCode.LeftShift)))
                                    {
                                        if (MH.RequestAssignToDock(tech))
                                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.AcceptMission);
                                        else
                                            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.MissionFailed);
                                        break;
                                    }
                                }
                            }
                        }*/
                    }
                }
            }
        }
        internal void BeforeSnap()
        {
            ManUI.inst.DoFlash(0.05f, 0.1f);
            ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Snapshot);
            inst.Invoke("MakeBlockPreviewJSONSimple", 0.05f);
            cooldown = false;
        }

        private static string expDirect = new DirectoryInfo(Application.dataPath).Parent.ToString() + "\\AutoBlockPNG";
        internal void MakeBlockPreviewJSONSimple()
        {   // The block preview is dirty, so we need to re-render a preview icon
            if (!target)
                return;
            TankBlock block = target;
            if (!Directory.Exists(expDirect))
                Directory.CreateDirectory(expDirect);
            string png = expDirect + "\\" + block.gameObject.name + "_preview.png";
            Bounds bounds = block.BlockCellBounds;
            float maxDimension = UnityEngine.Random.Range(1f, 3f) * Mathf.RoundToInt(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.5f) + 0.5f);

            Vector3 blockOGPos = block.transform.position;
            Quaternion blockOGRot = block.transform.rotation;
            block.transform.position = new Vector3(0, 0, 2500);
            block.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            Vector3 OGPos = Camera.main.transform.position;
            Vector3 lookAngle = new Vector3(Mathf.Clamp(UnityEngine.Random.Range(-10000, 10000), -1, 1), UnityEngine.Random.Range(-1.5f, 1.5f), 1f).normalized * Vector3.one.magnitude;
            Camera.main.transform.position = (bounds.center +
                lookAngle * maxDimension * 0.6f) + block.trans.position;
            Camera.main.transform.LookAt(bounds.center + block.trans.position, Vector3.up);
            float cache1 = Camera.main.farClipPlane;
            float cache2 = Camera.main.nearClipPlane;

            Camera.main.farClipPlane = 1000f;
            Camera.main.nearClipPlane = 0.1f;
            

            // Give the camera a render texture of fixed size
            RenderTexture rendTex = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32);
            RenderTexture.active = rendTex;
            RenderTexture old = Camera.main.targetTexture;

            // Render the block
            Camera.main.targetTexture = rendTex;
            Camera.main.Render();

            // Copy it into our target texture
            Texture2D preview = new Texture2D(512, 512);
            preview.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);

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
