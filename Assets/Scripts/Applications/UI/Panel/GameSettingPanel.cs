using LiteGameFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏设置子面板：音乐与音效音量。
/// </summary>
public class GameSettingPanel : ChildPanel
{
    /// <summary>拖动过程中两次落盘的最小间隔（秒）。用 unscaledTime，暂停时（timeScale=0）仍然有效。</summary>
    private const float SAVE_INTERVAL = 0.3f;

    [Header("Slider")]
    [SerializeField] private Slider musicVolume;
    [SerializeField] private Slider soundVolume;

    /// <summary>上一次落盘的时间</summary>
    private float lastSaveTime;

    #region 生命周期

    /// <summary>
    /// 每次切到该页（SetActive(true)）都重新回显，
    /// 避免在别处改过音量后 UI 仍显示旧值。
    /// </summary>
    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>切页 / 关闭面板时兜底落盘</summary>
    private void OnDisable()
    {
        SaveVolumeSettings();
    }

    /// <summary>退出游戏时兜底落盘（防止只写了内存没 Save）</summary>
    private void OnApplicationQuit()
    {
        SaveVolumeSettings();
    }

    public override void Init()
    {
        base.Init();
        Refresh();
    }

    public override void AddListener()
    {
        base.AddListener();

        // 预制体尚未挂载脚本 / 未拖引用时，不要让整个面板在 OnAddListener 阶段抛 NRE
        if (musicVolume != null)
            musicVolume.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (soundVolume != null)
            soundVolume.onValueChanged.AddListener(OnSoundVolumeChanged);
    }

    public override void RemoveListener()
    {
        base.RemoveListener();

        if (musicVolume != null)
            musicVolume.onValueChanged.RemoveListener(OnMusicVolumeChanged);

        if (soundVolume != null)
            soundVolume.onValueChanged.RemoveListener(OnSoundVolumeChanged);
    }

    #endregion

    #region 对外接口

    /// <summary>
    /// 恢复默认设置：音乐 50%、音效 50%。
    /// 直接作用于 AudioManager 并立即落盘，不依赖 Slider 的回调
    /// （Slider 值本来就在 0.5 时不会触发 onValueChanged）。
    /// </summary>
    public void ResumeSetting()
    {
        SetSliderValue(musicVolume, AudioManager.DEFAULT_VOLUME);
        SetSliderValue(soundVolume, AudioManager.DEFAULT_VOLUME);

        ApplyMusicVolume(AudioManager.DEFAULT_VOLUME);
        ApplySoundVolume(AudioManager.DEFAULT_VOLUME);

        SaveVolumeSettings();
    }

    #endregion

    #region Slider 回调

    private void OnMusicVolumeChanged(float value)
    {
        ApplyMusicVolume(ToNormalized(musicVolume, value));
        TrySaveVolumeSettings();
    }

    private void OnSoundVolumeChanged(float value)
    {
        ApplySoundVolume(ToNormalized(soundVolume, value));
        TrySaveVolumeSettings();
    }

    #endregion

    #region 音量的应用与持久化

    /// <summary>把运行时音量同步到两个滑块（不触发 onValueChanged）</summary>
    private void Refresh()
    {
        AudioManager audio = GetAudioManager();

        float bgm = audio != null
            ? audio.BGMVolume
            : PlayerPrefsManager.GetFloat(AudioManager.KEY_BGM_VOLUME, AudioManager.DEFAULT_VOLUME);

        float sfx = audio != null
            ? audio.SFXVolume
            : PlayerPrefsManager.GetFloat(AudioManager.KEY_SFX_VOLUME, AudioManager.DEFAULT_VOLUME);

        SetSliderValue(musicVolume, bgm);
        SetSliderValue(soundVolume, sfx);
    }

    /// <summary>
    /// 取一个「已存在」的 AudioManager：不存在时不触发惰性创建，
    /// 应用退出过程中（单例已标记退出、Instance 返回 null）也不会抛 NRE。
    /// </summary>
    private static AudioManager GetAudioManager()
    {
        if (!AudioManager.HasInstance)
            return null;

        // 注意：这里必须用 Unity 重载的 != null 判断，不能用 ?. / ??
        AudioManager audio = AudioManager.Instance;
        return audio != null ? audio : null;
    }

    /// <summary>应用 BGM 音量并写入内存（不落盘）</summary>
    private static void ApplyMusicVolume(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        AudioManager audio = GetAudioManager();
        if (audio != null)
            audio.SetBGMVolume(normalized);

        PlayerPrefsManager.SetFloat(AudioManager.KEY_BGM_VOLUME, normalized);
    }

    /// <summary>
    /// 应用 SFX 音量并写入内存（不落盘）。
    /// 注意：<see cref="AudioManager.SetSFXDefaultVolume"/> 只影响<b>后续</b>播放的音效，
    /// 已在播放中的 PlayOneShot 实例无法回溯调整（引擎限制）。
    /// </summary>
    private static void ApplySoundVolume(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        AudioManager audio = GetAudioManager();
        if (audio != null)
            audio.SetSFXDefaultVolume(normalized);

        PlayerPrefsManager.SetFloat(AudioManager.KEY_SFX_VOLUME, normalized);
    }

    /// <summary>节流落盘：拖动过程中最多每 <see cref="SAVE_INTERVAL"/> 秒写一次磁盘</summary>
    private void TrySaveVolumeSettings()
    {
        if (Time.unscaledTime - lastSaveTime < SAVE_INTERVAL)
            return;

        SaveVolumeSettings();
    }

    /// <summary>落盘：优先由 AudioManager 统一保存运行时音量，不存在时才用滑块值兜底</summary>
    private void SaveVolumeSettings()
    {
        AudioManager audio = GetAudioManager();
        if (audio != null)
        {
            audio.SaveVolumeSettings();
        }
        else
        {
            // AudioManager 尚未创建（或应用退出中）：直接以滑块值为准写存档
            if (musicVolume != null)
                PlayerPrefsManager.SetFloat(AudioManager.KEY_BGM_VOLUME, ToNormalized(musicVolume, musicVolume.value));

            if (soundVolume != null)
                PlayerPrefsManager.SetFloat(AudioManager.KEY_SFX_VOLUME, ToNormalized(soundVolume, soundVolume.value));

            PlayerPrefsManager.Save();
        }

        lastSaveTime = Time.unscaledTime;
    }

    #endregion

    #region Slider 值换算

    /// <summary>把 0-1 归一化音量写入 Slider（不触发回调）</summary>
    private static void SetSliderValue(Slider slider, float normalized)
    {
        if (slider == null)
            return;

        slider.SetValueWithoutNotify(
            Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(normalized)));
    }

    /// <summary>把 Slider 的原始值换算为 0-1 归一化音量</summary>
    private static float ToNormalized(Slider slider, float value)
    {
        if (slider == null)
            return Mathf.Clamp01(value);

        float range = slider.maxValue - slider.minValue;
        if (Mathf.Approximately(range, 0f))
            return Mathf.Clamp01(value);

        return Mathf.Clamp01((value - slider.minValue) / range);
    }

    #endregion
}
