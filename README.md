在保留1.0功能的基础上将de4dot加入了HarmonyScaffold，现在可以在dnspyex里直接生成反混淆后的dll文件，文件保存路径在原dll的同级目录下（注：这并不意味着可以直接一键生成，仍然需要用dnspyex再次打开反混淆后的...cleaned.dll文件才能生成）

Building on v1.0 features, de4dot has been integrated into HarmonyScaffold. You can now generate deobfuscated DLL files directly within dnSpyEx. The output file is saved in the same directory as the original DLL. (Note: This does not mean one-click generation — you still need to open the deobfuscated ...cleaned.dll file in dnSpyEx again to generate the final output.)
