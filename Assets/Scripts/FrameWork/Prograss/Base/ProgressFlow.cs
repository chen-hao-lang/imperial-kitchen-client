using System.Collections.Generic;
using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 加载流程
    /// </summary>
    public class ProgressFlow
    {
        private class Segment
        {
            public IProgressStep Step;
            public float Weight;
        }

        private readonly List<Segment> segments = new List<Segment>();
        private float lastRawSample = -1f;
        private float displayProgress;

        /// <summary>
        /// 每秒最大涨幅（平滑速度）。越小越顺滑，越大越"诚实"
        /// </summary>
        public float SmoothSpeed = 1.2f;

        /// <summary>
        /// 真实进度停滞时，显示进度允许的漂移速度（每秒）
        /// </summary>
        public float IdleDriftSpeed = 0.01f;

        /// <summary>
        /// 显示进度最多领先真实进度的幅度（"假进度"上限）
        /// </summary>
        public float MaxDriftAhead = 0.02f;

        /// <summary>
        /// 流程名（调试用）
        /// </summary>
        public string FlowName { get; }

        /// <summary>
        /// 真实进度（0~1，未平滑）
        /// </summary>
        public float RawProgress { get; private set; }

        /// <summary>
        /// 显示进度（0~1）
        /// </summary>
        public float DisplayProgress => displayProgress;

        /// <summary>
        /// 是否全部完成
        /// </summary>
        public bool IsFinished { get; private set; }

        /// <summary>
        /// 是否有任意阶段失败
        /// </summary>
        public bool IsFailed { get; private set; }

        /// <summary>
        /// 是否发生过失败（取第一个失败阶段的原因）
        /// </summary>
        public string Error { get; private set; }

        public ProgressFlow(string flowName) => FlowName = flowName;

        /// <summary>
        /// 注册一个阶段并返回它，便于调用方持有引用并上报进度。
        /// </summary>
        /// <param name="step">阶段实例</param>
        /// <param name="weight">相对时长预估权重，例如下载 3、场景 2、预加载 1</param>
        public T AddStep<T>(T step, float weight = 1f) where T : IProgressStep
        {
            segments.Add(new Segment { Step = step, Weight = Mathf.Max(0.0001f, weight) });
            return step;
        }

        /// <summary>运行时调整某阶段的权重（发现预估离谱时可修正，例如下载比预期长很多）</summary>
        public void SetWeight(IProgressStep step, float weight)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (ReferenceEquals(segments[i].Step, step))
                {
                    segments[i].Weight = Mathf.Max(0.0001f, weight);
                    return;
                }
            }
        }

        /// <summary>当前正在推进的阶段（第一个未完成且未失败的阶段）</summary>
        public IProgressStep CurrentStep
        {
            get
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (!segments[i].Step.IsDone && !segments[i].Step.IsFailed)
                        return segments[i].Step;
                }
                return segments.Count > 0 ? segments[segments.Count - 1].Step : null;
            }
        }

        /// <summary>每帧驱动：真实进度 → 平滑 → 显示进度</summary>
        public void Tick(float deltaTime)
        {
            if (IsFinished) return;

            float raw = EvaluateRaw();
            RawProgress = raw;

            bool allDone = segments.Count > 0;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.Step.IsFailed && !IsFailed)
                {
                    IsFailed = true;
                    Error = seg.Step.Error;
                }
                if (!seg.Step.IsDone) allDone = false;
            }
            IsFinished = allDone;

            float target = raw;
            bool stalled = raw <= lastRawSample + 0.0001f;
            lastRawSample = raw;

            if (IsFinished)
            {
                target = 1f;
            }
            else if (stalled)
            {
                // 真实进度停滞（下载卡在大文件、场景加载卡在 0.9）：
                // 允许以极慢速度漂移，但最多领先真实进度 MaxDriftAhead，避免"假得太离谱"
                target = Mathf.Min(raw + MaxDriftAhead, displayProgress + IdleDriftSpeed * deltaTime);
            }

            // 单调不减 + 限速追赶
            displayProgress = Mathf.Clamp01(
                Mathf.MoveTowards(displayProgress, Mathf.Max(target, displayProgress), SmoothSpeed * deltaTime));
        }

        /// <summary>强制结束（跳过动画，直接拉满）</summary>
        public void ForceComplete()
        {
            IsFinished = true;
            displayProgress = 1f;
            RawProgress = 1f;
        }

        private float EvaluateRaw()
        {
            if (segments.Count == 0) return 0f;

            float totalWeight = 0f;
            float sum = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                totalWeight += seg.Weight;
                float p = seg.Step.IsDone ? 1f : Mathf.Clamp01(seg.Step.LocalProgress);
                sum += seg.Weight * p;
            }
            return totalWeight <= 0f ? 0f : sum / totalWeight;
        }
    }
}