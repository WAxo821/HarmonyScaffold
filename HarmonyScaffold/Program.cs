using System;
using System.IO;
using System.Linq;

namespace HarmonyScaffold
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Harmony Patch Scaffolder V1.2 ===");
            Console.WriteLine();

            Console.Write("Namespace: ");
            string ns = Console.ReadLine();

            Console.Write("Class: ");
            string className = Console.ReadLine();

            Console.Write("Type parameters (comma separated, or empty): ");
            string typeParamsInput = Console.ReadLine();
            string[] typeParams = string.IsNullOrWhiteSpace(typeParamsInput)
                ? new string[0]
                : typeParamsInput.Split(',').Select(t => t.Trim()).ToArray();

            Console.Write("Method: ");
            string methodName = Console.ReadLine();

            Console.Write("Parameter types (comma separated, or empty): ");
            string paramTypesInput = Console.ReadLine();
            string[] paramTypes = string.IsNullOrWhiteSpace(paramTypesInput)
                ? new string[0]
                : paramTypesInput.Split(',').Select(t => t.Trim()).ToArray();

            Console.Write("Parameter names (comma separated, or empty): ");
            string paramNamesInput = Console.ReadLine();
            string[] paramNames = string.IsNullOrWhiteSpace(paramNamesInput)
                ? new string[0]
                : paramNamesInput.Split(',').Select(n => n.Trim()).ToArray();

            Console.WriteLine();
            Console.WriteLine("Patch type:");
            Console.WriteLine("  1 - Prefix only");
            Console.WriteLine("  2 - Postfix only");
            Console.WriteLine("  3 - Prefix + Postfix");
            Console.WriteLine("  4 - Transpiler");
            Console.Write("Choose (1-4): ");
            string patchChoice = Console.ReadLine();

            string code = PatchGenerator.Generate(ns, className, typeParams, methodName, paramTypes, paramNames, patchChoice);

            Console.WriteLine();
            Console.WriteLine("=== Generated Code ===");
            Console.WriteLine(code);

            string fileName = $"{className}_{methodName}_Patch.cs";
            File.WriteAllText(fileName, code);
            Console.WriteLine($"Saved to: {fileName}");

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}