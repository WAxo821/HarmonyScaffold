using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace HarmonyScaffold
{
    public static class PatchGenerator
    {
        public static string GenerateFromMethods(
            List<MethodDef> methods,
            string ns,
            string patchChoice,
            string author = "",
            bool useState = false)
        {
            var allCode = new List<string>();
            string harmonyId = null;

            foreach (var method in methods)
            {
                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                if (harmonyId == null && declaringType.Module?.Assembly != null)
                {
                    harmonyId = declaringType.Module.Assembly.Name;
                }

                string className = declaringType.FullName;
                string methodName = method.Name;
                string[] paramTypes = method.Parameters
                    .Select(p => CleanTypeName(p.Type.FullName))
                    .ToArray();
                string[] paramNames = method.Parameters
                    .Select(p => string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name)
                    .ToArray();
                string[] typeParams = new string[0];

                string code = Generate(ns, className, typeParams, methodName, paramTypes, paramNames, patchChoice, useState);
                allCode.Add(code);
            }

            string header = "";
            if (!string.IsNullOrEmpty(author))
            {
                header = $"// Author: {author}\r\n";
            }
            if (!string.IsNullOrEmpty(harmonyId))
            {
                header += $"// Harmony ID: {harmonyId}\r\n";
            }

            return header +
                   "using HarmonyLib;\r\n" +
                   (allCode.Any(c => c.Contains("System.Collections.Generic")) ? "using System.Collections.Generic;\r\n" : "") +
                   "\r\n" + string.Join("\r\n", allCode);
        }

        public static string Generate(
            string ns, string className, string[] typeParams,
            string methodName, string[] paramTypes, string[] paramNames,
            string patchChoice,
            bool useState = false)
        {
            string cleanClassName = className.Replace("+", ".");
            string patchClassName = $"{CleanMethodName(cleanClassName)}_{CleanMethodName(methodName)}_Patch";
            string classNameWithGenerics = cleanClassName;

            if (typeParams.Length > 0)
            {
                classNameWithGenerics = $"{cleanClassName}<{string.Join(", ", typeParams)}>";
            }

            string paramSignature = "";
            if (paramTypes.Length > 0 && paramNames.Length > 0)
            {
                var pairs = new string[Math.Min(paramTypes.Length, paramNames.Length)];
                for (int i = 0; i < pairs.Length; i++)
                {
                    pairs[i] = $"{paramTypes[i]} {paramNames[i]}";
                }
                paramSignature = string.Join(", ", pairs);
            }

            // __state 参数
            string stateParam = useState ? "object __state, " : "";

            string prefixCode = "";
            string postfixCode = "";
            string transpilerCode = "";
            string finalizerCode = "";

            if (patchChoice == "1" || patchChoice == "3")
            {
                prefixCode = $@"
        public static bool Prefix({stateParam}{paramSignature})
        {{
            // Your logic here
            return true;
        }}";
            }

            if (patchChoice == "2" || patchChoice == "3")
            {
                postfixCode = $@"
        public static void Postfix({stateParam}{paramSignature})
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
                finalizerCode = $@"
        public static void Finalizer({(string.IsNullOrEmpty(paramSignature) ? "" : paramSignature + ", ")}Exception __exception)
        {{
            // Your logic here
        }}";
            }

            return $@"namespace {ns}.Patches
{{
    [HarmonyPatch(typeof({classNameWithGenerics}), nameof({classNameWithGenerics}.{methodName}))]
    public static class {patchClassName}
    {{{prefixCode}{postfixCode}{transpilerCode}{finalizerCode}
    }}
}}";
        }

        private static string CleanTypeName(string typeName)
        {
            return typeName.Replace("&", "");
        }

        private static string CleanMethodName(string name)
        {
            return name.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace("+", "_").Replace("`", "_");
        }
    }
}