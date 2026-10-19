using System;
using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance;
    private const string PLAYER_PREFS_BINDINGS = "InputBindings";

    public enum Binding
    {
        Move_Up,
        Move_Down,
        Move_Left,
        Move_Right,
        Interact,
        InteractAlternate,
        Pause,
        Gamepad_Interact,
        Gamepad_InteractAlternate,
        Gamepad_Pause
    }

    private PlayerAction _playerAction;

    /// <summary>当前正在进行的一次重绑定（同一时刻只允许一个）</summary>
    private InputActionRebindingExtensions.RebindingOperation _activeRebind;

    private Vector2 moveInput;
    public Vector2 MoveInput => moveInput;

    public Vector2 mousePos;

    public Vector2 MouseScreenPositionNormalized => new Vector2(
        (mousePos.x / (float)Screen.width - 0.5f) * 2f,
        (mousePos.y / (float)Screen.height - 0.5f) * 2f
    );

    private bool openChatPanel = false;

    void Awake()
    {
        Instance = this;

        _playerAction = new PlayerAction();

        // 🔴 必须先恢复存档的按键覆盖，再 Enable：
        //    否则重启游戏后玩家自定义的键位会失效（原实现只 Save、从不 Load）。
        LoadBindingOverrides();

        _playerAction.Player.Enable();

        _playerAction.Player.Move.performed += Move;
        _playerAction.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        _playerAction.Player.Interact.performed += ctx => EventManager.Invoke(new OnInteractActionEvent());
        _playerAction.Player.InteractAlternate.performed += ctx => EventManager.Invoke(new OnInteractAlternateEvent());
        
        // 暂停键
        _playerAction.Player.Pause.performed += ctx => EventManager.Invoke(new OnPauseActionEvent());

        _playerAction.Player.Mouse.performed += ctx => mousePos = ctx.ReadValue<Vector2>();

        _playerAction.Player.OpenChatPanel.performed += ctx =>
        {
            openChatPanel = !openChatPanel;
            EventManager.Invoke(new OnOpenChatPanelEvent { IsOpen = openChatPanel });
        };
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_playerAction != null)
        {
            _playerAction.Player.Disable();
            _playerAction.Dispose();
            _playerAction = null;
        }
    }

    private void Move(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = input.normalized;
    }

    /// <summary>
    /// 当前焦点是否落在 UI 输入框里。
    ///
    /// 🔴 为什么需要：InputSystem 不会因为 <see cref="TMP_InputField"/> 获得焦点就屏蔽普通 Action，
    ///    玩家在聊天框打「Esc 之外的任意键」没问题，但按 Esc 会同时被 Pause 动作捕获，
    ///    导致"想取消输入"变成"打开暂停菜单"。所有订阅 Pause 事件的地方都应先过这道判断。
    /// </summary>
    public static bool IsTypingInInputField()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected.GetComponent<TMP_InputField>() != null;
    }

    #region 按键绑定的持久化

    /// <summary>
    /// 从 PlayerPrefs 读取重绑定覆盖并应用到 <see cref="PlayerAction"/>。
    /// 存档损坏时直接丢弃，回落到默认键位，避免整局游戏无法操作。
    /// </summary>
    public void LoadBindingOverrides()
    {
        string json = PlayerPrefsManager.GetString(PLAYER_PREFS_BINDINGS, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            _playerAction.LoadBindingOverridesFromJson(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerInput] 按键绑定存档解析失败，已回落到默认键位：{e.Message}");
            PlayerPrefsManager.DeleteKey(PLAYER_PREFS_BINDINGS);
        }
    }

    /// <summary>
    /// 恢复全部默认键位：清除运行时覆盖 + 删除存档，并广播重绑定事件让 UI 刷新。
    /// </summary>
    public void ResetAllBindings()
    {
        if (_playerAction == null)
            return;

        _playerAction.Player.Disable();
        _playerAction.RemoveAllBindingOverrides();
        _playerAction.Player.Enable();

        PlayerPrefsManager.DeleteKey(PLAYER_PREFS_BINDINGS);
        PlayerPrefsManager.Save();

        EventManager.Invoke<OnBindingRebindEvent>(new OnBindingRebindEvent());
    }

    #endregion

    public string GetBindingText(Binding binding)
    {
        switch (binding)
        {
            default:
            case Binding.Move_Up:
                return _playerAction.Player.Move.bindings[1].ToDisplayString();
            case Binding.Move_Down:
                return _playerAction.Player.Move.bindings[2].ToDisplayString();
            case Binding.Move_Left:
                return _playerAction.Player.Move.bindings[3].ToDisplayString();
            case Binding.Move_Right:
                return _playerAction.Player.Move.bindings[4].ToDisplayString();
            case Binding.Interact:
                return _playerAction.Player.Interact.bindings[0].ToDisplayString();
            case Binding.InteractAlternate:
                return _playerAction.Player.InteractAlternate.bindings[0].ToDisplayString();
            case Binding.Pause:
                return _playerAction.Player.Pause.bindings[0].ToDisplayString();
            case Binding.Gamepad_Interact:
                return _playerAction.Player.Interact.bindings[1].ToDisplayString();
            case Binding.Gamepad_InteractAlternate:
                return _playerAction.Player.InteractAlternate.bindings[1].ToDisplayString();
            case Binding.Gamepad_Pause:
                return _playerAction.Player.Pause.bindings[1].ToDisplayString();
        }
    }

    /// <summary>
    /// 交互式重绑定指定按键。
    /// </summary>
    /// <param name="binding">要重绑定的逻辑键位</param>
    /// <param name="onActionRebound">成功回调（已保存）</param>
    /// <param name="onActionCanceled">
    /// 取消/失败回调。🔴 必须处理：重绑定开始时会 Disable 整个 ActionMap，
    /// 若玩家按 Esc 取消而这里不恢复，会永久失去输入。
    /// </param>
    public void RebindBinding(Binding binding, Action onActionRebound, Action onActionCanceled = null)
    {
        if (_playerAction == null)
        {
            onActionCanceled?.Invoke();
            return;
        }

        // 同一时刻只允许一个重绑定：上一次还没结束时先取消，避免两个操作抢同一个 ActionMap
        CancelActiveRebind();

        _playerAction.Player.Disable();

        InputAction inputAction;
        int bindingIndex;

        switch (binding)
        {
            default:
            case Binding.Move_Up:
                inputAction = _playerAction.Player.Move;
                bindingIndex = 1;
                break;
            case Binding.Move_Down:
                inputAction = _playerAction.Player.Move;
                bindingIndex = 2;
                break;
            case Binding.Move_Left:
                inputAction = _playerAction.Player.Move;
                bindingIndex = 3;
                break;
            case Binding.Move_Right:
                inputAction = _playerAction.Player.Move;
                bindingIndex = 4;
                break;
            case Binding.Interact:
                inputAction = _playerAction.Player.Interact;
                bindingIndex = 0;
                break;
            case Binding.InteractAlternate:
                inputAction = _playerAction.Player.InteractAlternate;
                bindingIndex = 0;
                break;
            case Binding.Pause:
                inputAction = _playerAction.Player.Pause;
                bindingIndex = 0;
                break;
            case Binding.Gamepad_Interact:
                inputAction = _playerAction.Player.Interact;
                bindingIndex = 1;
                break;
            case Binding.Gamepad_InteractAlternate:
                inputAction = _playerAction.Player.InteractAlternate;
                bindingIndex = 1;
                break;
            case Binding.Gamepad_Pause:
                inputAction = _playerAction.Player.Pause;
                bindingIndex = 1;
                break;
        }

        try
        {
            _activeRebind = inputAction.PerformInteractiveRebinding(bindingIndex)
                // 排除鼠标指针类控制，避免轻微移动鼠标就把键位绑成 Mouse/position
                .WithControlsExcluding("Mouse/position")
                .WithControlsExcluding("Mouse/delta")
                // 明确 Esc 为取消键（同时意味着 Pause 键不能绑到 Esc）
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(callback =>
                {
                    _activeRebind = null;
                    callback.Dispose();
                    _playerAction.Player.Enable();

                    // 先保存再回调：UI 刷新时读到的就是最新绑定
                    PlayerPrefsManager.SetString(PLAYER_PREFS_BINDINGS, _playerAction.SaveBindingOverridesAsJson());
                    PlayerPrefsManager.Save();

                    onActionRebound?.Invoke();
                    EventManager.Invoke<OnBindingRebindEvent>(new OnBindingRebindEvent());
                })
                .OnCancel(callback =>
                {
                    _activeRebind = null;
                    callback.Dispose();
                    _playerAction.Player.Enable();
                    onActionCanceled?.Invoke();
                });

            _activeRebind.Start();
        }
        catch (Exception e)
        {
            // 兜底：任何异常都不能让 ActionMap 停在 Disable 状态
            Debug.LogError($"[PlayerInput] 重绑定 {binding} 失败：{e}");
            _activeRebind = null;
            _playerAction.Player.Enable();
            onActionCanceled?.Invoke();
        }
    }

    /// <summary>
    /// 取消正在进行的重绑定（面板被关闭 / 切页时调用）。
    /// 取消会触发 <c>OnCancel</c>，从而恢复 ActionMap 并回调面板。
    /// </summary>
    public void CancelActiveRebind()
    {
        if (_activeRebind == null)
            return;

        InputActionRebindingExtensions.RebindingOperation operation = _activeRebind;
        _activeRebind = null;

        try
        {
            operation.Cancel();
        }
        catch (Exception e)
        {
            // 操作可能已自行结束并 Dispose，这里只记录，避免影响调用方
            Debug.LogWarning($"[PlayerInput] 取消重绑定时出现异常（可忽略）：{e.Message}");
        }
    }
}

/// <summary>
/// 按键绑定被重新绑定事件（无参数）
/// </summary>
public struct OnBindingRebindEvent : IEvent { }

/// <summary>
/// 玩家按下 Interact 键事件（无参数）
/// </summary>
public struct OnInteractActionEvent : IEvent { }

/// <summary>
/// 玩家按下 InteractAlternate 键事件（无参数）
/// </summary>
public struct OnInteractAlternateEvent : IEvent { }

/// <summary>
/// 聊天面板打开/关闭事件
/// </summary>
public struct OnOpenChatPanelEvent : IEvent
{
    /// <summary>true=打开面板，false=关闭面板</summary>
    public bool IsOpen;
}

/// <summary>
/// 玩家按下 Pause 键事件（无参数）
/// </summary>
public struct OnPauseActionEvent : IEvent { }