using System.Collections;
using LiteGameFramework;
using UnityEngine;

public class ProgressHub : SingletonMono<ProgressHub>
{
    public bool AutoManagePanel = true;

    private ProgressFlow flow;
    private ProgressReport lastReport;
    private float lastBroadcastProgress = -1f;
    private string lastBroadcastTitle;
    private string lastBroadcastDetail;

    public ProgressFlow currentFlow => flow;
    public bool IsRunning => flow != null && !flow.IsFinished;
    public ProgressReport CurrentReport => lastReport;

    public ProgressFlow BeginFlow(string _flowName)
    {
        if(IsRunning)
        {
            flow.ForceComplete();
            Publish(true);
        }

        flow = new ProgressFlow(_flowName);
        lastBroadcastProgress  = -1;
        lastBroadcastTitle = null;
        lastBroadcastDetail = null;

        if(AutoManagePanel)
            StartCoroutine(OpenPanelRoutine());    

        Publish(_force:true);
        return flow;
    }   

    public void EndFlow()
    {
        if(flow == null) return;

        flow.ForceComplete();
        Publish(true);
        flow = null;

        if(AutoManagePanel)
            UIManager.Instance.HidePanel(EUIType.ProgressPanel);
    }

    private void Update()
    {
        if(flow == null )return;

        flow.Tick(Time.unscaledDeltaTime);
        Publish();
    }

    private void Publish(bool _force = false)
    {
        if(flow == null) return;

        float p = flow.DisplayProgress;
        var step = flow.CurrentStep;
        string title = step?.Title ?? string.Empty;
        string detail = step?.Detail ?? string.Empty;

        if(!_force && Mathf.Approximately(p,lastBroadcastProgress)
            && title == lastBroadcastTitle && detail == lastBroadcastDetail)    
            return;
        
        lastBroadcastProgress = p;
        lastBroadcastTitle = title;
        lastBroadcastDetail = detail;

        lastReport = new ProgressReport(p,title,detail,false,false);
        EventManager.Invoke<Changed>(new Changed(lastReport));
    }

    private IEnumerator OpenPanelRoutine()
    {
        yield return UIManager.Instance.OpenPanelAsync<ProgressPanel>(EUIType.ProgressPanel);
    }
}

public struct Changed : IEvent
{
    public ProgressReport progressReport;

    public Changed(ProgressReport _report)
    {
        progressReport = _report;
    }
}
