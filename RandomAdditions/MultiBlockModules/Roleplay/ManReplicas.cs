using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Allows importing of external content in the Replicas folder
    /// </summary>
    public static class ManReplicas
    {
        public static string replicasPath => Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Replicas");

        public static void ShowReplicasFolder()
        {
            if (GUILayout.Button("Open Replicas Folder", AltUI.ButtonOrangeLarge))
            {
                if (!Directory.Exists(replicasPath))
                    Directory.CreateDirectory(replicasPath);
                KickStart.OpenInExplorer(replicasPath);
            }
        }


        internal static HashSet<ModuleReplica> allReps = new HashSet<ModuleReplica>();
        public static void ForceResetAndUpdateAllReps()
        {
            foreach (var item in allReps)
            {
                item.ReleaseTheSubjectIfAny();
            }
            foreach (var item in allReps)
            {
                item.LoadTheSubject();
            }
        }


        private static Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        public static bool TryGetReplica(string fileNameWithExt, out Texture2D get)
        {
            if (Textures.TryGetValue(fileNameWithExt, out get))
            {
                return get != null;
            }
            else
            {
                string target = Path.Combine(replicasPath, fileNameWithExt);
                if (File.Exists(target))
                {
                    get = FileUtils.LoadTexture(target);
                    if (get != null)
                        Textures.Add(fileNameWithExt, get);
                    return get != null;
                }
                get = null;
                return false;
            }
        }

        private static Dictionary<string, GameObject> preGeneratedTechs = new Dictionary<string, GameObject>();
        /// <summary>
        /// DO NOT ALTER THE MODELS MADE BY THIS, COPY THEM FIRST
        /// </summary>
        /// <param name="techName"></param>
        /// <param name="get"></param>
        /// <returns></returns>
        public static bool TryGetTechModelPrefab(string techName, out GameObject get)
        {
            if (preGeneratedTechs.TryGetValue(techName, out get))
            {
                return get != null;
            }
            else
            {
                if (ManSnapshots.inst.ServiceDisk.SnapshotExists(techName))
                {   // LOAD IT

                    string target = Path.Combine(ManScreenshot.GetSnapshotPath(), techName);
                    if (File.Exists(target) && ManScreenshot.TryDecodeSnapshotRender(FileUtils.LoadTexture(target),
                        out var techSnapData, techName, false))
                    {
                        get = MirageDestraction.MakeCopyMirageTankFromSpecs(techSnapData.CreateTechData().m_BlockSpecs, techName);
                        if (get != null)
                            preGeneratedTechs.Add(techName, get);
                        return get != null;
                    }
                }
                get = null;
                return false;
            }
        }
    }
}
