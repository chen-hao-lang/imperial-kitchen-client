using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// PlayerPrefs 数据存储管理类
    /// 封装常用的数据存取操作，提供异常处理和默认值支持
    /// 注意：所有方法需在主线程调用
    /// </summary>
    public static class PlayerPrefsManager
{
    #region 基础类型存储 (int/float/string)
    /// <summary>
    /// 存储 int 类型数据
    /// </summary>
    /// <param name="key">存储键名（建议统一命名规范，如 "Player_Gold"）</param>
    /// <param name="value">要存储的值</param>
    public static void SetInt(string key, int value)
    {
        try
        {
            PlayerPrefs.SetInt(key, value);
            // 可选：立即保存（也可在游戏关键节点统一调用 Save()）
            // PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存储int数据失败，Key: {key}，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 读取 int 类型数据
    /// </summary>
    /// <param name="key">存储键名</param>
    /// <param name="defaultValue">键不存在时返回的默认值</param>
    /// <returns>读取到的值或默认值</returns>
    public static int GetInt(string key, int defaultValue = 0)
    {
        try
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : defaultValue;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取int数据失败，Key: {key}，错误信息：{e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 存储 float 类型数据
    /// </summary>
    public static void SetFloat(string key, float value)
    {
        try
        {
            PlayerPrefs.SetFloat(key, value);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存储float数据失败，Key: {key}，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 读取 float 类型数据
    /// </summary>
    public static float GetFloat(string key, float defaultValue = 0f)
    {
        try
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : defaultValue;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取float数据失败，Key: {key}，错误信息：{e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 存储 string 类型数据
    /// </summary>
    public static void SetString(string key, string value)
    {
        try
        {
            // 空字符串防护：避免存储 null 导致异常
            var safeValue = value ?? string.Empty;
            PlayerPrefs.SetString(key, safeValue);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"存储string数据失败，Key: {key}，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 读取 string 类型数据
    /// </summary>
    public static string GetString(string key, string defaultValue = "")
    {
        try
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : defaultValue;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取string数据失败，Key: {key}，错误信息：{e.Message}");
            return defaultValue;
        }
    }
    #endregion

    #region 扩展类型存储 (bool)
    /// <summary>
    /// 存储 bool 类型数据（转换为 int 存储：true=1，false=0）
    /// </summary>
    public static void SetBool(string key, bool value)
    {
        // 将 bool 转为 int 存储
        SetInt(key, value ? 1 : 0);
    }

    /// <summary>
    /// 读取 bool 类型数据（从 int 转换：1=true，0=false）
    /// </summary>
    public static bool GetBool(string key, bool defaultValue = false)
    {
        var intValue = GetInt(key, defaultValue ? 1 : 0);
        return intValue == 1;
    }
    #endregion

    #region 数据管理操作
    /// <summary>
    /// 删除指定键的存储数据
    /// </summary>
    public static void DeleteKey(string key)
    {
        try
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除数据失败，Key: {key}，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 清空所有 PlayerPrefs 存储数据（谨慎使用！）
    /// </summary>
    public static void ClearAll()
    {
        try
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("已清空所有 PlayerPrefs 存储数据");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"清空所有数据失败，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 手动保存所有未持久化的修改（建议在游戏存档、退出时调用）
    /// PlayerPrefs 会自动保存，但手动调用更安全
    /// </summary>
    public static void Save()
    {
        try
        {
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存 PlayerPrefs 数据失败，错误信息：{e.Message}");
        }
    }

    /// <summary>
    /// 检查指定键是否存在
    /// </summary>
    public static bool HasKey(string key)
    {
        try
        {
            return PlayerPrefs.HasKey(key);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"检查键是否存在失败，Key: {key}，错误信息：{e.Message}");
            return false;
        }
    }
    #endregion
}
}