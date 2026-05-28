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
            PatchCodeGenerator.ResetCounter();

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

                // Skip constructors — Harmony cannot patch .ctor / .cctor directly
                if (methodName == ".ctor" || methodName == ".cctor")
                    continue;

                bool isInterface = declaringType.IsInterface;
                bool isStatic = method.IsStatic;

                bool hasGenericParams = method.HasGenericParameters;
                string[] genericParamNames = hasGenericParams
                    ? method.GenericParameters.Select(p => p.Name.ToString()).ToArray()
                    : new string[0];

                string[] paramTypes = method.Parameters
                    .Select(p =>
                    {
                        string typeName = p.Type?.FullName ?? "System.Object";
                        // Preserve & suffix for ref/out detection, strip mods
                        bool isByRef = typeName.EndsWith("&");
                        if (isByRef) typeName = typeName.TrimEnd('&');
                        typeName = PatchCodeGenerator.CleanTypeName(typeName);
                        return isByRef ? "ref " + typeName : typeName;
                    })
                    .ToArray();
                string[] paramNames = method.Parameters
                    .Select(p => string.IsNullOrEmpty(p.Name) ? "unnamed" : p.Name)
                    .ToArray();
                bool isVoid = method.ReturnType != null && method.ReturnType.FullName == "System.Void";
                string returnTypeName = method.ReturnType?.FullName ?? "System.Void";

                string code = PatchCodeGenerator.Generate(ns, className, methodName, paramTypes, paramNames,
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

        public static string CleanMethodName(string name) => PatchCodeGenerator.CleanMethodName(name);
    }
}
