using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按键设置子面板：显示当前键位、交互式重绑定、恢复默认。
///
/// 数据流：
/// 1. 每次显示（<see cref="OnEnable"/> / <see cref="Init"/>）都从 <see cref="PlayerInput"/> 读实时绑定回显，
///    因此外来修改（例如「恢复默认」）或重启后加载的存档都能立即反映到 UI。
/// 2. 点击某个键位 → <see cref="PlayerInput.RebindBinding"/> 进入监听，成功即写入 PlayerPrefs；
///    按 Esc 取消走同一套收尾逻辑（隐藏遮罩 + 恢复 ActionMap）。
/// 3. 恢复默认 → <see cref="PlayerInput.ResetAllBindings"/> 清除覆盖并删除存档。
/// </summary>
public class ButtonSettingPanel : ChildPanel
{
    [Header("Button")]
    [SerializeField] private Button moveUpButton;
    [SerializeField] private Button moveDownButton;
    [SerializeField] private Button moveLeftButton;
    [SerializeField] private Button moveRightButton;
    [SerializeField] private Button interactButton;
    [SerializeField] private Button interactAlternateButton;
    [SerializeField] private Button gamepadInteractButton;
    [SerializeField] private Button gamepadInteractAlternateButton;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI moveUpText;
    [SerializeField] private TextMeshProUGUI moveDownText;
    [SerializeField] private TextMeshProUGUI moveLeftText;
    [SerializeField] private TextMeshProUGUI moveRightText;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private TextMeshProUGUI interactAlternateText;
    [SerializeField] private TextMeshProUGUI gamepadInteractText;
    [SerializeField] private TextMeshProUGUI gamepadInteractAlternateText;

    [Header("重绑定提示遮罩（可空）")]
    [SerializeField] private RectTransform pressToRebindKeyTransform;

    /// <summary>是否正在等待玩家按键（同一时刻只允许一个重绑定）</summary>
    private bool isRebinding;

    #region 生命周期

    /// <summary>打开该页时回显当前键位</summary>
    private void OnEnable()
    {
        UpdateVisual();
    }

    /// <summary>
    /// 玩家在「等待按键」时切页或关闭面板：必须取消挂起的重绑定，
    /// 否则它会继续吃掉下一次按键输入，玩家一按就被改键。
    /// </summary>
    private void OnDisable()
    {
        if (!isRebinding)
            return;

        isRebinding = false;

        PlayerInput input = PlayerInput.Instance;
        if (input != null)
            input.CancelActiveRebind();
    }

    public override void Init()
    {
        base.Init();
        UpdateVisual();
    }

    public override void AddListener()
    {
        base.AddListener();

        // 用方法组而非闭包：RemoveListener 才能精确移除，避免面板重载后重复绑定
        if (moveUpButton != null) moveUpButton.onClick.AddListener(OnMoveUpClick);
        if (moveDownButton != null) moveDownButton.onClick.AddListener(OnMoveDownClick);
        if (moveLeftButton != null) moveLeftButton.onClick.AddListener(OnMoveLeftClick);
        if (moveRightButton != null) moveRightButton.onClick.AddListener(OnMoveRightClick);
        if (interactButton != null) interactButton.onClick.AddListener(OnInteractClick);
        if (interactAlternateButton != null) interactAlternateButton.onClick.AddListener(OnInteractAlternateClick);
        if (gamepadInteractButton != null) gamepadInteractButton.onClick.AddListener(OnGamepadInteractClick);
        if (gamepadInteractAlternateButton != null)
            gamepadInteractAlternateButton.onClick.AddListener(OnGamepadInteractAlternateClick);

        UpdateVisual();
    }

    public override void RemoveListener()
    {
        base.RemoveListener();

        if (moveUpButton != null) moveUpButton.onClick.RemoveListener(OnMoveUpClick);
        if (moveDownButton != null) moveDownButton.onClick.RemoveListener(OnMoveDownClick);
        if (moveLeftButton != null) moveLeftButton.onClick.RemoveListener(OnMoveLeftClick);
        if (moveRightButton != null) moveRightButton.onClick.RemoveListener(OnMoveRightClick);
        if (interactButton != null) interactButton.onClick.RemoveListener(OnInteractClick);
        if (interactAlternateButton != null) interactAlternateButton.onClick.RemoveListener(OnInteractAlternateClick);
        if (gamepadInteractButton != null) gamepadInteractButton.onClick.RemoveListener(OnGamepadInteractClick);
        if (gamepadInteractAlternateButton != null)
            gamepadInteractAlternateButton.onClick.RemoveListener(OnGamepadInteractAlternateClick);
    }

    #endregion

