using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace LiteGameFramework
{
    /// <summary>
    /// Unity简单音频控制器（管理BGM和SFX）
    /// </summary>
    public class AudioManager : SingletonMono<AudioManager>
    {
        // 背景音乐音频源（唯一）
        private AudioSource _bgmSource;
        // 单个SFX音频源（使用 PlayOneShot 播放，可靠且无需额外GameObject）
        private AudioSource _sfxSource;

        // 角色配音（Voice）管理
        // 单个Voice音源用于播放角色配音，可选择打断或入队播放
        private AudioSource _voiceSource;
        private Queue<VoiceItem> _voiceQueue = new Queue<VoiceItem>();
        private Coroutine _voiceCoroutine;
        [Range(0f, 1f)] public float defaultVoiceVolume = 1f;

        // 音量配置（默认最大值）
        [Range(0f, 1f)] public float defaultBgmVolume = 1f;
        [Range(0f, 1f)] public float defaultSfxVolume = 1f;

        // 是否正在退出（用于防止在退出时重新创建单例导致场景残留）
        private static bool applicationIsQuitting = false;

        protected override void Awake()
        {
            base.Awake();

            // 初始化音源（若尚未初始化）
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.volume = defaultBgmVolume;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.volume = defaultSfxVolume;
            }

            if (_voiceSource == null)
            {
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.loop = false;
                _voiceSource.playOnAwake = false;
                _voiceSource.volume = defaultVoiceVolume;
            }
        }

        #region 背景音乐（BGM）控制
        /// <summary>
        /// 播放背景音乐
        /// </summary>
        /// <param name="bgmClip">背景音乐音频片段</param>
        /// <param name="isLoop">是否循环（默认true）</param>
        public void PlayBGM(AudioClip bgmClip, bool isLoop = true)
        {
            // 空检查
            if (bgmClip == null)
            {
                Debug.LogError("BGM音频片段不能为空！");
                return;
            }

            // 如果当前BGM和要播放的一致，且正在播放，直接返回
            if (_bgmSource.clip == bgmClip && _bgmSource.isPlaying)
            {
                return;
            }

            // 设置并播放BGM
            _bgmSource.clip = bgmClip;
            _bgmSource.loop = isLoop;
            _bgmSource.Play();
            Debug.Log($"开始播放BGM：{bgmClip.name}");
        }

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public void PauseBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
                Debug.Log("暂停BGM");
            }
        }

        /// <summary>
        /// 恢复播放背景音乐
        /// </summary>
        public void ResumeBGM()
        {
            if (_bgmSource != null && !_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                _bgmSource.Play();
                Debug.Log("恢复播放BGM");
            }
        }

        /// <summary>
        /// 停止播放背景音乐
        /// </summary>
        public void StopBGM()
        {
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
                _bgmSource.clip = null; // 清空音频片段
            }

            Debug.Log("停止BGM");
        }

        /// <summary>
        /// 设置背景音乐音量
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetBGMVolume(float volume)
        {
            // 限制音量范围0-1
            volume = Mathf.Clamp01(volume);
            if (_bgmSource != null)
            {
                _bgmSource.volume = volume;
                Debug.Log($"设置BGM音量：{volume}");
            }
            else
            {
                Debug.LogWarning("SetBGMVolume: _bgmSource is null");
            }
        }

        /// <summary>
        /// 检查BGM是否正在播放
        /// </summary>
        /// <returns>是否播放中</returns>
        public bool IsBGMPlaying()
        {
            return _bgmSource != null && _bgmSource.isPlaying;
        }
        #endregion

        #region 音效（SFX）控制
        /// <summary>
        /// 播放音效
        /// </summary>
        /// <param name="sfxClip">音效音频片段</param>
        /// <param name="volume">音量（0-1，默认使用默认值）</param>
        public void PlaySFX(AudioClip sfxClip, float volume = -1f)
        {
            // 空检查
            if (sfxClip == null)
            {
                Debug.LogError("SFX音频片段不能为空！");
                return;
            }

            // 确定音量
            float vol = volume < 0 ? defaultSfxVolume : Mathf.Clamp01(volume);

            // 使用单个SFX源的 PlayOneShot 播放，支持快速连续播放和叠加
            if (_sfxSource == null)
            {
                Debug.LogWarning("PlaySFX: _sfxSource 为 null，创建临时音源");
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.volume = defaultSfxVolume;
            }

            _sfxSource.PlayOneShot(sfxClip, vol);
            Debug.Log($"播放音效：{sfxClip.name}，音量：{vol}");
        }

        /// <summary>
        /// 设置所有音效的默认音量（后续播放的SFX生效）
        /// </summary>
        /// <param name="volume">音量（0-1）</param>
        public void SetSFXDefaultVolume(float volume)
        {
            defaultSfxVolume = Mathf.Clamp01(volume);
            Debug.Log($"设置SFX默认音量：{defaultSfxVolume}");
        }

        /// <summary>
        /// 停止所有正在播放的音效
        /// </summary>
        public void StopAllSFX()
        {
            if (_sfxSource != null && _sfxSource.isPlaying)
            {
                _sfxSource.Stop();
            }
            Debug.Log("停止所有音效");
        }

        /// <summary>
        /// 播放角色配音（可选择打断当前配音或加入队列）
        /// </summary>
        public void PlayVoice(AudioClip clip, float volume = -1f, bool interrupt = true)
        {
            if (clip == null)
            {
                Debug.LogError("Voice 音频片段不能为空！");
                return;
            }

            float vol = volume < 0 ? defaultVoiceVolume : Mathf.Clamp01(volume);

            if (_voiceSource == null)
            {
                Debug.LogWarning("PlayVoice: _voiceSource 为 null，创建临时音源");
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.playOnAwake = false;
                _voiceSource.loop = false;
                _voiceSource.volume = defaultVoiceVolume;
            }

            if (interrupt)
            {
                // 立即打断并播放
                if (_voiceCoroutine != null)
                {
                    StopCoroutine(_voiceCoroutine);
                    _voiceCoroutine = null;
                    _voiceQueue.Clear();
                }
                _voiceSource.clip = clip;
                _voiceSource.volume = vol;
                _voiceSource.Play();
                Debug.Log($"播放配音（打断）：{clip.name}，音量：{vol}");
            }
            else
            {
                // 入队播放
                _voiceQueue.Enqueue(new VoiceItem { clip = clip, volume = vol });
                if (_voiceCoroutine == null)
                {
                    _voiceCoroutine = StartCoroutine(ProcessVoiceQueue());
                }
                Debug.Log($"加入配音队列：{clip.name}，音量：{vol}");
            }
        }

        private IEnumerator ProcessVoiceQueue()
        {
            while (_voiceQueue.Count > 0)
            {
                var item = _voiceQueue.Dequeue();
                if (_voiceSource == null)
                {
                    _voiceSource = gameObject.AddComponent<AudioSource>();
                    _voiceSource.playOnAwake = false;
                    _voiceSource.loop = false;
                    _voiceSource.volume = defaultVoiceVolume;
                }
                _voiceSource.clip = item.clip;
                _voiceSource.volume = item.volume;
                _voiceSource.Play();
                yield return new WaitForSeconds(item.clip.length + 0.05f);
            }
            _voiceCoroutine = null;
        }

        /// <summary>
        /// 停止当前配音并清空队列（可选）
        /// </summary>
        public void StopVoice(bool clearQueue = true)
        {
            if (_voiceSource != null && _voiceSource.isPlaying)
                _voiceSource.Stop();
            if (clearQueue) _voiceQueue.Clear();
            if (_voiceCoroutine != null)
            {
                StopCoroutine(_voiceCoroutine);
                _voiceCoroutine = null;
            }
            Debug.Log("停止配音");
        }

        /// <summary>
        /// 检查配音是否正在播放
        /// </summary>
        public bool IsVoicePlaying()
        {
            return _voiceSource != null && _voiceSource.isPlaying;
        }

        /// <summary>
        /// 设置配音默认音量
        /// </summary>
        public void SetVoiceDefaultVolume(float volume)
        {
            defaultVoiceVolume = Mathf.Clamp01(volume);
            if (_voiceSource != null) _voiceSource.volume = defaultVoiceVolume;
            Debug.Log($"设置配音默认音量：{defaultVoiceVolume}");
        }

        // 小的辅助类型
        private class VoiceItem { public AudioClip clip; public float volume; }
        #endregion

        #region 内部辅助方法
        // （已使用 PlayOneShot，无需额外协程管理临时音源）
        #endregion

        #region 生命周期清理
        protected override void OnDestroy()
        {
            base.OnDestroy();

            // 仅在本实例是单例时清理并置空，避免销毁重复实例时错误清理
            if (Instance == this)
            {
                StopBGM();
                StopAllSFX();
                StopVoice();
            }
        }

        protected override void OnApplicationQuit()
        {
            // 标记正在退出，防止其他对象的 OnDestroy/OnDisable 在退出时触发 Instance 创建
            applicationIsQuitting = true;
        }
        #endregion
    }
}