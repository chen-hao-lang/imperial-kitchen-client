using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 加载进度快照（信息）
    /// </summary>
    public readonly struct ProgressReport
    {
        /// <summary>
        /// 显示进度
        /// </summary>
        public readonly float Progress;

        /// <summary>
        /// 主标题
        /// </summary>
        public readonly string Title;

        /// <summary>
        /// 副标题
        /// </summary>
        public readonly string Detail;

        /// <summary>
        /// 是否结束
        /// </summary>
        public readonly bool IsFinished;

        /// <summary>
        /// 是否失败
        /// </summary>
        public readonly bool IsFailed;

        public ProgressReport(float _progress,string _title,string _detail,bool _isFinshed,bool _isFailed)
        {
            Progress = _progress;
            Title = _title;
            Detail = _detail;
            IsFinished = _isFinshed;
            IsFailed = _isFailed;
        }

        /// <summary>
        /// 加载百分比
        /// </summary>
        public int Percent => Mathf.Clamp(Mathf.FloorToInt(Progress * 100f),0,100);
    }
}