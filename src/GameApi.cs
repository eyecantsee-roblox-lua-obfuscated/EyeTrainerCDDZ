using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CDDZTrainer
{
    /// <summary>
    /// Every call into the game's own code goes through here and is resolved by
    /// reflection, so the mod always compiles and a renamed member only disables
    /// the one feature that used it (instead of breaking the whole build).
    /// </summary>
    internal static class GameApi
    {
        const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.FlattenHierarchy;

        static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        static readonly Dictionary<string, List<MethodInfo>> methodCache = new Dictionary<string, List<MethodInfo>>();
        static readonly Dictionary<string, bool> reported = new Dictionary<string, bool>();

        /// <summary>The most recent "member not found / call failed" message, shown in the menu.</summary>
        public static string LastProblem = "";

        static void Report(string message)
        {
            if (reported.ContainsKey(message)) return;
            reported[message] = true;
            LastProblem = message;
            Debug.Log("[CDDZTrainer] " + message);
        }

        public static Type FindType(string fullName)
        {
            Type t;
            if (typeCache.TryGetValue(fullName, out t) && t != null)
            {
                return t;
            }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName, false); }
                catch { t = null; }

                if (t != null)
                {
                    typeCache[fullName] = t;
                    return t;
                }
            }
            return null;
        }

        static object GetStaticMember(Type type, string name)
        {
            PropertyInfo p = type.GetProperty(name, AnyMember);
            if (p != null) return p.GetValue(null, null);

            FieldInfo f = type.GetField(name, AnyMember);
            if (f != null) return f.GetValue(null);

            return null;
        }

        static List<MethodInfo> Candidates(Type type, string name)
        {
            string key = type.FullName + "." + name;
            List<MethodInfo> list;
            if (!methodCache.TryGetValue(key, out list))
            {
                list = new List<MethodInfo>();
                foreach (MethodInfo m in type.GetMethods(AnyMember))
                {
                    if (m.Name == name && !m.IsGenericMethodDefinition) list.Add(m);
                }
                methodCache[key] = list;
            }
            return list;
        }

        static bool TryConvert(object arg, Type p, out object value)
        {
            value = null;
            if (p.IsByRef) return false;
            if (arg == null) return !p.IsValueType;
            if (p.IsInstanceOfType(arg))
            {
                value = arg;
                return true;
            }

            try
            {
                if (p.IsEnum)
                {
                    if (arg is string) value = Enum.Parse(p, (string)arg);
                    else value = Enum.ToObject(p, arg);
                    return true;
                }
                if (arg is IConvertible && p.IsPrimitive)
                {
                    value = Convert.ChangeType(arg, p);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Calls an instance method by name, matching overloads on argument count/type.</summary>
        public static bool TryCall(object target, string name, out object result, params object[] args)
        {
            result = null;
            if (target == null) return false;
            if (args == null) args = new object[0];

            Type type = target.GetType();
            string lastError = null;

            foreach (MethodInfo m in Candidates(type, name))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != args.Length) continue;

                object[] converted = new object[ps.Length];
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (!TryConvert(args[i], ps[i].ParameterType, out converted[i]))
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;

                try
                {
                    result = m.Invoke(m.IsStatic ? null : target, converted);
                    return true;
                }
                catch (Exception e)
                {
                    lastError = e.GetBaseException().Message;
                }
            }

            if (lastError != null) Report(type.Name + "." + name + " failed: " + lastError);
            else Report(type.Name + "." + name + " not found (or arguments don't match)");
            return false;
        }

        public static bool Call(object target, string name, params object[] args)
        {
            object result;
            return TryCall(target, name, out result, args);
        }

        /// <summary>CharacterTracker.Instance.GetPlayer()</summary>
        public static Component GetPlayer()
        {
            try
            {
                Type tracker = FindType("CharacterTracker");
                if (tracker == null) return null;

                object instance = GetStaticMember(tracker, "Instance");
                if (instance == null)
                {
                    UnityEngine.Object[] found = FindAll(tracker);
                    if (found.Length > 0) instance = found[0];
                }
                if (instance == null) return null;

                MethodInfo m = tracker.GetMethod("GetPlayer", AnyMember, null, Type.EmptyTypes, null);
                if (m == null) return null;

                return m.Invoke(instance, null) as Component;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The Character component on (or above) a collider, or null.</summary>
        public static Component GetCharacterFrom(Collider col)
        {
            Type characterType = FindType("Character");
            if (characterType == null || col == null) return null;
            return col.GetComponentInParent(characterType);
        }

        public static bool IsAlive(Component character)
        {
            try
            {
                MethodInfo m = character.GetType().GetMethod("IsAlive", AnyMember, null, Type.EmptyTypes, null);
                if (m != null) return (bool)m.Invoke(character, null);
            }
            catch { }
            return true;
        }

        static MethodInfo NoArgMethod(Component c, string name)
        {
            return c.GetType().GetMethod(name, AnyMember, null, Type.EmptyTypes, null);
        }

        public static bool IsImmune(Component character)
        {
            try
            {
                MethodInfo m = NoArgMethod(character, "IsImmuneToDamage");
                if (m != null) return (bool)m.Invoke(character, null);
            }
            catch { }
            return false;
        }

        /// <summary>Uses the game's own Character.MakeImmuneToDamage / MakeNotImmuneToDamage.</summary>
        public static bool SetImmune(Component character, bool immune)
        {
            try
            {
                MethodInfo m = NoArgMethod(character, immune ? "MakeImmuneToDamage" : "MakeNotImmuneToDamage");
                if (m == null) return false;
                m.Invoke(character, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("[CDDZTrainer] SetImmune failed: " + e.GetBaseException().Message);
                return false;
            }
        }

        public static bool Teleport(Component character, Vector3 position)
        {
            return Call(character, "SetPositionOfUsOrMount", position);
        }

        /// <summary>The gameplay camera: Camera.main, else the player's own camera, else any enabled camera.</summary>
        public static Camera GetCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;

            try
            {
                Component player = GetPlayer();
                if (player != null)
                {
                    MethodInfo m = NoArgMethod(player, "GetPlayerCamera");
                    if (m != null)
                    {
                        Camera fromPlayer = m.Invoke(player, null) as Camera;
                        if (fromPlayer != null) return fromPlayer;
                    }
                }
            }
            catch { }

            foreach (Camera c in Camera.allCameras)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy) return c;
            }
            return null;
        }

        /// <summary>Living enemy robots (CharacterTracker.GetEnemyCharacters), excluding the player.</summary>
        public static List<Component> GetEnemies(Component player)
        {
            List<Component> list = new List<Component>();
            try
            {
                Type tracker = FindType("CharacterTracker");
                object instance = (tracker != null) ? GetStaticMember(tracker, "Instance") : null;
                MethodInfo m = (tracker != null)
                    ? tracker.GetMethod("GetEnemyCharacters", AnyMember, null, Type.EmptyTypes, null)
                    : null;

                if (instance != null && m != null)
                {
                    System.Collections.IEnumerable items = m.Invoke(instance, null) as System.Collections.IEnumerable;
                    if (items != null)
                    {
                        foreach (object o in items)
                        {
                            Component c = o as Component;
                            if (c != null && c != player) list.Add(c);
                        }
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch (Exception e)
            {
                Debug.Log("[CDDZTrainer] GetEnemies failed: " + e.GetBaseException().Message);
            }

            // Fallback: every Character in the scene except the player.
            foreach (UnityEngine.Object o in FindAll(FindType("Character")))
            {
                Component c = o as Component;
                if (c != null && c != player && IsAlive(c)) list.Add(c);
            }
            return list;
        }

        /// <summary>All "Kill" methods declared on Character / FirstPersonMover.</summary>
        public static List<MethodInfo> GetKillMethods()
        {
            List<MethodInfo> list = new List<MethodInfo>();
            string[] typeNames = { "Character", "FirstPersonMover" };

            foreach (string typeName in typeNames)
            {
                Type t = FindType(typeName);
                if (t == null) continue;

                MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (MethodInfo m in methods)
                {
                    if (m.Name == "Kill" && !m.IsAbstract && !m.ContainsGenericParameters)
                    {
                        list.Add(m);
                    }
                }
            }
            return list;
        }

        static object DefaultArg(Type t, Component attacker)
        {
            if (attacker != null && t != typeof(object) && t.IsAssignableFrom(attacker.GetType()))
            {
                return attacker;
            }
            if (t == typeof(bool)) return false;
            if (t.IsEnum) return Enum.GetValues(t).GetValue(0);
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }

        /// <summary>Kills a robot using the game's own Kill method. Returns true on success.</summary>
        public static bool KillCharacter(Component target, Component attacker)
        {
            if (target == null) return false;

            foreach (MethodInfo m in GetKillMethods())
            {
                if (!m.DeclaringType.IsAssignableFrom(target.GetType())) continue;

                ParameterInfo[] ps = m.GetParameters();
                object[] args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    args[i] = DefaultArg(ps[i].ParameterType, attacker);
                }

                try
                {
                    m.Invoke(target, args);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.Log("[CDDZTrainer] Kill overload failed: " + e.GetBaseException().Message);
                }
            }

            // Last resort: remove the object. Cruder (the game may not count it as a kill).
            Debug.Log("[CDDZTrainer] No working Kill method, destroying object instead.");
            UnityEngine.Object.Destroy(target.gameObject);
            return true;
        }

        /// <summary>FindObjectsByType / FindObjectsOfType, whichever this Unity version has.</summary>
        public static UnityEngine.Object[] FindAll(Type type)
        {
            if (type == null) return new UnityEngine.Object[0];

            try
            {
                foreach (MethodInfo m in typeof(UnityEngine.Object).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "FindObjectsByType") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(Type) && ps[1].ParameterType.IsEnum)
                    {
                        object sortNone = Enum.ToObject(ps[1].ParameterType, 0);
                        return (UnityEngine.Object[])m.Invoke(null, new object[] { type, sortNone });
                    }
                }

                MethodInfo old = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { typeof(Type) });
                if (old != null)
                {
                    return (UnityEngine.Object[])old.Invoke(null, new object[] { type });
                }
            }
            catch (Exception e)
            {
                Debug.Log("[CDDZTrainer] FindAll failed: " + e.GetBaseException().Message);
            }
            return new UnityEngine.Object[0];
        }

        /// <summary>Writes the interesting members of the game's character classes to a text file.</summary>
        public static string DumpApiInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("CDDZTrainer API dump " + DateTime.Now);
            sb.AppendLine("Harmony present: " + (FindType("HarmonyLib.Harmony") != null));
            sb.AppendLine();

            // Original dump values I needed at the time
            string[] typeNames = { "CharacterTracker", "Character", "FirstPersonMover", "MechBodyPart", "EnergySource" };
            string[] keys = { "kill", "damage", "health", "invinc", "immun", "energy", "alive", "dead", "player" };

            foreach (string typeName in typeNames)
            {
                Type t = FindType(typeName);
                if (t == null)
                {
                    sb.AppendLine("== " + typeName + ": NOT FOUND ==");
                    continue;
                }

                sb.AppendLine("== " + typeName + " (" + t.Assembly.GetName().Name + ") ==");
                foreach (MemberInfo mi in t.GetMembers(AnyMember))
                {
                    string lower = mi.Name.ToLowerInvariant();
                    bool match = false;
                    foreach (string key in keys)
                    {
                        if (lower.Contains(key)) { match = true; break; }
                    }
                    if (!match) continue;

                    string sig = mi.MemberType + " " + mi.Name;
                    MethodBase mb = mi as MethodBase;
                    if (mb != null)
                    {
                        List<string> ps = new List<string>();
                        foreach (ParameterInfo p in mb.GetParameters())
                        {
                            ps.Add(p.ParameterType.Name + " " + p.Name);
                        }
                        sig += "(" + string.Join(", ", ps.ToArray()) + ")";
                    }
                    sb.AppendLine("  " + sig + "   [declared in " + mi.DeclaringType.Name + "]");
                }
                sb.AppendLine();
            }

            string path = Path.Combine(Application.persistentDataPath, "CDDZTrainer_api_dump.txt");
            try { File.WriteAllText(path, sb.ToString()); }
            catch (Exception e) { path = "(could not write file: " + e.Message + ")"; }

            Debug.Log("[CDDZTrainer] " + sb);
            return path;
        }
    }

    /// <summary>
    /// Immunity: a Harmony prefix on the game's Kill methods that skips the call
    /// when the target is the player. Harmony is looked up by reflection (Mod-Bot
    /// already loads it), so there is no extra DLL to reference unless YOU are a quirky
    /// developer who wants to include it.
    /// </summary>
    internal static class ImmunityPatch
    {
        public static bool Enabled;
        public static string Status = "not installed yet";

        const string HarmonyId = "cddz.trainer.mod";
        static bool installed;

        // Harmony calls this before the original Kill. Returning false skips it.
        public static bool KillPrefix(object __instance)
        {
            if (!Enabled) return true;

            Component player = GameApi.GetPlayer();
            if (player != null && object.ReferenceEquals(__instance, player))
            {
                return false;
            }
            return true;
        }

        public static bool Install()
        {
            if (installed) return true;

            try
            {
                Type harmonyType = GameApi.FindType("HarmonyLib.Harmony");
                Type methodType = GameApi.FindType("HarmonyLib.HarmonyMethod");
                if (harmonyType == null || methodType == null)
                {
                    Status = "HarmonyLib not found in the game";
                    return false;
                }

                List<MethodInfo> targets = GameApi.GetKillMethods();
                if (targets.Count == 0)
                {
                    Status = "no Character.Kill method found (press F9 and check the dump)";
                    return false;
                }

                object harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });

                // Drop patches left by an earlier load of this mod.
                MethodInfo unpatch = harmonyType.GetMethod("UnpatchAll", new Type[] { typeof(string) });
                if (unpatch != null) unpatch.Invoke(harmony, new object[] { HarmonyId });

                MethodInfo mine = typeof(ImmunityPatch).GetMethod("KillPrefix", BindingFlags.Public | BindingFlags.Static);
                object prefix = Activator.CreateInstance(methodType);
                FieldInfo methodField = methodType.GetField("method");
                if (methodField != null)
                {
                    methodField.SetValue(prefix, mine);
                }
                else
                {
                    PropertyInfo methodProp = methodType.GetProperty("method");
                    methodProp.SetValue(prefix, mine, null);
                }

                MethodInfo patch = null;
                foreach (MethodInfo m in harmonyType.GetMethods())
                {
                    if (m.Name != "Patch") continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length >= 2 && ps[0].ParameterType == typeof(MethodBase) && ps[1].ParameterType == methodType)
                    {
                        patch = m;
                        break;
                    }
                }
                if (patch == null)
                {
                    Status = "Harmony.Patch method not found";
                    return false;
                }

                int count = 0;
                foreach (MethodInfo target in targets)
                {
                    object[] args = new object[patch.GetParameters().Length];
                    args[0] = target;
                    args[1] = prefix;
                    patch.Invoke(harmony, args);
                    count++;
                }

                installed = true;
                Status = "ready (" + count + " Kill method(s) patched)";
                return true;
            }
            catch (Exception e)
            {
                Status = "patch failed: " + e.GetBaseException().Message;
                Debug.Log("[CDDZTrainer] " + e);
                return false;
            }
        }
    }
}
