using RandomAdditions;
using RandomAdditions.RailSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;
using SafeSaves;
using Newtonsoft.Json;
using static LocalisationEnums;

/// <summary>
/// Makes the EXISTING chunks usuable again yay
/// </summary>
[AutoSaveManager]
public class ManCustomChunks
{
    [SSManagerInst]
    public static ManCustomChunks inst;
    public static HashSet<ChunkTypes> Resurrected = new HashSet<ChunkTypes>();

    public const int StartChunkID = 420;
    public Dictionary<string, CustomChunk> Active = new Dictionary<string, CustomChunk>();
    [SSaveField]
    public Dictionary<string, ChunkTypes> Registered = new Dictionary<string, ChunkTypes>();
    [SSaveField]
    public int RegisteredIDIterator = StartChunkID;
    public static int ChunkPrice(ChunkTypes CT) => ResourceManager.inst.GetResourceDef(CT).saleValue;

    public static void Init()
    {
        Init_Internal();

    }
    private static void Init_Internal()
    {
        if (inst == null)
        {
            inst = new ManCustomChunks();
            WikiPageChunk.GetChunkModName = ChunkModNameWrapper;
        }
    }
    public static string ChunkModNameWrapper(int CT)
    {
        if (Resurrected.Contains((ChunkTypes)CT))
            return KickStart.ModID;
        return WikiPageChunk.GetChunkModNameDefault(CT);
    }


