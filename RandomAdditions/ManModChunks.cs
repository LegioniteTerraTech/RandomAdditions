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
using System.IO;

/// <summary>
/// Makes the EXISTING chunks usuable again yay
/// </summary>
[AutoSaveManager]
public class ManModChunks : ModLoaderSystem<ManModChunks, ChunkTypes, CustomChunk>
{
    protected override string leadingFileName { get; } = "Res_";
    public override string LogDirectoryName { get; } = "Chunks";
    [SSManagerInst]
    public static ManModChunks inst = new ManModChunks();
    public static HashSet<ChunkTypes> Resurrected = new HashSet<ChunkTypes>();
    public static Dictionary<int, string> modChunksModNames = new Dictionary<int, string>();

    public static int ChunkPrice(ChunkTypes CT) => ResourceManager.inst.GetResourceDef(CT).saleValue;
    public ManModChunks()
    {
        WikiPageChunk.GetChunkModName = ChunkModNameWrapper;
    }
    protected override void Init_Internal()
    {
    }
    public static string ChunkModNameWrapper(int CT)
    {
        if (modChunksModNames.TryGetValue(CT, out string ModName))
            return ModName;
        if (Resurrected.Contains((ChunkTypes)CT))
            return KickStart.ModID;
        return WikiPageChunk.GetChunkModNameDefault(CT);
    }
    public static void PrepareAllChunks(bool reload)
    {
        DebugRandAddi.Log("ManModChunks: Loading all modded!");
        string path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.ToString(), "Custom Chunks");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        DebugRandAddi.Log("Path in: " + path);
        inst.CreateAll(reload, path);
        DebugRandAddi.Log("ManModChunks: finished!");
    }



    private const BindingFlags spamFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo ResLook = typeof(StringLookup).GetField("m_ChunkNames", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResLook2 = typeof(StringLookup).GetField("m_ChunkDescriptions", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly FieldInfo ResData = typeof(ResourceManager).GetField("m_DefinitionTable", spamFlags);
    private static readonly FieldInfo ResRare = typeof(ResourcePickup).GetField("m_ChunkRarity", spamFlags);
    private static readonly MethodInfo poolStart2 = typeof(ResourcePickup).GetMethod("OnPool", spamFlags);

    private static RecipeTable.RecipeList foundryRecipes;
    private static List<RecipeTable.Recipe> foundryDirect => foundryRecipes.m_Recipes;
    public static void RenewOldChunks()
    {
        InsureFoundryRecipes();
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
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Terreria Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Makes up tough, explosive-absorbing armor." +
                            "\nA very sturdy alloy fused from the best-bonding matchup out there: Plumbite and Titania!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = 3 * ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            3 * ChunkPrice(ChunkTypes.TitaniaIngot);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk, 
                                (int)ChunkTypes.PlumbiaIngot), 3),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.TitaniaIngot), 3),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_ThermiaIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Thermia Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Heat-resistant materials used for orbital re-entry." +
                            "\nAn alloy with extreme heat resistance: Ignite, Titania, and Oleite make this possible.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.OlasticBrick);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 1),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.OlasticBrick), 5),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_FulmeniaIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Fulmenia Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "A highly-potent energy conductor and storage.\n" +
                            "It is highly radioactive!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.RodiusCapsule);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 1),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.RodiusCapsule), 5),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_FunderiaIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Funderia Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Can unleash extreme amounts of power safely." +
                            "\nAn alloy with powerful energy bending properties.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.IgnianCrystal);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 1),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.IgnianCrystal), 5),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_PenniaIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Pennia Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Used to manipulate gravity at a finely grained level." +
                            "\nAn alloy with gravity-bending properties.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.CelestianCrystal);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 1),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.CelestianCrystal), 5),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_BosoniaIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Bosonia Ingot");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of highly intelligent blocks." +
                            "\nAn alloy capable of building advanced neural pathways.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        def.saleValue = ChunkPrice(ChunkTypes.PlumbiaIngot) +
                            5 * ChunkPrice(ChunkTypes.ErudianCrystal);
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 1),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.ErudianCrystal), 5),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent1:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Present");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "What's this? A present for me?  You shouldn't have!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent2:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Gift");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Whatever is inside is very soft.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent3:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Bonus");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "It's GOTTA be worth a LOT.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_ChristmasPresent4:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Surprise");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Hmm, what lies within?");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = 500;
                        break;
                    case ChunkTypes._deprecated_Stone:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Stone");
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Absolutely useless.  This has practically no use. Not even the trading stations want it...");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Component);
                        if (def.saleValue == 0)
                            def.saleValue = -1;
                        break;
                    case ChunkTypes._deprecated_HeartOre:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Heart Ore");//"Cardiacite"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "An extremely rare ore, \"Cardiacite\" is used in the creation of even greater intelligences.\n" +
                            "Why it has self-repairing properties like Luxite, but unlike it's yellow fellow, it can " +
                            "self-replicate!  \nIf given the right substances and conditions that is.\n\n" +
                            "(WIP) Can be grown with the Lab block.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 327;
                        break;
                    case ChunkTypes._deprecated_HeartCrystal:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Heart Crystal");//"Cardiac Prism"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of reality-bending, self-replicating machines.\nBeware of the singularity!\n" +
                            "Some believe it was the aftermath of a mighty widespread nano-machine race.  That's bogus!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        def.saleValue = 863;
                        break;
                    case ChunkTypes._deprecated_CommOre:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Comm Ore");//"Magellus Fragment"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "A mysterious material bursting with tiny explosive electrical sparks.  Looks alien in origin." +
                            "\nAffectionately known as \"Plasmite\" amongst many nations out there, this ore posesses " +
                            "impressive quantum energy funneling properties and is very volatile in nature.\n" +
                            "Rumors say of a planet made almost entirely of it lie somewhere in the cosmos, waiting to be plundered " +
                            "(or 'poloded).\n\n" +
                            "(WIP) Needs to be collected in a Pillars Biome with a Pillar Cracker");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 291;
                        break;
                    case ChunkTypes._deprecated_CommCrystal:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Comm Crystal");//Magellus Compound
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "The basis for excessively powerful energy manipulation weapons like E.P.M.C." +
                            "\nComm Crystals have the most fine-grained command over the wide spectrum of energies." +
                            "\nThe reach of energy types this can control appears seemingly limitless.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 442;
                        break;
                    case ChunkTypes._deprecated_SenseOre:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Sense Ore");//"Adaranthium"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "It is prized for it's powerful 3D detection and space-time bending properties." +
                            "\nThis originates from vibrant seas of <b>Adaranth</b>, a distant planet several parsecs away from the Off-World." +
                            "\nMany Kingdoms reside upon the oceanic planet and prosper greatly.\n\n" +
                            "(WIP) Can be found deep in the ocean with Ocean Mode enabled with Water Mod + Lava.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 4321;
                        break;
                    case ChunkTypes._deprecated_SenseCrystal:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Sense Crystal");//"Adaranth Ingot"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Used in the creation of devices which detect in the 3rd dimension as well as bend space-time. " +
                            "\nA beautiful pearl-like crystal worthy for a king or queen." +
                            "\nI have no idea how this works. Stuff this into a block" +
                            " and return to me with the results!");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 5263;
                        break;
                    case ChunkTypes._deprecated_SmallMetalOre:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Small Metal Ore");//"Tessellium"
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "Extremely tough and durable, this stellar metal ore is no ordinary Earth metal.\n" +
                            "It binds effortlessly to other metals, and becomes even stronger in the process.\n" +
                            "It goes by a great many names.  Some nations call it \"Tessellium\", others call it Bulk Compound.\n\n" +
                            "(WIP) Can be found high in the sky where Spaceships can spawn.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Raw);
                        if (def.saleValue == 0)
                            def.saleValue = 103636;
                        break;
                    case ChunkTypes._deprecated_SmallMetalIngot:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Small Metal Ingot");//Tesseract
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
                            "The ultimate building material.  \nA critical component for any bulky, tough armors " +
                            "that could take an onslaught from an entire armada.");
                        ManSpawn.inst.VisibleTypeInfo.SetDescriptor(hash, ChunkCategory.Refined);
                        if (def.saleValue == 0)
                            def.saleValue = 243262;
                        break;
                    case ChunkTypes._deprecated_AlloyExpRes:
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkName, stepper, "Alloy Exp Res");//Abstractum
                        LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, stepper,
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
                        if (!RecipeExistsInFoundry((ChunkTypes)stepper))
                            AddRecipeFoundryFast(new RecipeTable.Recipe.ItemSpec[]
                            {
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.PlumbiaIngot), 32),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes.TitaniaIngot), 8),
                                new RecipeTable.Recipe.ItemSpec( new ItemTypeInfo(ObjectTypes.Chunk,
                                (int)ChunkTypes._deprecated_SmallMetalIngot), 4),
                            }, new ItemTypeInfo(ObjectTypes.Chunk, stepper));
                        break;
                }
            }
            stepper++;
        }
        AddRecipesForOldChunks();
    }

    private static void InsureFoundryRecipes()
    {
        if (foundryRecipes == null)
        {
            foundryRecipes = new RecipeTable.RecipeList()
            {
                m_Name = "foundry",
                m_Recipes = new List<RecipeTable.Recipe>(),
                m_Root = false,
                m_UseForChunkCategoryCalculation = false,
                m_UseForMoneyRecipeCalculation = false,
                m_ValueAddFactor = 3,
            };
        }
    }

    private static readonly FieldInfo recData = typeof(RecipeManager).GetField("m_ModdedRecipes", spamFlags);
    private static Dictionary<string, RecipeTable.RecipeList> modRecipies;
    private static void AddRecipesForOldChunks()
    {
        if (modRecipies == null)
        {
            modRecipies = (Dictionary<string, RecipeTable.RecipeList>)recData.GetValue(RecipeManager.inst);
        }
        if (modRecipies != null)
        {
            AddRecipeListDemo(foundryRecipes);
        }
    }
    private static bool RecipeExistsInFoundry(ChunkTypes outputChunk)
    {
        int search = (int)outputChunk;
        return foundryDirect.Exists(x => x.m_OutputItems.First().m_Item.ItemType == search);
    }
    private static RecipeTable.Recipe AddRecipeFoundryFast(RecipeTable.Recipe.ItemSpec[] inputs, ItemTypeInfo outputChunk)
    {
        return new RecipeTable.Recipe()
        {
            m_EnergyOutput = 0f,
            m_BuildTimeSeconds = 3f,
            m_CalcState = RecipeTable.Recipe.CalcState.NeedUpdate,
            m_EnergyType = TechEnergy.EnergyType.Electric,
            m_MoneyOutput = 0,
            m_OutputType = RecipeTable.Recipe.OutputType.Items,
            m_InputItems = inputs,
            m_OutputItems = new RecipeTable.Recipe.ItemSpec[]
            {
                new RecipeTable.Recipe.ItemSpec(outputChunk, 3),
            },
        };
    }
    private static void AddRecipeListDemo(RecipeTable.RecipeList list)
    {
        if (modRecipies == null)
            modRecipies = (Dictionary<string, RecipeTable.RecipeList>)recData.GetValue(ResourceManager.inst);
        if (modRecipies != null && !modRecipies.ContainsKey(list.m_Name))
            modRecipies.Add(list.m_Name, list);
    }

    public void FindOldChunks()
    {
        DebugRandAddi.Log("FindOldChunks - Getting unused Chunks...");
        var defaultS = ManUI.inst.m_SpriteFetcher.GetSprite(ObjectTypes.Block, -1);
        for (int i = 0; i < Enum.GetValues(typeof(ChunkTypes)).Length; i++)
        {
            ChunkTypes CT = (ChunkTypes)i;
            if (CT.ToString().StartsWith("_deprecated"))
            {
                var nameLoc = StringLookup.GetItemName(ObjectTypes.Chunk, i);
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

    internal static CustomChunk ExtractFromExisting(ResourceTable.Definition objTarget)
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

    protected override CustomChunk ExtractFromExisting(object objTarget)
    {
        if (objTarget == null)
            throw new NullReferenceException("objTarget IS NULL");
        ResourceTable.Definition def = objTarget as ResourceTable.Definition;
        if (def == null)
            throw new NullReferenceException("ResourceTable.Definition IS NULL");
        Transform target = def.basePrefab;
        if (!target)
            throw new NullReferenceException("basePrefab IS NULL");
        Visible vis = target.GetComponent<Visible>();
        if (!vis)
            throw new NullReferenceException("visible IS NULL");
        Damageable dmg = target.GetComponent<Damageable>();
        if (!dmg)
            throw new NullReferenceException("Damageable IS NULL");
        ResourcePickup RP = target.GetComponent<ResourcePickup>();
        if (!RP)
            throw new NullReferenceException("ResourcePickup IS NULL");

        //Collider Col = target.GetComponent<Collider>();
        var CT = (ChunkTypes)vis.ItemType;
        var MR = target.GetComponentInChildren<MeshRenderer>(true);
        var MF = target.GetComponentInChildren<MeshFilter>(true);
        return new CustomChunk()
        {
            Name = target.name,
            Description = StringLookup.GetItemDescription(ObjectTypes.Chunk, vis.ItemType),
            PrefabName = target.name,
            TextureName = MR.sharedMaterial ?
                MR.sharedMaterial.name : MR.material.name,
            MeshName = MF.sharedMesh ?
                MF.sharedMesh.name : MF.mesh.name,
            Cost = def.saleValue,
            Health = (float)healthMain.GetValue(dmg),
            Mass = def.mass,
            Rarity = RP.ChunkRarity,
            DynamicFriction = def.frictionDynamic,
            StaticFriction = def.frictionStatic,
            Restitution = def.restitution,
            JSONData = new Dictionary<string, object>(),
        };
    }

    protected override void FinalAssignment(CustomChunk chunk, ChunkTypes AssignedID)
    {
        Visible vis = chunk.prefab.GetComponent<Visible>();
        int AssignedIDInt = (int)AssignedID;
        int PreviousIDInt = vis.m_ItemType.ItemType;
        if (PreviousIDInt == AssignedIDInt)
            return;
        Dictionary<int, int> IdToNameIndexLookup = (Dictionary<int, int>)ResLook.GetValue(null);
        int defRedirect = AssignedIDInt;
        if (PreviousIDInt == -1)
        {
            LocalisationExt.RegisterRawEng(StringBanks.ChunkName, AssignedIDInt, chunk.Name);
            LocalisationExt.RegisterRawEng(StringBanks.ChunkDescription, AssignedIDInt, chunk.Description);
        }
        else
            defRedirect = IdToNameIndexLookup[AssignedIDInt];
        if (PreviousIDInt != AssignedIDInt)
        { // We resync this with our new ID
            try
            {
                modChunksModNames.Remove(PreviousIDInt);
                IdToNameIndexLookup.Remove(PreviousIDInt);
                chunk.prefab.DeletePool();
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(typeof(ManModChunks).Name + ": Error when assigning \"" + chunk.Name + ", (" +
                    vis.m_ItemType.ItemType + ")\" to (" + AssignedIDInt + "): " + e);
            }
        }
        if (!modChunksModNames.ContainsKey(AssignedIDInt))
        {
            try
            {
                Registered.Add(chunk.fileName, AssignedID);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModChunks).Name + ": Error when registering \"" + chunk.Name +
                    ", (" + AssignedIDInt + ")", e);
            }
            chunk.prefabBase.def.m_ChunkType = AssignedID;
            var List = (Dictionary<ChunkTypes, ResourceManager.ResourceDefWrapper>)ResData.GetValue(ResourceManager.inst);
            List.Add(AssignedID, chunk.prefabBase);
            vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Chunk, AssignedIDInt);
            try
            {
                IdToNameIndexLookup.Add(AssignedIDInt, defRedirect);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModChunks).Name + ": Error when registering the name and description of \"" + 
                    chunk.Name + ", (" + AssignedIDInt + ")", e);
            }
            try
            {
                modChunksModNames.Add(AssignedIDInt, chunk.mod.ModID);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(ManModChunks).Name + ": Error when registering the mod name of \"" +
                    chunk.Name + ", (" + AssignedIDInt + ")", e);
            }
            chunk.prefab.CreatePool(4);
            DebugRandAddi.Log("ManModChunks: Assigned Custom Chunk " + chunk.Name + " to ID " + AssignedIDInt);
            var group = ManIngameWiki.InsureWikiGroup(chunk.mod.ModID, "Chunks", ManIngameWiki.ChunksSprite);
            new WikiPageChunk(AssignedIDInt, group);
        }
    }

    protected override void CreateInstanceFile(ModContainer Mod, string path, bool Reload = false)
    {
        string fileName = Path.GetFileName(path);
        string text;
        Active.TryGetValue(fileName, out CustomChunk chunk);
        if (chunk == null || Reload)
        {
            JSONConverterUniversal.Foundation = null;
            JSONConverterUniversal.CreateNew = true;
            text = File.ReadAllText(path);
            chunk = JsonConvert.DeserializeObject<CustomChunk>(text);//, new JSONConverterUniversal());
            if (chunk == null)
                throw new NullReferenceException("Chunk file " + fileName + " is corrupted!");
            chunk.mod = Mod;
        }
        if (!Reload && chunk.prefab != null)
            return;
        Active.Remove(fileName);
        if (ResourceManager.inst == null)
            throw new NullReferenceException("ResourceManager.inst is NULL - cannot continue!");
        var Prefabs = (Dictionary<ChunkTypes, ResourceManager.ResourceDefWrapper>)ResData.GetValue(ResourceManager.inst);
        if (Prefabs == null)
            throw new NullReferenceException("Chunk lookup is NULL - cannot continue!");
        ChunkTypes prefabType;
        if (chunk.PrefabName == null)
        {
            throw new NullReferenceException("Chunk PrefabName for file " + fileName + " does not exists!" +
                "  A chunk NEEDS a valid prefab to exist!");
        }
        else if (chunk.Mass <= 0)
        {
            throw new NullReferenceException("Chunk PrefabName \"" + chunk.PrefabName + "\" cannot have Mass of " + chunk.Mass.ToString() + ", " +
                "  A chunk NEEDS Mass greater than 0!");
        }
        else if (!EnumTryGetTypeFlexable(chunk.PrefabName, out prefabType))
            prefabType = ChunkTypes.Wood;
        if (Prefabs.TryGetValue(prefabType, out ResourceManager.ResourceDefWrapper PrefabOutter) &&
            PrefabOutter?.def?.basePrefab != null)
        {
            Transform Prefab = PrefabOutter.def.basePrefab;
            Transform Instance = Prefab.UnpooledSpawn();
            if (!Instance)
                throw new NullReferenceException("Instance is null");

            try
            {
                chunk.fileName = fileName;
                chunk.mod = Mod;
                Visible vis = Instance.GetComponent<Visible>();
                if (!vis)
                    throw new NullReferenceException("Vis is null");

                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Chunk, -1);
                Rigidbody rbody = Instance.GetComponent<Rigidbody>();
                if (!rbody)
                    throw new NullReferenceException("rbody is null");
                rbody.mass = chunk.Mass;
                ResourcePickup RP = Instance.GetComponent<ResourcePickup>();
                if (!RP)
                    throw new NullReferenceException("ResourcePickup is null");
                ResRare.SetValue(RP, chunk.Rarity);

                try
                {
                    var meshR = Mod.GetMaterialFromModAssetBundle(chunk.TextureName, false);
                    if (!meshR)
                        meshR = ResourcesHelper.GetMaterialFromBaseGameAllDeep(chunk.TextureName, false);
                    Instance.GetComponent<MeshRenderer>().sharedMaterial = meshR;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshRenderer null");
                }
                try
                {
                    var meshF = Mod.GetMeshFromModAssetBundle(chunk.MeshName, false);
                    if (meshF)
                        Instance.GetComponent<MeshFilter>().sharedMesh = meshF;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshFilter null");
                }
                var dmg = Instance.GetComponent<Damageable>();
                if (!dmg)
                    throw new NullReferenceException("Damageable is null");
                healthMain.SetValue(dmg, (int)(chunk.Health * 4096));


                poolStart.Invoke(vis, new object[] { });
                poolStart2.Invoke(RP, new object[] { });

                chunk.prefab = Instance;
                Instance.gameObject.SetActive(false);
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

                Active.Add(fileName, chunk);
                DebugRandAddi.Log("Created " + chunk.Name + " instance.");

            }
            catch (Exception e)
            {
                UnityEngine.Object.Destroy(Instance.gameObject);
                DebugRandAddi.Log("Failed to create " + chunk.Name + " instance - " + e);
            }
        }
        else
            throw new NullReferenceException("Chunk PrefabName \"" + chunk.PrefabName + "\" does not have a valid prefab instance!" +
                "  A chunk NEEDS a valid prefab to exist!");

    }
    protected override void CreateInstanceAsset(ModContainer Mod, TextAsset path, bool Reload = false)
    {
        string fileName = path.name;
        string text = null;
        Active.TryGetValue(fileName, out CustomChunk chunk);
        if (chunk == null || Reload)
        {
            JSONConverterUniversal.Foundation = null;
            JSONConverterUniversal.CreateNew = true;
            text = path.text;
            JsonConvert.DeserializeObject<CustomChunk>(text, new JSONConverterUniversal());
        }
        if (!Reload && chunk.prefab != null)
            return;
        var Prefabs = (Dictionary<ChunkTypes, ResourceManager.ResourceDefWrapper>)ResData.GetValue(ResourceManager.inst);
        ChunkTypes prefabType;
        if (chunk.PrefabName == null)
        {
            Debug.Log("Chunk PrefabName <FIELD IS NULL> does not exists!" +
                "  A chunk NEEDS a valid prefab to exist!");
            return;
        }
        else if (chunk.Mass <= 0)
        {
            Debug.Log("Chunk PrefabName \"" + chunk.PrefabName + "\" cannot have Mass of " + chunk.Mass.ToString() + ", " +
                "  A chunk NEEDS Mass greater than 0!");
            return;
        }
        else if (!EnumTryGetTypeFlexable(chunk.PrefabName, out prefabType))
            prefabType = ChunkTypes.Wood;
        if (Prefabs.TryGetValue(prefabType, out ResourceManager.ResourceDefWrapper PrefabOutter) &&
            PrefabOutter.def != null && PrefabOutter.def.basePrefab != null)
        {
            Transform Prefab = PrefabOutter.def.basePrefab;
            Transform Instance = Prefab.UnpooledSpawn();
            try
            {
                chunk.mod = Mod;
                chunk.fileName = fileName;
                Visible vis = Instance.GetComponent<Visible>();
                vis.m_ItemType = new ItemTypeInfo(ObjectTypes.Chunk, -1);
                Rigidbody rbody = Instance.GetComponent<Rigidbody>();
                rbody.mass = chunk.Mass;
                ResourcePickup RP = Instance.GetComponent<ResourcePickup>();
                ResRare.SetValue(RP, chunk.Rarity);
                try
                {
                    var meshR = Mod.GetMaterialFromModAssetBundle(chunk.TextureName, false);
                    if (!meshR)
                        meshR = ResourcesHelper.GetMaterialFromBaseGameAllDeep(chunk.TextureName, false);
                    Instance.GetComponent<MeshRenderer>().sharedMaterial = meshR;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshRenderer null");
                }
                try
                {
                    var meshF = Mod.GetMeshFromModAssetBundle(chunk.MeshName, false);
                    if (meshF)
                        Instance.GetComponent<MeshFilter>().sharedMesh = meshF;
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("MeshFilter null");
                }
                healthMain.SetValue(Instance.GetComponent<Damageable>(), (int)(chunk.Health * 4096));


                poolStart.Invoke(vis, new object[] { });
                poolStart2.Invoke(RP, new object[] { });

                chunk.prefab = Instance;
                Instance.gameObject.SetActive(false);
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

                Active.Add(fileName, chunk);

            }
            catch (Exception e)
            {
                UnityEngine.Object.Destroy(Instance.gameObject);
                DebugRandAddi.Log("Failed to create " + chunk.Name + " instance - " + e);
            }
        }
        else
            Debug.Log("Chunk PrefabName \"" + chunk.PrefabName + "\" does not have a valid prefab instance!" +
                "  A chunk NEEDS a valid prefab to exist!");

    }
}