using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RandomAdditions;
using TerraTechETCUtil;
using SafeSaves;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using System.IO;

public class JSONConverterUniversal : JsonConverter
{
    public static bool CreateNew;
    public static GameObject Foundation;
    public override bool CanWrite => false;
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
    }

    private static void GenerateAdvanced(JToken token)
    {
        Foundation = CustomModules.NuterraDeserializer.DeserializeIntoGameObject((JObject)token, Foundation);
    }
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject json = JObject.Load(reader);
        if (CreateNew)
        {
            if (existingValue == null)
                existingValue = Activator.CreateInstance(objectType);//json.ToObject(objectType);
            try
            {
                foreach (var item in json)
                {
                    JToken token = item.Value;
                    if (token is JObject obj)
                    {
                        if (item.Key != "JSONData")
                            obj.Merge(existingValue);
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidCastException("The whole file was in an unexpected format", e);
            }
        }
        else
        {
            try
            {
                foreach (var item in json)
                {
                    JToken token = item.Value;
                    if (item.Key == "JSONData")
                    {
                        try
                        {
                            GenerateAdvanced(token);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                throw new InvalidCastException("The whole file was in an unexpected format", e);
            }
        }
        return existingValue;
    }

    public override bool CanConvert(Type objectType) => true;
}

public abstract class ModLoadable
{
    [JsonIgnore]
    public ModContainer mod;
    [JsonIgnore]
    public string fileName;
    public Dictionary<string,object> JSONData = new Dictionary<string,object>();
}
public abstract class ModLoaderSystem<T, V, A> where T : ModLoaderSystem<T, V, A>, new()
    where V : struct where A : ModLoadable
{
    protected abstract string leadingFileName { get; }
    protected const BindingFlags spamFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    protected static readonly FieldInfo healthMain = typeof(Damageable).GetField("m_OrigMaxHealth", spamFlags);
    protected static readonly MethodInfo poolStart = typeof(Visible).GetMethod("OnPool", spamFlags);
    public static bool enabled = false;

    public Dictionary<string, A> Active = new Dictionary<string, A>();
    [SSaveField]
    public Dictionary<string, V> Registered = new Dictionary<string, V>();
    [SSaveField]
    public int RegisteredIDIterator = 420;

    protected abstract void Init_Internal();
    protected abstract void FinalAssignment(A instance, V type);
    public void PrepareForSaving()
    {
        if (!enabled)
            return;
        try
        {
            Init_Internal();
        }
        catch (Exception e)
        {
            DebugRandAddi.Log("Cascade crash of " + typeof(T) + ".PrepareForSaving(): " + e);
        }
    }
    public void FinishedSaving()
    {
    }
    public void PrepareForLoading()
    {
        if (!enabled)
            return;
        try
        {
            Init_Internal();
        }
        catch (Exception e)
        {
            DebugRandAddi.Log("Cascade crash of " + typeof(T) + ".PrepareForLoading(): " + e);
        }
    }
    public void FinishedLoading()
    {
        if (!enabled)
            return;
        try
        {
            // Check for all active instance prefabs. If we have any, reassign then.
            foreach (var item in Registered)
            {
                if (Active.TryGetValue(item.Key, out var val))
                {
                    DebugRandAddi.Log(typeof(A).Name + " \"" + item.Key + ", (" + item.Value + ")\" Re-registering.");
                    FinalAssignment(val, item.Value);
                }
                else
                    DebugRandAddi.Log(typeof(A).Name + " \"" + item.Key + ", (" + item.Value + ")\" is not available!  " +
                        "Will not be able to load it into game world!");
            }
            // Then add in all of the new Active ones.
            foreach (var item in Active)
            {
                if (!Registered.ContainsKey(item.Key))
                {   // Add in all of the new ones
                    V value = (V)(object)RegisteredIDIterator++;
                    DebugRandAddi.Log(typeof(A).Name + " \"" + item.Key + ", (" + value + ")\" is being added!");
                    FinalAssignment(item.Value, value);
                }
            }
        }
        catch (Exception e)
        {
            DebugRandAddi.Log("Cascade crash of " + typeof(T) + ".FinishedLoading(): " + e);
        }
    }

    public void CreateAll(bool reload, string path)
    {
        CreateLocal(reload, path);
        CreateWorkshop(reload);
    }
    public void CreateLocal(bool reload, string path)
    {
        var MC = ResourcesHelper.GetModContainer("Random Additions", out _);
        foreach (var item in Directory.GetFiles(path))
        {
            string filename = Path.GetFileNameWithoutExtension(item);
            if (File.Exists(item))
            {
                try
                {
                    CreateInstanceFile(MC, item, reload);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log(GetType().Name + ": Failed to load " + (filename.NullOrEmpty() ? "<NULL>" : filename) + " - " + e);
                }
            }
            foreach (var item2 in Directory.GetDirectories(path))
            {
                CreateLocal(reload, item2);
            }
        }
    }
    public void CreateWorkshop(bool reload)
    {
        Init_Internal();
        string searchCache = leadingFileName;
        foreach (var item in ResourcesHelper.IterateAllMods())
        {
            var contain = item.Value;
            foreach (var item2 in contain.Contents.m_AdditionalAssets.FindAll(x => x.name.StartsWith(searchCache) && x is TextAsset))
            {
                try
                {
                    CreateInstanceAsset(contain, item2 as TextAsset, reload);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log(GetType().Name + ": Failed to load " + (item2.name.NullOrEmpty() ? "<NULL>" : item2.name) + " - " + e);
                }
            }
        }
    }


    protected abstract A ExtractFromExisting(object target);
    protected abstract void CreateInstanceFile(ModContainer Mod, string path, bool Reload = false);
    protected abstract void CreateInstanceAsset(ModContainer Mod, TextAsset asset, bool Reload = false);


    public static bool EnumTryGetTypeFlexable<T>(string name, out T output) where T : struct
    {
        output = default;
        if (name.NullOrEmpty())
            return false;

        if (int.TryParse(name, out int result))
        {
            output = (T)(object)result;
            return true;
        }
        if (Enum.TryParse(name, out T result2))
        {
            output = result2;
            return true;
        }
        return false;
    }

    public static A GenerateGOFromJSON(string json)
    { 
        return JsonConvert.DeserializeObject<A>(json, serializerSettings);
    }

    public static JSONConverterUniversal serializerSuper = new JSONConverterUniversal();
    public static JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };
    /*
    public static Stack<StringBuilder> seqencer = new Stack<StringBuilder>();
    public static void RecurseGenerateGOFromJSON(string data, GameObject hierachyCurrent)
    {
        CustomModules.NuterraDeserializer.DeserializeIntoGameObject();
        foreach (var item in data)
        {
            if (item.Key.NullOrEmpty()) 
                continue;
            if (item.Value is MonoBehaviour Mono)
            {
            }
            else if (item.Value is Dictionary<string, object> nested)
            {
                GameObject GO2 = new GameObject(item.Key);
                GO2.transform.SetParent(hierachyCurrent.transform);
                RecurseGenerateGOFromJSON(nested, GO2);
            }
        }
    }


    public static void ReadUntilEndLine(int position, string data)
    {
        seqencer.Append(data.Skip(position).TakeWhile(x => x == '\n'));
    }
    private static void FinalizeAndBuild(StringBuilder SB)
    { 
        
    }
    private static void IterateAndBuildBrackets(string data)
    {
        int depth = 0;
        StringBuilder context = new StringBuilder();
        foreach (var item in data)
        {
            switch (item)
            {
                case '{':
                    seqencer.Push(context);
                    context = new StringBuilder();
                    depth++;
                    break;
                case '}':
                    FinalizeAndBuild(context);
                    context = seqencer.Pop();
                    depth--;
                    break;
                default:
                    break;
            }
            context.Append(item);
        }
    }
    public static void GetBrackets(int position, string data)
    {
        seqencer.Append(data.Skip(position).TakeWhile(x => x == '\n'));
    }
    */
}