    public static void RenewOldChunks()
    {
        DebugRandAddi.Log("FindOldChunks - Renewing unused Chunks...");
        int stepper = 420;
        Dictionary<int, int> vars = (Dictionary<int, int>)ResLook.GetValue(null);
        Dictionary<int, int> vars2 = (Dictionary<int, int>)ResLook2.GetValue(null);
        var defaultS = ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Block, -1);
        for (int i = 0; i < Enum.GetValues(typeof(ChunkTypes)).Length; i++)
        {
            ChunkTypes CT = (ChunkTypes)i;
            if (CT.ToString().StartsWith("_deprecated"))
            {
                Resurrected.Add(CT);
                vars.Add(i, stepper);
                vars2.Add(i, stepper);
                var nameLoc = StringLookup.GetItemName(ObjectTypes.Chunk, i);
                var sprite = ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Chunk, i);
                ResourceTable.Definition def = ResourceManager.inst.GetResourceDef(CT);
                /*
                DebugRandAddi.Log(CT.ToString() + " - Name: " + def.name + ",  Prefab: " + (def.basePrefab ? "True" : " False") +
                    ",  Sprite: " + (sprite != defaultS ? "True" : " False") + ",  LOC Name: " +
                    ("ERROR: String Not Found" != nameLoc ? nameLoc : "No_Name") +
                    "\n  Value: " + def.saleValue + ",  Mass: " + def.mass);
                */
                int hash = ItemTypeInfo.GetHashCode(ObjectTypes.Chunk, i);
                switch (CT)
                {
                    case ChunkTypes._deprecated_TerreriaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Terreria Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper, 
                            "Makes up tough, explosive-absorbing armor." +
                            "\nA very sturdy alloy fused from the best-bonding matchup out there: Plumbite and Titania!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = 3 * ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            3 * ChunkPrice(ChunkTypes.TitaniaIngot);
                        break;
                    case ChunkTypes._deprecated_ThermiaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Thermia Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Heat-resistant materials used for orbital re-entry." +
                            "\nAn alloy with extreme heat resistance: Ignite, Titania, and Oleite make this possible.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.OlasticBrick);
                        break;
                    case ChunkTypes._deprecated_FulmeniaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Fulmenia Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "A highly-potent energy conductor and storage.\n" +
                            "It is highly radioactive!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.RodiusCapsule);
                        break;
                    case ChunkTypes._deprecated_FunderiaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Funderia Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Can unleash extreme amounts of power safely." +
                            "\nAn alloy with powerful energy bending properties.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.IgnianCrystal);
                        break;
                    case ChunkTypes._deprecated_PenniaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Pennia Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Used to manipulate gravity at a finely grained level." +
                            "\nAn alloy with gravity-bending properties.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.CelestianCrystal);
                        break;
                    case ChunkTypes._deprecated_BosoniaIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Bosonia Ingot");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of highly intelligent blocks." +
                            "\nAn alloy capable of building advanced neural pathways.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) + 
                            5 * ChunkPrice(ChunkTypes.ErudianCrystal);
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent1:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Present");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "What's this? A present for me?  You shouldn't have!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent2:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Gift");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Whatever is inside is very soft.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent3:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Bonus");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "It's GOTTA be worth a LOT.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent4:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Surprise");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Hmm, what lies within?");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_Stone:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Stone");
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Absolutely useless.  This has practically no use. Not even the trading stations want it...");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = -1;
                        break;
                    case ChunkTypes._deprecated_HeartOre:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Heart Ore");//"Cardiacite"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "An extremely rare ore, \"Cardiacite\" is used in the creation of even greater intelligences.\n" +
                            "Why it has self-repairing properties like Luxite, but unlike it's yellow fellow, it can " +
                            "self-replicate!  \nIf given the right substances and conditions that is.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 327;
                        break;
                    case ChunkTypes._deprecated_HeartCrystal:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Heart Crystal");//"Cardiac Prism"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of reality-bending, self-replicating machines.\nBeware of the singularity!\n" +
                            "Some believe it was the aftermath of a mighty widespread nano-machine race.  That's bogus!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        def.saleValue = 863;
                        break;
                    case ChunkTypes._deprecated_CommOre:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Comm Ore");//"Magellus Fragment"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "A mysterious material bursting with tiny explosive electrical sparks.  Looks alien in origin." +
                            "\nAffectionately known as \"Plasmite\" amongst many nations out there, this ore posesses " +
                            "impressive quantum energy funneling properties and is very volitile in nature.\n" +
                            "Rumors say of a planet made almost entirely of it lie somewhere in the cosmos, waiting to be plundered " +
                            "(or 'poloded).");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 291;
                        break;
                    case ChunkTypes._deprecated_CommCrystal:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Comm Crystal");//Magellus Compound
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "The basis for excessively powerful energy manipulation weapons like E.P.M.C." +
                            "\nComm Crystals have very fine-grained command over the spectrum of energy." +
                            "\nThe bandwidth of energy types this can control seemingly appears limitless.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 442;
                        break;
                    case ChunkTypes._deprecated_SenseOre:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Sense Ore");//"Adaranthium"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "It is prized for it's powerful 3D detection and space-time bending properties." +
                            "\nThis originates from vibrant seas of <b>Adaranth</b>, a distant planet several parsecs away from the Off-World." +
                            "\nMany Kingdoms reside upon the oceanic planet and prosper greatly.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 4321;
                        break;
                    case ChunkTypes._deprecated_SenseCrystal:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Sense Crystal");//"Adaranth Ingot"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of devices which detect in the 3rd dimension as well as bend space-time. " +
                            "\nA beautiful pearl-like crystal worthy for a king or queen." +
                            "\nI have no idea how this works. Stuff this into a block" +
                            " and return to me with the results!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 5263;
                        break;
                    case ChunkTypes._deprecated_SmallMetalOre:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Small Metal Ore");//"Tessellium"
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Extremely tough and durable, this stellar metal ore is no ordinary Earth metal.\n" +
                            "It binds effortlessly to other metals, and becomes even stronger in the process.\n" +
                            "It goes by a great many names.  Some nations call it \"Tessellium\", others call it Bulk Compound.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 103636;
                        break;
                    case ChunkTypes._deprecated_SmallMetalIngot:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Small Metal Ingot");//Tesseract
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "The ultimate building material.  \nA critical component for any bulky, tough armors " +
                            "that could take an onslaught from an entire armada.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 243262;
                        break;
                    case ChunkTypes._deprecated_AlloyExpRes:
                        LocalisationExt.Register(StringBanks.ChunkName, stepper, "Alloy Exp Res");//Abstractum
                        LocalisationExt.Register(StringBanks.ChunkDescription, stepper,
                            "Better known as Bulkhead Alloy, this alloy is famous for having \"more hitpoints than god\"," +
                            "seemingly able to take on anything in the galaxy without a dent." +
                            "\nProcuring this however is nearly impossible as it's a deeply guarded trade secret by the highest of " +
                            "empires, and the most mighty of space pirateers."
                        /*
                    "It's... in a strange state of being...\n\"Abstractus\" is better known for jumping random places when it is restrained." +
                    "\n...It's probably best if you don't put this in any tractor beam..."*/);
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 4209001;
                        break;
                }
            }
            stepper++;
        }
    }


    private static readonly BindingFlags spamFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo ResLook = typeof(StringLookup).GetField("m_ChunkNames", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResLook2 = typeof(StringLookup).GetField("m_ChunkDescriptions", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResData = typeof(ResourceManager).GetField("m_DefinitionTable", spamFlags);
    private static readonly FieldInfo ResRare = typeof(ResourcePickup).GetField("m_ChunkRarity", spamFlags);
    private static readonly FieldInfo healthMain = typeof(Damageable).GetField("m_MaxHealthFixed", spamFlags);
    private static readonly MethodInfo poolStart = typeof(Visible).GetMethod("OnPool", spamFlags);
    private static readonly MethodInfo poolStart2 = typeof(ResourcePickup).GetMethod("OnPool", spamFlags);
    public void FindOldChunks()
    {
        DebugRandAddi.Log("FindOldChunks - Getting unused Chunks...");
        var defaultS = ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Block, -1);
        for (int i = 0; i < Enum.GetValues(typeof(ChunkTypes)).Length; i++)
        {
            ChunkTypes CT = (ChunkTypes)i;
            if (CT.ToString().StartsWith("_deprecated"))
            {
                var nameLoc = StringLookup.GetItemName(ObjectTypes.Chunk,i);
                var sprite = ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Chunk, i);
                ResourceTable.Definition def = ResourceManager.inst.GetResourceDef(CT);
                DebugRandAddi.Log(CT.ToString() + " - Name: " + def.name + ",  Prefab: " + (def.basePrefab ? "True" : " False") + 
                    ",  Sprite: " + (sprite != defaultS ? "True" : " False") + ",  LOC Name: " + 
                    ("ERROR: String Not Found" != nameLoc ? nameLoc : "No_Name") + 
                    "\n  Value: " + def.saleValue + ",  Mass: " + def.mass);
                ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Chunk, i);
            }
        }
    }


    public static void PrepareForSaving()
    {
        Init_Internal();
    }
    public static void FinishedSaving()
    {
    }
    public static void PrepareForLoading()
    {
        Init_Internal();
    }
    public static void FinishedLoading()
    {
        foreach (var item in inst.Registered)
        {
            if (inst.Active.TryGetValue(item.Key, out var val))
                inst.AssignCustomChunk(val, item.Value);
            else
                DebugRandAddi.Log("Resource Chunk \"" + item.Key + "\" is not available!  " +
                    "Will not be able to load it into game world!");
        }
        foreach (var item in inst.Active)
        {
            if (!inst.Registered.ContainsKey(item.Key))
            { 
                inst.AssignCustomChunk(item.Value, (ChunkTypes)inst.RegisteredIDIterator);
                inst.RegisteredIDIterator++;
            }
        }
    }

    public void AssignCustomChunk(CustomChunk chunk, ChunkTypes ID)
    {
        int IDi = (int)ID;
        Visible vis = chunk.prefab.GetComponent<Visible>();
        if (vis.m_ItemType.ItemType == IDi)
            return;
        Dictionary<int, int> vars = (Dictionary<int, int>)ResLook.GetValue(null);
        int prevEnd = IDi;
        if (vis.m_ItemType.ItemType == -1)
        {
            LocalisationExt.Register(StringBanks.ChunkName, IDi, chunk.Name);
            LocalisationExt.Register(StringBanks.ChunkDescription, IDi, chunk.Description);
        }
        else
            prevEnd = vars[IDi];
        if (vis.m_ItemType.ItemType != IDi)
        {
            try
            {
                vars.Remove(vis.m_ItemType.ItemType);
                chunk.prefab.DeletePool();
            }
            catch { }
        }
        Registered.Add(chunk.Name, ID);
        chunk.prefabBase.def.m_ChunkType = ID;
        var List = (Dictionary<ChunkTypes, ResourceManager.ResourceDefWrapper>)ResData.GetValue(ResourceManager.inst);
        List.Add(ID, chunk.prefabBase);
        vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Chunk, IDi);
        vars.Add(IDi, prevEnd);
        chunk.prefab.CreatePool(4);
    }

    public void CreateNewChunk(ModContainer Mod, CustomChunk chunk, bool Reload = false)
    {
        if (!Reload && chunk.prefab != null)
            return;
        var Prefabs = (Dictionary<ChunkTypes, ResourceManager.ResourceDefWrapper>)ResData.GetValue(ResourceManager.inst);

        if (chunk.PrefabName == null)
        {
            Debug.Log("Chunk PrefabName <FIELD IS NULL> does not exists!" +
                "  A chunk NEEDS a valid prefab to exist!");
        }
        else if (chunk.Mass <= 0)
        {
            Debug.Log("Chunk PrefabName \"" + chunk.PrefabName + "\" cannot have Mass of " + chunk.Mass.ToString() + ", " +
                "  A chunk NEEDS Mass greater than 0!");
        }
        else if (Enum.TryParse<ChunkTypes>(chunk.PrefabName, out var prefabName))
        {
            if (Prefabs.TryGetValue(prefabName, out ResourceManager.ResourceDefWrapper PrefabOutter) &&
                PrefabOutter.def != null && PrefabOutter.def.basePrefab != null)
            {

                Transform Prefab = PrefabOutter.def.basePrefab;
                Transform Instance = Prefab.UnpooledSpawn();

                Visible vis = Instance.GetComponent<Visible>();
                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Chunk, -1);
                Rigidbody rbody = Instance.GetComponent<Rigidbody>();
                rbody.mass = chunk.Mass;
                ResourcePickup RP = Instance.GetComponent<ResourcePickup>();
                ResRare.SetValue(RP, chunk.Rarity);

                Instance.GetComponent<MeshRenderer>().sharedMaterial = 
                    Mod.GetMaterialFromModAssetBundle(chunk.TextureName);
                Instance.GetComponent<MeshFilter>().sharedMesh =
                    Mod.GetMeshFromModAssetBundle(chunk.MeshName);
                healthMain.SetValue(Instance.GetComponent<Damageable>(), (int)(chunk.Health * 4096));


                poolStart.Invoke(vis, new object[] { });
                poolStart2.Invoke(RP, new object[] { });

                chunk.prefab = Instance;
                ResourceTable.Definition InstanceDef = new ResourceTable.Definition
                {
                    basePrefab = Instance,
                    frictionDynamic = chunk.DynamicFriction,
                    frictionStatic = chunk.StaticFriction,
                    mass = chunk.Mass,
                    saleValue = chunk.Cost,
                    m_ChunkType = (ChunkTypes)(-1),
                    name = chunk.Name,
                    restitution = chunk.Restitution,
                };
                ResourceManager.ResourceDefWrapper InstanceOutter = new ResourceManager.ResourceDefWrapper()
                {
                    def = InstanceDef,
                };
                InstanceOutter.InitMaterials();
                chunk.prefabBase = InstanceOutter;
                
                Active.Add(chunk.Name, chunk);
            }
            else
                Debug.Log("Chunk PrefabName \"" + chunk.PrefabName + "\" does not have a valid prefab instance!" +
                    "  A chunk NEEDS a valid prefab to exist!");
        }
        else
            Debug.Log("Chunk PrefabName \"" + chunk.PrefabName + "\" does not exists!" +
                "  A chunk NEEDS a valid prefab to exist!");
    }
}

public class CustomChunk
{

    [JsonIgnore]
    internal Transform prefab;
    [JsonIgnore]
    internal ResourceManager.ResourceDefWrapper prefabBase;

    public string Name = "Terry";
    public string Description = "A basic resource chunk";
    public string PrefabName = ChunkTypes.Wood.ToString();
    public string MeshName = "TerryMesh";
    public string TextureName = "TerryTex";
    public float Health = 50;
    public ChunkRarity Rarity = ChunkRarity.Common;
    public float Mass = 0.25f;
    public int Cost = 8;
    public float DynamicFriction = 0.8f;
    public float StaticFriction = 0.8f;
    public float Restitution = 1;
}
