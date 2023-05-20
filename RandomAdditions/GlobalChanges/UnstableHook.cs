using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Used for hooks to classes that are unstable-only
    ///   Safe and compact way to use reflection.
    /// </summary>
    internal struct UnstableHook<T>
    {
        private readonly T defaultVal;
        private readonly FieldInfo targ;

        public UnstableHook(string ClassName, string FieldName, T DefaultVal)
        {
            defaultVal = DefaultVal;
            Type t = Type.GetType(ClassName);
            if (t != null)
            {
                targ = t.GetField(FieldName, BindingFlags.Public | BindingFlags.Instance);
                if (targ == null)
                    DebugRandAddi.Log("UnstableHook could not find " + FieldName + " in type " + ClassName);
                else if (targ.FieldType != typeof(T))
                {
                    targ = null;
                    throw new Exception("UnstableHook generic type is set to " + typeof(T).FullName + " but target is of type " + targ.FieldType.FullName);
                }
            }
            else
            {
                DebugRandAddi.Log("UnstableHook could not find type " + ClassName);
                targ = null;
            }
        }

        /// <summary>
        /// Do NOT overcall as it uses reflection!
        /// </summary>
        /// <param name="inst"></param>
        /// <returns></returns>
        public T GetValue(object inst)
        {
            if (targ == null)
                return defaultVal;
            return (T)targ.GetValue(inst);
        }
    }
}
