using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace UnityEditor.Build.Pipeline.Tasks
{
    public static class SBPBuildTasks
    {
        public static IList<IBuildTask> Create(string builtInShaderBundleName, string monoScriptsBundleName)
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

            // Player Scripts
            buildTasks.Add(new BuildPlayerScripts());
            buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
            buildTasks.Add(new CalculateCustomDependencyData());
#endif
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            // 注意：不要使用 CreateBuiltInShadersBundle（SBP 2.1.5 存在缺陷）。
            // 该任务在构建内容不包含内置着色器引用时，会无条件读回可选的
            // IBundleExplictObjectLayout 上下文对象并抛出异常：
            //   "Object of Type IBundleExplictObjectLayout was not available within the BuildContext"
            // 其内部实际逻辑就是 CreateBuiltInBundle，直接使用后者行为完全等价且无此缺陷。
            if (string.IsNullOrEmpty(builtInShaderBundleName) == false)
                buildTasks.Add(new CreateBuiltInBundle(builtInShaderBundleName));
            if (string.IsNullOrEmpty(monoScriptsBundleName) == false)
                buildTasks.Add(new CreateMonoScriptBundle(monoScriptsBundleName));
            buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            buildTasks.Add(new PostPackingCallback());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            buildTasks.Add(new AppendBundleHash());
            buildTasks.Add(new GenerateLinkXml());
            buildTasks.Add(new PostWritingCallback());

            return buildTasks;
        }
    }
}