using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using RandomAdditions;
using SafeSaves;
using TerraTechETCUtil;
using UnityEngine;

/// <summary>
/// Remember that Scenery is stored in the game by it's GameObject name, not by it's own SceneryType 
///   <para>(SceneryTypes is used by other utilities like Advanced AI to determine interactivity)</para>
///   <para>For other experimental matters see <seealso cref="CustomSceneryMaker"/></para>
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
        table = SpawnHelper.GetAllTerrainObjectPrefabTable();
        WikiPageScenery.GetSceneryModName = SceneryModNameWrapper;
    }
    public static void SanityCheck()
    {
        if (ResLook == null)
            throw new NullReferenceException(nameof(ResLook));
        if (ResLook2 == null)
            throw new NullReferenceException(nameof(ResLook2));
        /*
        if (poolStart2 == null)
            throw new NullReferenceException(nameof(poolStart2));
        if (poolStart3 == null)
            throw new NullReferenceException(nameof(poolStart3));//*/
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


    private static readonly FieldInfo ResLook = typeof(StringLookup).GetField("m_SceneryNames", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResLook2 = typeof(StringLookup).GetField("m_SceneryDescriptions", BindingFlags.NonPublic | BindingFlags.Static);
    //private static readonly MethodInfo poolStart2 = typeof(TerrainObject).GetMethod("OnPool", spamFlags);// Already done by "Instance.CreatePool(4)"
    //private static readonly MethodInfo poolStart3 = typeof(ResourceDispenser).GetMethod("OnPool", spamFlags);// Already done by "Instance.CreatePool(4)"


    protected override void FinalAssignmentStarting()
    {
        var tableCached = table.GetLookupList();
        foreach (var item in Active)
            tableCached.Remove(item.Key);
    }
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
                modSceneryModNames.Add(AssignedIDInt, scenery.runtimeMod.ModID);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModScenery).Name + ": Error when registering the mod name for \"" +
                    scenery.Name + ", (" + AssignedIDInt + ")", e);
            }
            scenery.prefab.CreatePool(4);
            DebugRandAddi.Log("ManModScenery: Assigned Custom Scenery " + scenery.Name + " to ID " + AssignedIDInt);
            var group = ManIngameWiki.InsureSceneryWikiGroup(scenery.runtimeMod.ModID);
            new WikiPageScenery(AssignedIDInt, group);
        }
    }

    protected override void FinalAssignmentFinished()
    {
        table.InitLookupTable();
        /// Then it goes to <see cref="AddOurSceneryNOW(Dictionary{string, TerrainObject})"/>
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
    protected override void LoadInstanceFile(ModContainer Mod, string path, bool Reload = false)
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
            scenery.runtimeMod = Mod;
        }
        if (!Reload && scenery.prefab != null)
            return;
        Active.Remove(fileName);
        LoadInstance(Mod, fileName, scenery);
    }
    protected override void LoadInstanceAsset(ModContainer Mod, TextAsset path, bool Reload = false)
    {
        string fileName = path.name;
        string text = null;
        Active.TryGetValue(fileName, out CustomScenery scenery);
        if (scenery == null || Reload)
        {
            JSONConverterUniversal.Foundation = null;
            JSONConverterUniversal.CreateNew = true;
            text = path.text;
            scenery = JsonConvert.DeserializeObject<CustomScenery>(text, new JSONConverterUniversal());
            if (scenery == null)
                throw new NullReferenceException("Scenery file " + fileName + " is corrupted!");
            scenery.runtimeMod = Mod;
        }
        if (!Reload && scenery.prefab != null)
            return;
        Active.Remove(fileName);
        LoadInstance(Mod, fileName, scenery);
    }

    /// <inheritdoc/>
    protected override void LoadInstance(ModContainer Mod, string ID, CustomScenery scenery)
    {
        if (Mod == null)
            throw new NullReferenceException("Mod is NULL - cannot continue!");
        if (ID.NullOrEmpty())
            throw new NullReferenceException("ID is NULL - cannot continue!");
        if (scenery == null)
            throw new NullReferenceException("CustomScenery is NULL - cannot continue!");
        if (scenery.PrefabName == null)
        {
            Debug.Log("Scenery PrefabName <FIELD IS NULL> does not exists!" +
                "  scenery NEEDS a valid prefab to exist!");
            return;
        }
        TerrainObject PrefabTO = SpawnHelper.GetResourceNodePrefab(scenery.PrefabName);
        if (PrefabTO != null)
        {
            try
            {
                scenery.runtimeMod = Mod;
                scenery.fileName = ID;
                Transform Prefab = PrefabTO.transform;
                if (Prefab == null)
                    throw new NullReferenceException("Prefab is null");
                Transform Instance = UnityEngine.Object.Instantiate(Prefab, null);
                if (Instance == null)
                    throw new NullReferenceException("Instance is null");

                Visible vis = Instance.GetComponent<Visible>();
                if (vis == null)
                    throw new NullReferenceException("Visible is null");
                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Scenery, -1);

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
                var dmg = Instance.GetComponent<Damageable>();
                if (dmg == null)
                    throw new NullReferenceException("Damageable is null");
                healthMain.SetValue(dmg, (int)(scenery.Health * 4096));

                TerrainObject TO = Instance.GetComponent<TerrainObject>();
                if (TO == null)
                    throw new NullReferenceException("TerrainObject is null");

                // Already done by "Instance.CreatePool(4)"
                //poolStart.Invoke(vis, new object[] { });
                //poolStart2.Invoke(TO, new object[] { });

                scenery.prefab = TO;
                Instance.gameObject.SetActive(false);

                Active.Add(ID, scenery);

                Instance.CreatePool(4);

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

    public static void AddOurSceneryNOW(Dictionary<string, TerrainObject> tableMainGame)
    {
        //CustomSceneryMaker.MakeScenery(tableMainGame);
        foreach (var item in inst.Registered)
        {
            if (inst.Active.TryGetValue(item.Key, out var cust))
            {
                if (tableMainGame.ContainsKey(item.Key))
                {
                    DebugRandAddi.LogError("We tried to add Scenery of name " + item +
                        " but we failed because it was still registered even when it shouldn't be.  We will now be out of sync");
                }
                else
                    tableMainGame.Add(item.Key, cust.prefab);
            }
            else
                DebugRandAddi.LogError("We tried to add Scenery of name " + item +
                    " but we failed because it does't actually exist(???).  We will now be out of sync");
        }
    }
}
