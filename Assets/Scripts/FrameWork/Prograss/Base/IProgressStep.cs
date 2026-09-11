namespace LiteGameFramework
{
    public interface IProgressStep
    {
        /// <summary>
        /// 加载文本
        /// </summary>
        string Title{get;}

        /// <summary>
        /// 加载副文本
        /// </summary>
        string Detail{get;}

        /// <summary>
        /// 加载进度
        /// </summary>
        float LocalProgress{get;}

        /// <summary>
        /// 加载是否完成
        /// </summary>
        bool IsDone {get;}

        /// <summary>
        /// 加载是否失败
        /// </summary>
        bool IsFailed{get;}

        /// <summary>
        /// 加载失败原因
        /// </summary>
        string Error{get;}
    }
}