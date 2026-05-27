using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace HarmonyScaffold
{
    public static class PatchGenerator
    {
        private static int _patchCounter = 0;

        public static string GenerateFromMethods(
            List<MethodDef> methods,
            string ns,
            string patchChoice,
            string author = "",
            bool useState = false)
        {
            var allCode = new List<string>();
            string harmonyId = null;
            _patchCounter = 0;

            foreach (var method in methods)
            {
                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                if (harmonyId == null && declaringType.Module?.Assembly != null)
                    harmonyId = declaringType.Module.Assembly.Name;

                string className = declaringType.FullName;
                string methodName = method.Name;

                if (methodName.StartsWith("<") || methodName.StartsWith("get_") || methodName.StartsWith("set_"))
                    continue;

                bool isInterface = declaringType.IsInterface;
                bool isStatic = method.IsStatic;

                bool hasGenericParams = method.HasGenericParameters;
                string[] genericParamNames = hasGenericParams
                    ? method.GenericParameters.Select(p => p.Name.ToString()).ToArray()
                    : new string[0];

                string[] paramTypes = method.Parameters
                    .Select(p => CleanTypeName(p.Type.FullName))
                    .ToArray();
                string[] paramNames = method.Parameters
                    .Select(p => string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name)
                    .ToArray();
                string[] typeParams = new string[0];

                bool isVoid = method.ReturnType != null && method.ReturnType.FullName == "System.Void";
                string returnTypeName = method.ReturnType?.FullName ?? "System.Void";

                string code = Generate(ns, className, typeParams, methodName, paramTypes, paramNames,
                    patchChoice, useState, isVoid, isInterface, isStatic, returnTypeName, genericParamNames);
                allCode.Add(code);
            }

            string header = "";
            if (!string.IsNullOrEmpty(author))
                header = $"// Author: {author}\r\n";
            if (!string.IsNullOrEmpty(harmonyId))
                header += $"// Harmony ID: {harmonyId}\r\n";

            return header +
                   "using HarmonyLib;\r\n" +
                   (allCode.Any(c => c.Contains("System.Collections.Generic")) ? "using System.Collections.Generic;\r\n" : "") +
                   "\r\n" + string.Join("\r\n", allCode);
        }

        public static string Generate(
            string ns, string className, string[] typeParams,
            string methodName, string[] paramTypes, string[] paramNames,
            string patchChoice,
            bool useState = false,
            bool isVoid = false,
            bool isInterface = false,
            bool isStatic = false,
            string returnTypeName = "System.Void",
            string[] genericParamNames = null)
        {
            string cleanClassName = className.Replace("+", ".");

            if (cleanClassName.Contains("`"))
            {
                var parts = cleanClassName.Split('`');
                if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                {
                    var typeArgs = new string[count];
                    for (int i = 0; i < count; i++)
                        typeArgs[i] = "T" + (i + 1);
                    cleanClassName = parts[0] + "<" + string.Join(", ", typeArgs) + ">";
                }
            }

            _patchCounter++;
            string uniqueSuffix = _patchCounter > 1 ? "_" + _patchCounter : "";
            string patchClassName = $"{CleanMethodName(cleanClassName)}_{CleanMethodName(methodName)}_Patch{uniqueSuffix}";
            string classNameWithGenerics = cleanClassName;

            string genericMethodParams = "";
            if (genericParamNames != null && genericParamNames.Length > 0)
                genericMethodParams = "<" + string.Join(", ", genericParamNames) + ">";

            // __instance 参数（非静态方法自动加）
            string instanceParam = (!isStatic && patchChoice != "4")
                ? $"{cleanClassName} __instance, "
                : "";

                        string paramSignature = "";
            if (paramTypes.Length > 0 && paramNames.Length > 0)
            {
                var filteredPairs = new List<string>();
                for (int i = 0; i < Math.Min(paramTypes.Length, paramNames.Length); i++)
                {
                    if (paramTypes[i] == cleanClassName)
                        continue;
                    filteredPairs.Add($"{paramTypes[i]} {paramNames[i]}");
                }
                paramSignature = string.Join(", ", filteredPairs);
            }

            string stateParam = useState ? "object __state, " : "";

            // __result 参数（Postfix 自动加）
            string resultParam = "";
            if ((patchChoice == "2" || patchChoice == "3") && !isVoid && returnTypeName != "System.Void")
                resultParam = $"{returnTypeName} __result, ";

            string prefixCode = "";
            string postfixCode = "";
            string transpilerCode = "";
            string finalizerCode = "";

            if (patchChoice == "1" || patchChoice == "3")
            {
                string fullParams = instanceParam + stateParam + paramSignature;
                fullParams = fullParams.TrimEnd(',', ' ');
                if (isVoid)
                {
                    prefixCode = $@"
        public static void Prefix({fullParams})
        {{
            // Your logic here
        }}";
                }
                else
                {
                    prefixCode = $@"
        public static bool Prefix({fullParams})
        {{
            // Your logic here
            return true;
        }}";
                }
            }

            if (patchChoice == "2" || patchChoice == "3")
            {
                string fullParams = instanceParam + stateParam + resultParam + paramSignature;
                fullParams = fullParams.TrimEnd(',', ' ');
                postfixCode = $@"
        public static void Postfix({fullParams})
        {{
            // Your logic here
        }}";
            }

            if (patchChoice == "4")
            {
                transpilerCode = $@"
        public static System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> Transpiler(
            System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> instructions)
        {{
            return instructions;
        }}";
            }

            if (patchChoice == "5")
            {
                string finalizerParams = instanceParam + paramSignature;
                finalizerParams = finalizerParams.TrimEnd(',', ' ');
                if (!string.IsNullOrEmpty(finalizerParams))
                    finalizerParams += ", ";
                finalizerParams += "Exception __exception";
                finalizerCode = $@"
        public static void Finalizer({finalizerParams})
        {{
            // Your logic here
        }}";
            }

            string interfaceWarning = isInterface
                ? $"        // WARNING: '{classNameWithGenerics}' is an interface. Replace typeof() with a concrete type and verify the method name.\r\n"
                : "";

            string genericWarning = (genericParamNames != null && genericParamNames.Length > 0)
                ? $"        // WARNING: Generic method — string overload \"{methodName}\" used. Verify Harmony resolves the correct method.\r\n"
                : "";

            // nameof 不支持泛型参数, 接口方法也无法直接 patch, 此时用字符串重载
            string methodRef = (genericParamNames != null && genericParamNames.Length > 0) || isInterface
                ? $"\"{methodName}\""
                : $"nameof({classNameWithGenerics}.{methodName}{genericMethodParams})";

            return $@"namespace {ns}.Patches
{{
    [HarmonyPatch(typeof({classNameWithGenerics}), {methodRef})]
    public static class {patchClassName}
    {{{interfaceWarning}{genericWarning}{prefixCode}{postfixCode}{transpilerCode}{finalizerCode}
    }}
}}";
        }

        private static string CleanTypeName(string typeName)
        {
            return typeName.Replace("&", "");
        }

        public static string CleanMethodName(string name)
        {
            return name.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace("+", "_").Replace("`", "_").Replace(" ", "_");
        }
    }
}