using System.Collections;
using System.Collections.Generic;
using LiteGameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanel : BasePanel
{
    [SerializeField] private RectTransform inputLabel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [SerializeField] private RectTransform messageLabel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Scrollbar scrollbar;
    private bool isPlayeOpen;

    /// <summary> 聊天消息历史，用于滚动查看 </summary>
    private readonly List<string> messageHistory = new List<string>();

    /// <summary> 自动关闭 messageLabel 的协程 </summary>
    private Coroutine autoCloseCoroutine;

    /// <summary> 收到消息后自动关闭的时长（秒） </summary>
    private const float MESSAGE_SHOW_DURATION = 1f;

    public override void LoadPanel()
    {
        base.LoadPanel();

        // 初始隐藏输入框和消息框
        inputLabel.gameObject.SetActive(false);
        messageLabel.gameObject.SetActive(false);
    }

    public override void OnAddListener()
    {
        base.OnAddListener();

        sendButton.onClick.AddListener(SendMessage);
        scrollbar.onValueChanged.AddListener(OnScrollbarDrag);

        EventManager.AddListener<OnNetworkMessage>(this, OnReciveMessage);

        // PlayerInput 挂在本地玩家预制体上，由 PlayerSpawner 在连接成功后才生成，
        // 所以这里先订阅 OnLocalPlayerSpawned，等玩家生成后再订阅 PlayerInput 的事件
        PlayerSpawner.Instance.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();

        sendButton.onClick.RemoveAllListeners();
        scrollbar.onValueChanged.RemoveAllListeners();

        if (NetworkManager.Instance != null)
        {
            EventManager.RemoveListener<OnNetworkMessage>(this, OnReciveMessage);
        }
        if (PlayerSpawner.Instance != null)
        {
            PlayerSpawner.Instance.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
        }
        if (PlayerInput.Instance != null)
        {
            PlayerInput.Instance.OnOpenChatPanel -= OpenOrClosePanel;
        }
    }

    /// <summary>
    /// 本地玩家生成后才会存在 PlayerInput，此时再订阅其事件
    /// </summary>
    private void OnLocalPlayerSpawned()
    {
        if (PlayerInput.Instance != null)
        {
            PlayerInput.Instance.OnOpenChatPanel += OpenOrClosePanel;
        }
    }

    private void OpenOrClosePanel(bool obj)
    {
        if (obj)
        {
            OpenInputLabel();
        }
        else
        {
            CloseInputLabel();
        }
    }

    private void SendMessage()
    {
        // 更新messageLabel
        // 发送消息给服务器
        string content = inputField.text;
        if (string.IsNullOrWhiteSpace(content)) return;

        int playerId = NetworkManager.Instance.PlayerID;

        // 本地更新messageLabel（自己的消息由服务器转发给其他玩家，不会回显到自己）
        AppendMessage(playerId, content);

        // 发送消息给服务器
        NetworkManager.Instance.SendMessage(playerId, content);

        // 清空输入框，保持焦点方便连续发送
        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    private void OnScrollbarDrag(float _value)
    {
        // 根据_value大小调整messageText的显示信息
        // Scrollbar 约定：value=0 位于底部（显示最新消息），value=1 位于顶部（显示最旧消息）
        if (messageText == null) return;

        float contentHeight = messageText.preferredHeight;
        float viewHeight = messageLabel.rect.height;

        // 内容不足一屏时无需滚动
        if (contentHeight <= viewHeight) return;

        // 上下移动messageText来显示不同区域
        // （messageText需挂在可裁剪的容器内，建议pivot在顶部、锚点在容器顶部）
        float offset = (contentHeight - viewHeight) * (1f - _value);
        messageText.rectTransform.anchoredPosition = new Vector2(
            messageText.rectTransform.anchoredPosition.x,
            -offset);
    }

    public void OpenInputLabel()
    {
        // 玩家主动打开输入框架
        // 打开时收到消息无法自动关闭
        isPlayeOpen = true;
        inputLabel.gameObject.SetActive(true);
        messageLabel.gameObject.SetActive(true);

        // 停止可能还在运行的自动关闭协程，保证面板保持打开
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    /// <summary>
    /// 关闭输入框架（发送完成后或玩家主动关闭时调用）
    /// </summary>
    public void CloseInputLabel()
    {
        isPlayeOpen = false;
        inputLabel.gameObject.SetActive(false);
        messageLabel.gameObject.SetActive(false);
    }

    public void OnReciveMessage(OnNetworkMessage _arg)
    {
        //打开messageLabel
        //显示消息,显示格式为"Plyer{_playerId}:{_messageContent}"
        //显示1s后自动关闭messageLabel
        AppendMessage(_arg.playerId, _arg.message);

        messageLabel.gameObject.SetActive(true);

        // 玩家打开输入框时不自动关闭，方便边看边输入
        if (isPlayeOpen) return;

        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
        }
        autoCloseCoroutine = StartCoroutine(AutoCloseMessageLabel());
    }

    /// <summary>
    /// 显示1秒后自动关闭messageLabel
    /// </summary>
    private IEnumerator AutoCloseMessageLabel()
    {
        yield return new WaitForSeconds(MESSAGE_SHOW_DURATION);
        messageLabel.gameObject.SetActive(false);
        autoCloseCoroutine = null;
    }

    /// <summary>
    /// 追加一条聊天消息到历史，并刷新messageText显示
    /// </summary>
    private void AppendMessage(int _playerId, string _messageContent)
    {
        string line = $"Player{_playerId}:{_messageContent}";
        messageHistory.Add(line);
        messageText.text = string.Join("\n", messageHistory);

        // 如果滚动条位于底部，则自动滚到最新消息
        if (scrollbar != null && scrollbar.value <= 0.01f)
        {
            OnScrollbarDrag(0f);
        }
    }
}
