using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;

namespace RandomAdditions
{
    /// <summary>
    /// Probably won't be finished
    /// </summary>
    public class JSONRandAddModules : JSONModuleLoader
    {
        private static JSONRandAddModules inst;
        private static Dictionary<string, Type> registeredTypes = null;
        public static void CompileLookupAndInit()
        {
            if (registeredTypes != null)
                return;
            registeredTypes = new Dictionary<string, Type>();
            // Standalone
            Enlist(typeof(ModuleClock));
            Enlist(typeof(ModuleHangar));
            Enlist(typeof(ModuleJumpDrive));//?
            Enlist(typeof(ModuleModeSwitch));
            Enlist(typeof(ModuleOmniCore));
            Enlist(typeof(ModuleTileLoader));
            Enlist(typeof(ModuleTrajectory));

            // Needs other Vanilla Modules and advanced coding - unlikely to be implemented in Official JSON
            //Enlist(typeof(ModuleCustomShop));
            //Enlist(typeof(ModuleFuelEnergyGenerator));
            //Enlist(typeof(ModuleItemFixedHolderBeam));
            //Enlist(typeof(ModuleItemSilo));

            inst = new JSONRandAddModules();
            JSONBlockLoader.RegisterModuleLoader(inst);
        }
        private static void Enlist(Type type)
        {
            registeredTypes.Add(type.Name, type);
        }


        public override string GetModuleKey()
        {
            return "RandAddModules";
        }

        /// <summary>
        /// This actually handles multiple modules.  My bad.
        /// </summary>
        /// <param name="blockID"></param>
        /// <param name="def"></param>
        /// <param name="block"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public override bool CreateModuleForBlock(int blockID, ModdedBlockDefinition def, TankBlock block, JToken data)
        {
            if (data.Type == JTokenType.Object)
            {
                JObject obj = (JObject)data;
                Dictionary<string, JToken> modulesList = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(obj.ToString());
                foreach (var keyVal in modulesList)
                {
                    JObject objModule = (JObject)keyVal.Value;
                    if (registeredTypes.TryGetValue(keyVal.Key, out Type toAdd))
                    {
                        var comp = block.gameObject.GetComponent(toAdd);
                        if (comp == null)
                            comp = block.gameObject.AddComponent(toAdd);
                        FieldInfo[] fis = toAdd.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        if (fis != null)
                        {
                            foreach (var FI in fis)
                            {
                                try
                                {
                                    JObject field = TryGetObject(objModule, FI.Name);
                                    if (field != null)
                                    {
                                        FI.SetValue(comp, field.ToObject(FI.FieldType));
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }
        public override bool InjectBlock(int blockID, ModdedBlockDefinition def, JToken jToken)
        {
            return true;
        }

    }
}
