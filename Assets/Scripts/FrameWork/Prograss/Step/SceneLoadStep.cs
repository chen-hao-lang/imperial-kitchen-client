using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace LiteGameFramework
{
    public class SceneLoadStep : ProgressStep
    {
        private const float ProgressCeiling = 0.9f;

        public float MinHoldSeconds = 0.3f;

        /// <summary>
        /// 允许超时的最长时间
        /// </summary>
        public float LoadTimeout = 120f;

        public SceneHandle Handle { get; private set; }

        private readonly string location;
        private readonly string packageName;

        public SceneLoadStep(string _location, string _packName, string _title = "LoadigScene") : base(_title)
        {
            location = _location;
            packageName = _packName;
        }

        public IEnumerator Run()
        {
            var package = YooAssetsLoad.Instance.GetPackage(packageName);
            if (package == null)
            {
                Fail($"资源包不存在 {packageName} (是否忘记初始化资源包？)");
                yield break;
            }

            Handle = package.LoadSceneAsync(
                location,
                LoadSceneMode.Single,
                LocalPhysicsMode.None,
                allowSceneActivation: false
            );

            if (Handle == null)
            {
                Fail($"场景加载启动失败：{location}");
                yield break;
            }

            // 真实加载
            float startTime = Time.realtimeSinceStartup;
            while (Handle.Progress < ProgressCeiling - 0.001f)
            {
                // 操作失败
                if (Handle.Status == EOperationStatus.Failed)
                {
                    Fail(Handle.Error);
                    Handle.Release();
                    yield break;
                }

                // 加载时间超过 限制
                if (Time.realtimeSinceStartup - startTime > LoadTimeout)
                {
                    Fail($"场景加载超时：{location}");
                    // 释放句柄
                    Handle.Release();
                    yield break;
                }

                Report(Handle.Progress / ProgressCeiling * 0.9f);
                yield return null;
            }

            Report(0.9f);

            float holdStart = Time.realtimeSinceStartup;
            // 最低停留
            while (Time.realtimeSinceStartup - holdStart < MinHoldSeconds)
                yield return null;

            // 激活场景
            Handle.AllowSceneActivation();
            while (!Handle.IsDone)
                yield return null;

            if (Handle.Status == EOperationStatus.Failed)
            {
                Fail(Handle.Error);
                Handle.Release();
                yield break;
            }

            // 🔴 场景已激活（Awake/OnEnable 已执行，Start 在本帧末执行）即视为加载完成。
            //    原实现此处等待 OnSceneReady 事件，但该事件全项目无人触发 →
            //    每次切场景都白等满 ReadyTimeout(15s)，进度条在 90%~99% 空转。
            //    场景自身的面板预加载属于"场景初始化"，不应计入加载进度。
            yield return null;

            Complete();
        }
    }
}