using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgressPanel : BasePanel
{
    [SerializeField ] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailText;

    public override void OnAddListener()
    {
        base.OnAddListener();

        EventManager.AddListener<Changed>(this,OnProgressChanger);

        OnProgressChanger(new Changed(ProgressHub.Instance.CurrentReport));
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        EventManager.RemoveAllListener(this);
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        if(ProgressHub.HasInstance)
            OnProgressChanger(new Changed(ProgressHub.Instance.CurrentReport));
    }

    private void OnProgressChanger(Changed changed)
    {
        if(progressSlider != null) progressSlider.value = changed.progressReport.Progress;
        if(percentText != null) percentText.text = changed.progressReport.Percent.ToString();
        if(titleText != null) titleText.text = changed.progressReport.Title;
        if(detailText != null) detailText.text = changed.progressReport.Detail;
    }
}
