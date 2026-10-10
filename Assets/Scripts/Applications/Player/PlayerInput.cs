using System;
using LiteGameFramework;
using UnityEngine;
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
        _playerAction.Player.Enable();

        _playerAction.Player.Move.performed += Move;
        _playerAction.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        _playerAction.Player.Interact.performed += ctx => EventManager.Invoke(new OnInteractActionEvent());
        _playerAction.Player.InteractAlternate.performed += ctx => EventManager.Invoke(new OnInteractAlternateEvent());
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

    public void RebindBinding(Binding binding, Action onActionRebound)
    {
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

        inputAction.PerformInteractiveRebinding(bindingIndex)
            .OnComplete(callback =>
            {
                callback.Dispose();
                _playerAction.Player.Enable();
                onActionRebound();

                PlayerPrefsManager.SetString(PLAYER_PREFS_BINDINGS, _playerAction.SaveBindingOverridesAsJson());
                PlayerPrefsManager.Save();

                EventManager.Invoke<OnBindingRebindEvent>(new OnBindingRebindEvent());
            })
            .Start();
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