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

                // 跳过编译器生成的方法
                if (methodName.StartsWith("<") || methodName.StartsWith("get_") || methodName.StartsWith("set_"))
                    continue;

                // 接口方法标记，但仍生成（用户需要自己处理）
                bool isInterface = declaringType.IsInterface;

                // 检查是否是泛型方法
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

                // 检查是否是 void 返回类型
                bool isVoid = method.ReturnType != null && method.ReturnType.FullName == "System.Void";

                string code = Generate(ns, className, typeParams, methodName, paramTypes, paramNames, 
                    patchChoice, useState, isVoid, isInterface, genericParamNames);
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
            string[] genericParamNames = null)
        {
            string cleanClassName = className.Replace("+", ".");

            // 处理泛型类名：MyClass`1 -> MyClass<T>
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

            // 生成唯一类名，避免重载冲突
            _patchCounter++;
            string uniqueSuffix = _patchCounter > 1 ? "_" + _patchCounter : "";
            string patchClassName = $"{CleanMethodName(cleanClassName)}_{CleanMethodName(methodName)}_Patch{uniqueSuffix}";

            string classNameWithGenerics = cleanClassName;

            // 泛型方法参数
            string genericMethodParams = "";
            if (genericParamNames != null && genericParamNames.Length > 0)
            {
                genericMethodParams = "<" + string.Join(", ", genericParamNames) + ">";
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

            string stateParam = useState ? "object __state, " : "";

            string prefixCode = "";
            string postfixCode = "";
            string transpilerCode = "";
            string finalizerCode = "";

                       if (patchChoice == "1" || patchChoice == "3")
            {
                if (isVoid)
                {
                    prefixCode = $@"
        public static void Prefix({stateParam}{paramSignature})
        {{
            // Your logic here
        }}";
                }
                else
                {
                    prefixCode = $@"
        public static bool Prefix({stateParam}{paramSignature})
        {{
            // Your logic here
            return true;
        }}";
                }
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
                string finalizerParams = paramSignature;
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
                ? "        // WARNING: This is an interface method. Use a concrete type for typeof().\r\n"
                : "";

            string genericWarning = (genericParamNames != null && genericParamNames.Length > 0)
                ? "        // WARNING: This is a generic method. You may need to adjust the type parameters.\r\n"
                : "";

            return $@"namespace {ns}.Patches
{{
    [HarmonyPatch(typeof({classNameWithGenerics}), nameof({classNameWithGenerics}.{methodName}{genericMethodParams}))]
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