using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace HarmonyScaffold
{
    public static class ObfuscatorDetector
    {
        public class DetectionResult
        {
            public string ObfuscatorName { get; set; }
            public List<string> SuspiciousMethods { get; set; } = new List<string>();
            public List<StringDecryptInfo> EncryptedStrings { get; set; } = new List<StringDecryptInfo>();
        }

        public class StringDecryptInfo
        {
            public string EncryptedValue { get; set; }
            public string DecryptMethod { get; set; }
            public string CallerMethod { get; set; }
        }

        public static DetectionResult Analyze(ModuleDefMD module)
        {
            var result = new DetectionResult();
            var moduleMethods = new HashSet<string>();

            foreach (var type in module.GetTypes())
                foreach (var m in type.Methods)
                    if (m.Body != null)
                        moduleMethods.Add(m.FullName);

            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods)
                {
                    if (method.Body == null) continue;

                    var instructions = method.Body.Instructions;
                    for (int i = 0; i < instructions.Count; i++)
                    {
                        var instr = instructions[i];
                        if (instr.OpCode == OpCodes.Call && instr.Operand is IMethod calledMethod)
                        {
                            var name = calledMethod.FullName;
                            // 检查是否调用了模块内的方法，且前面是 Ldstr
                            var prevInstr = i > 0 ? instructions[i - 1] : null;
                            if (prevInstr != null && prevInstr.OpCode == OpCodes.Ldstr)
                            {
                                // 检查被调方法是否定义在当前模块内
                                bool isInternal = moduleMethods.Contains(name);
                                if (isInternal || name.Contains("<Module>"))
                                {
                                    if (!result.SuspiciousMethods.Contains(name))
                                        result.SuspiciousMethods.Add(name);

                                    result.EncryptedStrings.Add(new StringDecryptInfo
                                    {
                                        EncryptedValue = (string)prevInstr.Operand,
                                        DecryptMethod = name,
                                        CallerMethod = method.FullName
                                    });
                                }
                            }
                        }
                    }
                }
            }

            if (result.EncryptedStrings.Count > 0)
                result.ObfuscatorName = "ConfuserEx (String Encryption Detected)";

            return result;
        }
    }
}