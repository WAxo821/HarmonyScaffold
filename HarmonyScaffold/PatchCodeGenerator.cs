using System;
using System.Collections.Generic;

namespace HarmonyScaffold
{
    /// <summary>
    /// 纯字符串级别的 Harmony 补丁代码生成器，无外部依赖。
    /// CLI / AI 工具可直接调用，不需要 dnlib。
    /// </summary>
    public static class PatchCodeGenerator
    {
        private static int _patchCounter = 0;

        public static string Generate(
            string ns,
            string className,
            string methodName,
            string[] paramTypes,
            string[] paramNames,
            string patchChoice,
            bool useState = false,
            bool isVoid = false,
            bool isInterface = false,
            bool isStatic = false,
            string returnTypeName = "System.Void",
            string[] genericParamNames = null)
        {
            string cleanClassName = className.Replace("+", ".");

            // Detect generic arity and build open-generic typeof() (e.g. typeof(List<>))
            int genericArity = 0;
            if (cleanClassName.Contains("`"))
            {
                var parts = cleanClassName.Split('`');
                if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                {
                    genericArity = count;
                    string commas = count > 1 ? new string(',', count - 1) : "";
                    cleanClassName = parts[0] + "<" + commas + ">";
                }
            }

            // Validate patch choice
            if (patchChoice != "1" && patchChoice != "2" && patchChoice != "3" &&
                patchChoice != "4" && patchChoice != "5")
                patchChoice = "3";

            _patchCounter++;
            string uniqueSuffix = _patchCounter > 1 ? "_" + _patchCounter : "";
            string patchClassName = $"{CleanMethodName(cleanClassName)}_{CleanMethodName(methodName)}_Patch{uniqueSuffix}";
            string classNameWithGenerics = cleanClassName;

            string genericMethodParams = "";
            if (genericParamNames != null && genericParamNames.Length > 0)
                genericMethodParams = "<" + string.Join(", ", genericParamNames) + ">";

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

            // Use string overload when nameof() would fail (generic types, special chars, interfaces)
            bool hasSpecialChars = methodName.IndexOfAny(new[] { '<', '>', '-', '#', ' ' }) >= 0
                || classNameWithGenerics.IndexOfAny(new[] { '<', '>', '-', '#', ' ' }) >= 0;
            bool needStringOverload = (genericParamNames != null && genericParamNames.Length > 0)
                || isInterface || hasSpecialChars || genericArity > 0;
            string methodRef = needStringOverload
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

        public static string CleanTypeName(string typeName)
        {
            return typeName.Replace("&", "");
        }

        public static string CleanMethodName(string name)
        {
            return name.Replace(".", "_")
                       .Replace("<", "_")
                       .Replace(">", "_")
                       .Replace("+", "_")
                       .Replace("`", "_")
                       .Replace(" ", "_")
                       .Replace("\\", "_")
                       .Replace("/", "_")
                       .Replace(":", "_");
        }

        internal static void ResetCounter() => _patchCounter = 0;
    }
}
