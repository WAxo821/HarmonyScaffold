using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
                if (parts.Length == 2 && int.TryParse(parts[1], out int arity))
                {
                    genericArity = arity;
                    string commas = arity > 1 ? new string(',', arity - 1) : "";
                    cleanClassName = parts[0] + "<" + commas + ">";
                }
            }

            // Validate patch choice
            if (patchChoice != "1" && patchChoice != "2" && patchChoice != "3" &&
                patchChoice != "4" && patchChoice != "5")
                patchChoice = "3";

            int count = Interlocked.Increment(ref _patchCounter);
            string uniqueSuffix = count > 1 ? "_" + count : "";
            string patchClassName = $"{CleanMethodName(cleanClassName)}_{CleanMethodName(methodName)}_Patch{uniqueSuffix}";
            string classNameWithGenerics = cleanClassName;

            string genericMethodParams = "";
            if (genericParamNames != null && genericParamNames.Length > 0)
                genericMethodParams = "<" + string.Join(", ", genericParamNames) + ">";

            string instanceType = genericArity > 0 ? "object" : cleanClassName;
            string instanceParam = (!isStatic && patchChoice != "4")
                ? $"{instanceType} __instance, "
                : "";

            string paramSignature = "";
            if (paramTypes.Length > 0 && paramNames.Length > 0)
            {
                var filteredPairs = new List<string>();
                for (int i = 0; i < Math.Min(paramTypes.Length, paramNames.Length); i++)
                {
                    string cleanType = CleanGenericTypeName(paramTypes[i]);
                    filteredPairs.Add($"{cleanType} {paramNames[i]}");
                }
                paramSignature = string.Join(", ", filteredPairs);
            }

            string stateParam = useState ? "object __state, " : "";

            // Normalize return type: backtick → C# format, fallback to object for open generics
            string cleanReturnType = CleanGenericTypeName(returnTypeName);
            string resultParam = "";
            if ((patchChoice == "2" || patchChoice == "3") && !isVoid && returnTypeName != "System.Void")
                resultParam = $"{cleanReturnType} __result, ";

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

        /// <summary>
        /// Converts dnlib backtick notation (List`1) to C# open-generic (List<>).
        /// Falls back to "object" when the result would be an un-declarable open generic.
        /// </summary>
        private static string CleanGenericTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "object";

            // Handle by-ref types
            bool isByRef = fullName.EndsWith("&");
            if (isByRef) fullName = fullName.TrimEnd('&');

            string result = fullName;

            // Convert backtick N → <,,> (open generic arity)
            int tick = result.IndexOf('`');
            if (tick >= 0)
            {
                string baseName = result.Substring(0, tick);
                string suffix = result.Substring(tick + 1);
                // Only convert if the suffix is a number (generic arity)
                if (int.TryParse(suffix, out int arity) && arity > 0)
                {
                    string commas = arity > 1 ? new string(',', arity - 1) : "";
                    result = baseName + "<" + commas + ">";
                }
            }

            // Open generics cannot be used as variable/argument types — fall back to object
            if (result.Contains("<") && result.Contains(">") && result.IndexOf('<') < result.IndexOf('>'))
            {
                int openStart = result.IndexOf('<');
                int openEnd = result.LastIndexOf('>');
                // Check if the bracket-enclosed content is only commas (open generic) or actual types
                string inner = result.Substring(openStart + 1, openEnd - openStart - 1);
                if (string.IsNullOrEmpty(inner) || inner.All(c => c == ','))
                    return "object";
            }

            return isByRef ? "ref " + result : result;
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

        internal static void ResetCounter() => Interlocked.Exchange(ref _patchCounter, 0);
    }
}
