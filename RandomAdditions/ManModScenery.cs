using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;
using RandomAdditions;
using Newtonsoft.Json;
using System.IO;

/// <summary>
/// Remember that Scenery is stored in the game by it's GameObject name, not by it's own SceneryType 
///   (SceneryTypes is used by other utilities like Advanced AI to determine interactivity)
/// </summary>
[AutoSaveManager]
public class ManModScenery : ModLoaderSystem<ManModScenery, SceneryTypes, CustomScenery>
{
    protected override string leadingFileName { get; } = "Sce_";
    public override string LogDirectoryName { get; } = "Scenery";
    [SSManagerInst]
    public static ManModScenery inst = new ManModScenery();
    public static Dictionary<int, string> modSceneryModNames = new Dictionary<int, string>();
    internal static TerrainObjectTable table;
    public ManModScenery()
    {
        FieldInfo sce = typeof(ManSpawn).GetField("m_TerrainObjectTable", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo sce2 = typeof(TerrainObjectTable).GetField("m_GUIDToPrefabLookup", BindingFlags.NonPublic | BindingFlags.Instance);
        table = (TerrainObjectTable)sce.GetValue(ManSpawn.inst);
        if (table == null) 
            throw new NullReferenceException("ManModScenery: ManSpawn.inst has not allocated m_TerrainObjectTable for some reason and ManModScenery setup failed");
        WikiPageScenery.GetSceneryModName = SceneryModNameWrapper;
    }
    protected override void Init_Internal()
    {
    }
    public static string SceneryModNameWrapper(int CT)
    {
        if (modSceneryModNames.TryGetValue(CT, out string ModName))
            return ModName;
        return WikiPageScenery.GetSceneryModNameDefault(CT);
    }
    public static void PrepareAllScenery(bool reload)
    {
        DebugRandAddi.Log("ManModScenery: Loading all modded!");
        string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Scenery");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        inst.CreateAll(reload, path);
        DebugRandAddi.Log("ManModScenery: Loading finished!");
        DebugRandAddi.Log("ManModScenery: Rebuilding lookup!");
        SpawnHelper.GrabInitList(true);
        DebugRandAddi.Log("ManModScenery:  Rebuilding lookup finished!");
    }


    private static readonly FieldInfo ResLook = typeof(StringLookup).GetField("m_ChunkNames", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResLook2 = typeof(StringLookup).GetField("m_ChunkDescriptions", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo poolStart2 = typeof(TerrainObject).GetMethod("OnPool", spamFlags);
    private static readonly MethodInfo poolStart3 = typeof(ResourceDispenser).GetMethod("OnPool", spamFlags);

    protected override void FinalAssignment(CustomScenery scenery, SceneryTypes AssignedID)
    {
        Visible vis = scenery.prefab.GetComponent<Visible>();
        int AssignedIDInt = (int)AssignedID;
        int PreviousIDInt = vis.m_ItemType.ItemType;
        if (PreviousIDInt == AssignedIDInt)
            return;
        Dictionary<int, int> IdToNameIndexLookup = (Dictionary<int, int>)ResLook.GetValue(null);
        int defRedirect = AssignedIDInt;
        if (PreviousIDInt == -1)
        {
            LocalisationExt.RegisterRawEng(LocalisationEnums.StringBanks.SceneryName, AssignedIDInt, scenery.Name);
            LocalisationExt.RegisterRawEng(LocalisationEnums.StringBanks.SceneryDescription, AssignedIDInt, scenery.Description);
        }
        else
            defRedirect = IdToNameIndexLookup[AssignedIDInt];
        if (PreviousIDInt != AssignedIDInt)
        { // We resync this with our new ID
            try
            {
                modSceneryModNames.Remove(PreviousIDInt);
                IdToNameIndexLookup.Remove(PreviousIDInt);
                scenery.prefab.DeletePool();
            }
            catch (Exception e) 
            {
                DebugRandAddi.Log(typeof(ManModScenery).Name + ": Error when assigning \"" + 
                    scenery.Name + ", (" + PreviousIDInt + ")\" to (" + AssignedIDInt + "): " + e);
            }
        }
        if (!modSceneryModNames.ContainsKey(AssignedIDInt))
        {
            try
            {
                Registered.Add(scenery.fileName, AssignedID);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModScenery).Name + ": Error when registering \"" + 
                    scenery.Name + ", (" + AssignedIDInt + ")", e);
            }
            vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, AssignedIDInt);
            try
            {
                IdToNameIndexLookup.Add(AssignedIDInt, defRedirect);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModScenery).Name + ": Error when registering the name and description of \"" +
                    scenery.Name + ", (" + AssignedIDInt + ")", e);
            }
            try
            {
                modSceneryModNames.Add(AssignedIDInt, scenery.mod.ModID);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModScenery).Name + ": Error when registering the mod name for \"" +
                    scenery.Name + ", (" + AssignedIDInt + ")", e);
            }
            scenery.prefab.CreatePool(4);
            DebugRandAddi.Log("ManModScenery: Assigned Custom Scenery " + scenery.Name + " to ID " + AssignedIDInt);
            var group = ManIngameWiki.InsureWikiGroup(scenery.mod.ModID, "Resources", ManIngameWiki.ScenerySprite);
            new WikiPageScenery(AssignedIDInt, group);
        }
    }

    internal static CustomScenery ExtractFromExisting(TerrainObject objTarget)
    {
        try
        {
            return inst.ExtractFromExisting((object)objTarget);
        }
        catch (Exception e)
        {
            DebugRandAddi.Log("Failed to fetch " +
                (objTarget.name.NullOrEmpty() ? "<NULL>" : objTarget.name) + " - " + e);
            return null;
        }
    }

    protected override CustomScenery ExtractFromExisting(object objTarget)
    {
        TerrainObject def = objTarget as TerrainObject;
        if (!def)
            throw new NullReferenceException("TerrainObject IS NULL");
        Transform target = def.transform;
        if (!target)
            throw new NullReferenceException("Transform IS NULL");
        Visible vis = target.GetComponent<Visible>();
        if (!vis)
            throw new NullReferenceException("Visible IS NULL");
        ResourceDispenser RD = target.GetComponent<ResourceDispenser>();
        if (!RD)
            throw new NullReferenceException("ResourceDispenser IS NULL");
        Damageable dmg = target.GetComponent<Damageable>();
        var MR = target.GetComponentInChildren<MeshRenderer>(true);
        var MF = target.GetComponentInChildren<MeshFilter>(true);
        var CT = (ChunkTypes)vis.ItemType;
        if (dmg == null)
        {
            return new CustomScenery()
            {
                ID = target.name,
                Name = StringLookup.GetItemName(ObjectTypes.Scenery, vis.ItemType),
                Description = StringLookup.GetItemDescription(ObjectTypes.Scenery, vis.ItemType),
                PrefabName = target.name,
                TextureName = MR ? (MR.sharedMaterial ?
                MR.sharedMaterial.name : MR.material.name) : null,
                MeshName = MF ? (MF.sharedMesh ?
                MF.sharedMesh.name : MF.mesh.name) : null,
                DamageableType = ManDamage.DamageableType.Standard,
                Health = -1,//int.MaxValue / 2048f,
                GroundRadius = RD.GroundRadius,
                HostileFlora = false,
                MaxHeightOffset = 0,
                MinHeightOffset = 0,
                JSONData = new Dictionary<string, object>(),
            };
        }
        else
        {
            return new CustomScenery()
            {
                ID = target.name,
                Name = StringLookup.GetItemName(ObjectTypes.Scenery, vis.ItemType),
                Description = StringLookup.GetItemDescription(ObjectTypes.Chunk, vis.ItemType),
                PrefabName = target.name,
                TextureName = MR ? (MR.sharedMaterial ?
                MR.sharedMaterial.name : MR.material.name) : null,
                MeshName = MF ? (MF.sharedMesh ?
                MF.sharedMesh.name : MF.mesh.name) : null,
                DamageableType = dmg.DamageableType,
                Health = (float)healthMain.GetValue(dmg),
                GroundRadius = RD.GroundRadius,
                HostileFlora = false,
                MaxHeightOffset = 0,
                MinHeightOffset = 0,
                JSONData = new Dictionary<string, object>(),
            };
        }
    }
    protected override void CreateInstanceFile(ModContainer Mod, string path, bool Reload = false)
    {
        string fileName = Path.GetFileName(path);
        string text = null;
        Active.TryGetValue(fileName, out CustomScenery scenery);
        if (scenery == null || Reload)
        {
            JSONConverterUniversal.Foundation = null;
            JSONConverterUniversal.CreateNew = true;
            text = File.ReadAllText(path);
            scenery = JsonConvert.DeserializeObject<CustomScenery>(text);//, new JSONConverterUniversal());
            if (scenery == null)
                throw new NullReferenceException("Scenery file " + fileName + " is corrupted!");
            scenery.mod = Mod;
        }
        if (!Reload && scenery.prefab != null)
            return;
        Active.Remove(fileName);
        if (scenery.PrefabName == null)
        {
            throw new NullReferenceException("Scenery PrefabName for file " + fileName + " does not exists!" +
                "  Scenery NEEDS a valid prefab to exist!");
        }
        TerrainObject PrefabTO = SpawnHelper.GetResourceNodePrefab(scenery.PrefabName);// table.GetPrefabFromSavedGUID(scenery.PrefabName);
        if (PrefabTO)
        {
            try
            {
                scenery.mod = Mod;
                scenery.fileName = fileName;
                Transform Prefab = PrefabTO.transform;
                Transform Instance = Prefab.UnpooledSpawn();

                Visible vis = Instance.GetComponent<Visible>();
                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, -1);

                try
                {
                    var meshR = Mod.GetMaterialFromModAssetBundle(scenery.TextureName, false);
                    if (!meshR)
                        meshR = ResourcesHelper.GetMaterialFromBaseGameAllDeep(scenery.TextureName, false);
                    if (meshR != null)
                        Instance.GetComponent<MeshRenderer>().sharedMaterial = meshR;
                    else
                        DebugRandAddi.Assert("Texture null for " + fileName + "!!!");
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshRenderer null");
                }
                try
                {
                    var meshF = Mod.GetMeshFromModAssetBundle(scenery.MeshName, false);
                    if (meshF)
                        Instance.GetComponent<MeshFilter>().sharedMesh = meshF;
                    else
                        DebugRandAddi.Assert("Mesh null for " + fileName + "!!!");
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshFilter null");
                }
                var dmg = Instance.GetComponent<Damageable>();
                if (dmg)
                {
                    if (scenery.Health <= 0)
                        healthMain.SetValue(dmg, (int)(9001 * 4096));
                    else
                        healthMain.SetValue(dmg, (int)(scenery.Health * 4096));
                    dmg.m_DamageableType = scenery.DamageableType;
                }
                else if (scenery.Health > 0)
                    DebugRandAddi.Assert("Health set > 0 for " + fileName + ", but reference prefab has no Damageable!!!" +
                        "\n Change to a prefab that has a Damageable (destructable scenery)");

                TerrainObject TO = Instance.GetComponent<TerrainObject>();
                ResourceDispenser RD = Instance.GetComponent<ResourceDispenser>();

                poolStart.Invoke(vis, new object[] { });
                poolStart2.Invoke(TO, new object[] { });
                poolStart3.Invoke(RD, new object[] { });

                scenery.prefab = Instance;
                Instance.gameObject.SetActive(false);

                JSONConverterUniversal.Foundation = Instance.gameObject;
                JSONConverterUniversal.CreateNew = false;
                if (text == null)
                    text = File.ReadAllText(path);
                JsonConvert.DeserializeObject<CustomScenery>(text, new JSONConverterUniversal());

                Active.Add(fileName, scenery);
                DebugRandAddi.Log("Created " + scenery.Name + " instance.");

            }
            catch (Exception e)
            {
                UnityEngine.Object.Destroy(PrefabTO.gameObject);
                DebugRandAddi.Log("Failed to create " + scenery.Name + " instance - " + e);
            }
        }
        else
            throw new NullReferenceException("Scenery PrefabName \"" + scenery.PrefabName + "\" does not have a valid prefab instance!" +
                "  A chunk NEEDS a valid prefab to exist!");

    }
    protected override void CreateInstanceAsset(ModContainer Mod, TextAsset path, bool Reload = false)
    {
        string fileName = path.name;
        string text = null;
        Active.TryGetValue(fileName, out CustomScenery scenery);
        if (scenery == null || Reload)
        {
            JSONConverterUniversal.Foundation = null;
            JSONConverterUniversal.CreateNew = true;
            text = path.text;
            JsonConvert.DeserializeObject<CustomScenery>(text, new JSONConverterUniversal());
        }
        if (!Reload && scenery.prefab != null)
            return;
        Active.Remove(fileName);
        if (scenery.PrefabName == null)
        {
            Debug.Log("Scenery PrefabName <FIELD IS NULL> does not exists!" +
                "  A chunk NEEDS a valid prefab to exist!");
            return;
        }
        TerrainObject PrefabTO = table.GetPrefabFromSavedGUID(scenery.PrefabName);
        if (PrefabTO)
        {
            try
            {
                scenery.mod = Mod;
                scenery.fileName = fileName;
                Transform Prefab = PrefabTO.transform;
                Transform Instance = Prefab.UnpooledSpawn();

                Visible vis = Instance.GetComponent<Visible>();
                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, -1);

                Instance.GetComponent<MeshRenderer>().sharedMaterial =
                    Mod.GetMaterialFromModAssetBundle(scenery.TextureName);
                Instance.GetComponent<MeshFilter>().sharedMesh =
                    Mod.GetMeshFromModAssetBundle(scenery.MeshName);
                try
                {
                    var meshR = Mod.GetMaterialFromModAssetBundle(scenery.TextureName, false);
                    if (!meshR)
                        meshR = ResourcesHelper.GetMaterialFromBaseGameAllDeep(scenery.TextureName, false);
                    Instance.GetComponent<MeshRenderer>().sharedMaterial = meshR;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshRenderer null");
                }
                try
                {
                    var meshF = Mod.GetMeshFromModAssetBundle(scenery.MeshName, false);
                    if (meshF)
                        Instance.GetComponent<MeshFilter>().sharedMesh = meshF;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshFilter null");
                }
                healthMain.SetValue(Instance.GetComponent<Damageable>(), (int)(scenery.Health * 4096));

                TerrainObject TO = Instance.GetComponent<TerrainObject>();

                poolStart.Invoke(vis, new object[] { });
                poolStart2.Invoke(TO, new object[] { });

                scenery.prefab = Instance;
                Instance.gameObject.SetActive(false);

                JSONConverterUniversal.Foundation = Instance.gameObject;
                JSONConverterUniversal.CreateNew = false;
                if (text == null)
                    text = path.text;
                JsonConvert.DeserializeObject<CustomScenery>(text, new JSONConverterUniversal());

                Active.Add(fileName, scenery);

            }
            catch (Exception e)
            {
                UnityEngine.Object.Destroy(PrefabTO.gameObject);
                DebugRandAddi.Log("Failed to create " + scenery.Name + " instance - " + e);
            }
        }
        else
            Debug.Log("Chunk PrefabName \"" + scenery.PrefabName + "\" does not have a valid prefab instance!" +
                "  A chunk NEEDS a valid prefab to exist!");

    }
}