    #region 对外接口

    /// <summary>恢复默认按键设置（清空全部重绑定覆盖并删除存档）</summary>
    public void ResumeSetting()
    {
        PlayerInput input = PlayerInput.Instance;
        if (input == null)
        {
            Debug.LogWarning("[ButtonSettingPanel] PlayerInput 尚未创建，无法恢复默认按键");
            return;
        }

        // 正在等按键时先取消，避免重置后又被挂起的操作改写
        if (isRebinding)
            input.CancelActiveRebind();

        isRebinding = false;
        HidePressToRebindKey();

        input.ResetAllBindings();
        UpdateVisual();
    }

    #endregion

    #region 按钮回调

    private void OnMoveUpClick() => StartRebind(PlayerInput.Binding.Move_Up, moveUpButton, moveUpText);

    private void OnMoveDownClick() => StartRebind(PlayerInput.Binding.Move_Down, moveDownButton, moveDownText);

    private void OnMoveLeftClick() => StartRebind(PlayerInput.Binding.Move_Left, moveLeftButton, moveLeftText);

    private void OnMoveRightClick() => StartRebind(PlayerInput.Binding.Move_Right, moveRightButton, moveRightText);

    private void OnInteractClick() => StartRebind(PlayerInput.Binding.Interact, interactButton, interactText);

    private void OnInteractAlternateClick() =>
        StartRebind(PlayerInput.Binding.InteractAlternate, interactAlternateButton, interactAlternateText);

    private void OnGamepadInteractClick() =>
        StartRebind(PlayerInput.Binding.Gamepad_Interact, gamepadInteractButton, gamepadInteractText);

    private void OnGamepadInteractAlternateClick() =>
        StartRebind(PlayerInput.Binding.Gamepad_InteractAlternate, gamepadInteractAlternateButton,
            gamepadInteractAlternateText);

    #endregion

    #region 重绑定流程

    private void StartRebind(PlayerInput.Binding _binding, Button _button, TextMeshProUGUI _valueText)
    {
        PlayerInput input = PlayerInput.Instance;
        if (input == null)
        {
            Debug.LogWarning("[ButtonSettingPanel] PlayerInput 尚未创建，无法重绑定按键");
            return;
        }

        // 已有一次重绑定在等待按键：忽略新点击，避免两个操作抢同一个 ActionMap
        if (isRebinding)
            return;

        isRebinding = true;
        ShowPressToRebindKey(_valueText);

        // 成功与取消共用同一套收尾：隐藏提示 + 回显最新键位
        input.RebindBinding(_binding, OnRebindFinished, OnRebindFinished);
    }

    private void OnRebindFinished()
    {
        isRebinding = false;
        HidePressToRebindKey();
        UpdateVisual();
    }

    /// <summary>
    /// 显示「按下按键」提示。
    /// 预制体没配遮罩时降级为直接把该键位的文字改成提示，避免玩家点了没有任何反馈。
    /// </summary>
    private void ShowPressToRebindKey(TextMeshProUGUI _valueText)
    {
        if (pressToRebindKeyTransform != null)
        {
            pressToRebindKeyTransform.gameObject.SetActive(true);
            return;
        }

        if (_valueText != null)
            _valueText.text = "按下按键…";
    }

    private void HidePressToRebindKey()
    {
        if (pressToRebindKeyTransform == null)
            return;

        pressToRebindKeyTransform.gameObject.SetActive(false);
    }

    #endregion

    #region UI 刷新

    /// <summary>把当前实际生效的键位（含存档覆盖）刷到 8 个按钮文字上</summary>
    private void UpdateVisual()
    {
        PlayerInput input = PlayerInput.Instance;
        if (input == null)
            return;

        SetText(moveUpText, input.GetBindingText(PlayerInput.Binding.Move_Up));
        SetText(moveDownText, input.GetBindingText(PlayerInput.Binding.Move_Down));
        SetText(moveLeftText, input.GetBindingText(PlayerInput.Binding.Move_Left));
        SetText(moveRightText, input.GetBindingText(PlayerInput.Binding.Move_Right));
        SetText(interactText, input.GetBindingText(PlayerInput.Binding.Interact));
        SetText(interactAlternateText, input.GetBindingText(PlayerInput.Binding.InteractAlternate));
        SetText(gamepadInteractText, input.GetBindingText(PlayerInput.Binding.Gamepad_Interact));
        SetText(gamepadInteractAlternateText, input.GetBindingText(PlayerInput.Binding.Gamepad_InteractAlternate));
    }

    private static void SetText(TextMeshProUGUI _text, string _value)
    {
        if (_text != null)
            _text.text = _value;
    }

    #endregion
}
