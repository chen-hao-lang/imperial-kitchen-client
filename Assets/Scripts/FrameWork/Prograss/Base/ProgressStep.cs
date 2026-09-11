using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// 加载阶段基类
    /// </summary>
    public class ProgressStep : IProgressStep
    {
        public string Title {get ;protected set;}

        public string Detail {get ;protected set;}

        public float LocalProgress {get ;protected set;}

        public bool IsDone {get ;protected set;}

        public bool IsFailed {get ;protected set;}

        public string Error {get ;protected set;}

        public ProgressStep(string _title,string _detail = null)
        {
            Title = _title;
            Detail = _detail;
        }

        /// <summary>
        /// 上报当前阶段的加载进度
        /// </summary>
        /// <param name="_prograss"></param>
        public void Report(float _prograss)
        {
            if(IsDone || IsFailed) return;
            LocalProgress = Mathf.Clamp01(Mathf.Max(LocalProgress,_prograss));
        }

        /// <summary>
        /// 设置加载副文本
        /// </summary>
        /// <param name="_detail">文本内容</param>
        public void SetDetail(string _detail) => Detail = _detail;

        /// <summary>
        /// 设置加载的主文案
        /// </summary>
        /// <param name="_title">文案内容，如“正在加载中。。。”</param>
        public void SetTitle(string _title) => Title = _title;

        /// <summary>
        /// 加载完成
        /// </summary>
        public void Complete()
        {
            if(IsFailed) return;
            LocalProgress = 1f;
            IsDone = true;
        }

        /// <summary>
        /// 加载失败
        /// </summary>
        /// <param name="_error">失败原因</param>
        public void Fail(string _error)
        {
            IsFailed = true;
            Error = _error;
        }
    }
}