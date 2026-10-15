using System;
using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionPanel : BasePanel
{
    [Header("Button")] [SerializeField] private Button soundEffectsButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button moveUpButton;
    [SerializeField] private Button moveDownButton;
    [SerializeField] private Button moveLeftButton;
    [SerializeField] private Button moveRightButton;
    [SerializeField] private Button interactButton;
    [SerializeField] private Button interactAlternateButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button gamepadInteractButton;
    [SerializeField] private Button gamepadInteractAlternateButton;
    [SerializeField] private Button gamepadPauseButton;

    [Header("Text")] [SerializeField] private TextMeshProUGUI soundEffectsText;
    [SerializeField] private TextMeshProUGUI musicText;
    [SerializeField] private TextMeshProUGUI moveUpText;
    [SerializeField] private TextMeshProUGUI moveDownText;
    [SerializeField] private TextMeshProUGUI moveLeftText;
    [SerializeField] private TextMeshProUGUI moveRightText;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private TextMeshProUGUI interactAlternateText;
    [SerializeField] private TextMeshProUGUI pauseText;
    [SerializeField] private TextMeshProUGUI gamepadInteractText;
    [SerializeField] private TextMeshProUGUI gamepadInteractAlternateText;
    [SerializeField] private TextMeshProUGUI gamepadPauseText;
    [SerializeField] private RectTransform pressToRebindKeyTransform;

    /// <summary> 本地玩家生成事件处理器（保持同一委托实例以便退订） </summary>
    private Action<OnLocalPlayerSpawned> onLocalPlayerSpawnedHandler;

    public override void LoadPanel()
    {
        base.LoadPanel();

        HidePressToRebindKey();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();

        soundEffectsButton.onClick.AddListener(() =>
        {
            //TODO
            //SoundManager.Instance.ChangeVolume();
            UpdateVisual();
        });
        musicButton.onClick.AddListener(() =>
        {
            //TODO
            //MusicManager.Instance.ChangeVolume();
            UpdateVisual();
        });

        closeButton.onClick.AddListener(() => { HidePanel(); });


        moveUpButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Move_Up); });
        moveDownButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Move_Down); });
        moveLeftButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Move_Left); });
        moveRightButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Move_Right); });
        interactButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Interact); });
        interactAlternateButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.InteractAlternate); });
        pauseButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Pause); });
        gamepadInteractButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Gamepad_Interact); });
        gamepadInteractAlternateButton.onClick.AddListener(() =>
        {
            RebindBinding(PlayerInput.Binding.Gamepad_InteractAlternate);
        });
        gamepadPauseButton.onClick.AddListener(() => { RebindBinding(PlayerInput.Binding.Gamepad_Pause); });

        KitchenGameManager.Instance.OnGameUnpaused += KitchenGameManager_OnGameUnpaused;

        // 通过 EventManager 订阅本地玩家生成事件，实现解耦
        onLocalPlayerSpawnedHandler = _ => UpdateVisual();
        EventManager.AddListener<OnLocalPlayerSpawned>(this, onLocalPlayerSpawnedHandler);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        KitchenGameManager.Instance.OnGamePaused -= KitchenGameManager_OnGameUnpaused;

        if (onLocalPlayerSpawnedHandler != null)
        {
            EventManager.RemoveListener<OnLocalPlayerSpawned>(this, onLocalPlayerSpawnedHandler);
            onLocalPlayerSpawnedHandler = null;
        }
    }

    public override void OpenPanel()
    {
        base.OpenPanel();

        soundEffectsButton.Select();
    }

    private void KitchenGameManager_OnGameUnpaused()
    {
        HidePanel();
    }

    private void UpdateVisual()
    {
        //TODO
        //soundEffectsText.text = "Sound Effects: " + Mathf.Round(SoundManager.Instance.GetVolume() * 10f);
        //musicText.text = "Music: " + Mathf.Round(MusicManager.Instance.GetVolume() * 10f);

        moveUpText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Move_Up);
        moveDownText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Move_Down);
        moveLeftText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Move_Left);
        moveRightText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Move_Right);
        interactText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Interact);
        interactAlternateText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.InteractAlternate);
        pauseText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Pause);
        gamepadInteractText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Gamepad_Interact);
        gamepadInteractAlternateText.text =
            PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Gamepad_InteractAlternate);
        gamepadPauseText.text = PlayerInput.Instance.GetBindingText(PlayerInput.Binding.Gamepad_Pause);
    }

    private void ShowPressToRebindKey()
    {
        if (pressToRebindKeyTransform == null)
            return;
        pressToRebindKeyTransform.gameObject.SetActive(true);
    }

    private void HidePressToRebindKey()
    {
        if (pressToRebindKeyTransform == null)
            return;
        pressToRebindKeyTransform.gameObject.SetActive(false);
    }

    private void RebindBinding(PlayerInput.Binding binding)
    {
        ShowPressToRebindKey();
        PlayerInput.Instance.RebindBinding(binding, () =>
        {
            HidePressToRebindKey();
            UpdateVisual();
        });
    }
}