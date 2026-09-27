using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Web (Yandex Games) release settings, adapted from the optimize-web skill template.
    /// Decompression Fallback is ON because the hosting's Content-Encoding headers are not confirmed yet;
    /// switch it off once the server is known to send "Content-Encoding: br".
    /// </summary>
    public static class WebOptimizer
    {
        private const int c_InitialMemoryMb = 256;
        private const int c_MaximumMemoryMb = 2048;

        [MenuItem("Tools/Apply Web Release Settings")]
        public static void Optimize()
        {
            var target = NamedBuildTarget.WebGL;
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
            PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.stripUnusedMeshComponents = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            // Off: WebAssembly 2023 features need Safari 16.4+ - older iPhones (iOS 15 / early 16) would not start.
            PlayerSettings.WebGL.wasm2023 = false;
            // Rustwave template: full-window canvas, no page scroll / long-press menu, capped pixel ratio on phones.
            PlayerSettings.WebGL.template = "PROJECT:Rustwave";
            PlayerSettings.WebGL.initialMemorySize = c_InitialMemoryMb;
            PlayerSettings.WebGL.maximumMemorySize = c_MaximumMemoryMb;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;

            AssetDatabase.SaveAssets();

            Debug.Log(
                "Web release settings applied and saved:\n"
                + $"  il2cppCodeGeneration = {PlayerSettings.GetIl2CppCodeGeneration(target)}\n"
                + $"  managedStrippingLevel = {PlayerSettings.GetManagedStrippingLevel(target)}\n"
                + $"  apiCompatibilityLevel = {PlayerSettings.GetApiCompatibilityLevel(target)}\n"
                + $"  stripEngineCode = {PlayerSettings.stripEngineCode}\n"
                + $"  stripUnusedMeshComponents = {PlayerSettings.stripUnusedMeshComponents}\n"
                + $"  dataCaching = {PlayerSettings.WebGL.dataCaching}\n"
                + $"  compressionFormat = {PlayerSettings.WebGL.compressionFormat}\n"
                + $"  decompressionFallback = {PlayerSettings.WebGL.decompressionFallback}\n"
                + $"  exceptionSupport = {PlayerSettings.WebGL.exceptionSupport}\n"
                + $"  debugSymbolMode = {PlayerSettings.WebGL.debugSymbolMode}\n"
                + $"  wasm2023 = {PlayerSettings.WebGL.wasm2023}\n"
                + $"  template = {PlayerSettings.WebGL.template}\n"
                + $"  memory = {PlayerSettings.WebGL.initialMemorySize}..{PlayerSettings.WebGL.maximumMemorySize} MB {PlayerSettings.WebGL.memoryGrowthMode}\n"
                + $"  codeOptimization = {UnityEditor.WebGL.UserBuildSettings.codeOptimization}");
        }
    }
}
