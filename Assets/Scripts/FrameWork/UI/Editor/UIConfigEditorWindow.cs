using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LiteGameFramework
{
    /// <summary>
    /// UI配置编辑器窗口 —— 用于编辑 UIConfigJson 列表的JSON配置文件
    /// 布局：顶部文件选择框 | 左侧面板列表 | 右侧配置详情
    /// </summary>
    public class UIConfigEditorWindow : EditorWindow
{
    // ========== 文件选择 ==========
    private TextAsset jsonFile;

    // ========== 数据 ==========
    private List<UIConfigJson> configs = new List<UIConfigJson>();
    private int selectedIndex = -1;

    // ========== 滚动位置 ==========
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;

    [MenuItem("Tool/UI配置编辑器")]
    static void CreateWindow()
    {
        var window = GetWindow<UIConfigEditorWindow>();
        window.titleContent = new GUIContent("UI配置编辑器");
        window.minSize = new Vector2(700, 450);
    }

    private void OnEnable()
    {
        // 窗口启动时的初始化，自动查找UIConfig.json
        AutoFindAndLoadConfig();
    }

    /// <summary>
    /// 通过 AssetDatabase 自动搜索并加载 UIConfig.json
    /// </summary>
    void AutoFindAndLoadConfig()
    {
        string[] guids = AssetDatabase.FindAssets("UIConfig t:TextAsset");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) == "UIConfig.json")
            {
                jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (jsonFile != null)
                {
                    LoadConfigs();
                    return;
                }
            }
        }
        Debug.LogWarning("[UIConfigEditor] 未在 Assets 中找到 UIConfig.json，请手动拖入配置文件");
    }

    void OnGUI()
    {
        DrawTopArea();
        EditorGUILayout.Space(4);
        DrawMainArea();
    }

    // ==================== 顶部：文件选择区域 ====================
    void DrawTopArea()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("JSON配置文件:", GUILayout.Width(90));

        var newFile = (TextAsset)EditorGUILayout.ObjectField(jsonFile, typeof(TextAsset), false);
        if (newFile != jsonFile)
        {
            jsonFile = newFile;
            LoadConfigs();
        }

        if (GUILayout.Button("加载", GUILayout.Width(60)))
        {
            LoadConfigs();
        }

        if (jsonFile != null)
        {
            EditorGUILayout.LabelField($"路径: {AssetDatabase.GetAssetPath(jsonFile)}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ==================== 主体区域：左面板列表 + 右配置详情 ====================
    void DrawMainArea()
    {
        EditorGUILayout.BeginHorizontal();

        // 左侧 —— 面板列表
        DrawLeftPanel();

        // 分割线
        EditorGUILayout.Separator();

        // 右侧 —— 配置详情
        DrawRightPanel();

        EditorGUILayout.EndHorizontal();
    }

    // ==================== 左侧：面板列表 ====================
    void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.35f));
        EditorGUILayout.LabelField("面板列表", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos);

        for (int i = 0; i < configs.Count; i++)
        {
            bool isSelected = (i == selectedIndex);
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.3f, 0.7f, 1f, 1f) : originalBg;

            EditorGUILayout.BeginHorizontal("box");

            string displayName = string.IsNullOrEmpty(configs[i].panelName)
                ? $"[{i}] 未命名面板"
                : $"[{i}] {configs[i].type} - {configs[i].panelName}";

            if (GUILayout.Button(displayName, EditorStyles.label))
            {
                selectedIndex = i;
            }

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = originalBg;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(4);

        // 添加 / 删除按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加面板", GUILayout.Height(25)))
        {
            configs.Add(new UIConfigJson());
            selectedIndex = configs.Count - 1;
        }
        if (selectedIndex >= 0 && selectedIndex < configs.Count)
        {
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("- 删除面板", GUILayout.Height(25)))
            {
                configs.RemoveAt(selectedIndex);
                if (selectedIndex >= configs.Count)
                    selectedIndex = configs.Count - 1;
                Repaint();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ==================== 右侧：配置详情 ====================
    void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("面板配置", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (selectedIndex >= 0 && selectedIndex < configs.Count)
        {
            rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos);

            var cfg = configs[selectedIndex];

            // Type —— 动态类型下拉（由 PanelName 注册，自动同步到 EUIType）
            var typeNames = GetAvailableTypeNames();
            int typeIndex = Mathf.Max(0, typeNames.IndexOf(cfg.type));
            int newTypeIndex = EditorGUILayout.Popup("Type (EUIType)", typeIndex, typeNames.ToArray());
            if (newTypeIndex >= 0 && newTypeIndex < typeNames.Count)
            {
                cfg.type = typeNames[newTypeIndex];
            }

            EditorGUILayout.Space(4);

            // Package Name
            cfg.packageName = EditorGUILayout.TextField("Package Name", cfg.packageName);

            EditorGUILayout.Space(4);

            // Panel Name
            cfg.panelName = EditorGUILayout.TextField("Panel Name", cfg.panelName);

            EditorGUILayout.Space(4);

            // Layer —— 对应 EUILayer 枚举
            EUILayer uiLayer = EUILayer.Normal;
            System.Enum.TryParse(cfg.layer, out uiLayer);
            uiLayer = (EUILayer)EditorGUILayout.EnumPopup("Layer (EUILayer)", uiLayer);
            cfg.layer = uiLayer.ToString();

            EditorGUILayout.Space(4);

            // Is Window
            cfg.isWindow = EditorGUILayout.Toggle("Is Window", cfg.isWindow);

            EditorGUILayout.Space(8);

            // 当前配置的 JSON 预览
            EditorGUILayout.LabelField("JSON预览:", EditorStyles.miniBoldLabel);
            try
            {
                string previewJson = JsonConvert.SerializeObject(cfg, Formatting.Indented);
                EditorGUILayout.TextArea(previewJson, GUILayout.Height(100));
            }
            catch { }

            configs[selectedIndex] = cfg;
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("请从左侧列表选择一个面板进行配置，或点击“+添加面板”创建新的UI配置", MessageType.Info);
        }

        EditorGUILayout.Space(8);

        // 底部操作按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("保存配置到文件", GUILayout.Width(130), GUILayout.Height(30)))
        {
            SaveConfigs();
        }

        if (GUILayout.Button("重新加载", GUILayout.Width(80), GUILayout.Height(30)))
        {
            LoadConfigs();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ==================== JSON 加载 ====================
    void LoadConfigs()
    {
        if (jsonFile == null)
        {
            Debug.LogWarning("请先选择一个JSON配置文件");
            return;
        }

        try
        {
            string json = jsonFile.text;
            configs = JsonConvert.DeserializeObject<List<UIConfigJson>>(json) ?? new List<UIConfigJson>();
            selectedIndex = configs.Count > 0 ? 0 : -1;
            Debug.Log($"<color=green>[UIConfigEditor] 加载成功，共 {configs.Count} 个UI配置</color>");
            Repaint();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[UIConfigEditor] JSON加载失败: {e.Message}</color>");
        }
    }

    // ==================== JSON 保存 ====================
    void SaveConfigs()
    {
        if (jsonFile == null)
        {
            Debug.LogError("请先选择一个JSON配置文件");
            return;
        }

        try
        {
            string json = JsonConvert.SerializeObject(configs, Formatting.Indented);
            string path = AssetDatabase.GetAssetPath(jsonFile);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[UIConfigEditor] 保存成功 → {path}</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[UIConfigEditor] 保存失败: {e.Message}</color>");
        }

        // 保存后自动将 type 注册到 EUIType 枚举文件
        RegenerateEUITypeFile();
    }

    // ==================== 动态类型列表 ====================
    /// <summary>
    /// 获取所有可用的 Type 名称：
    /// 当前 EUIType 枚举值（排除 Max） + 配置中独有的 panelName
    /// 保证 Max 不会出现在下拉选项中
    /// </summary>
    List<string> GetAvailableTypeNames()
    {
        var names = new List<string>();

        // 1. 从现有 EUIType 枚举中获取（排除 Max）
        foreach (var enumName in System.Enum.GetNames(typeof(EUIType)))
        {
            if (enumName != "Max")
                names.Add(enumName);
        }

        // 2. 从配置的 panelName 中添加尚未在枚举中的名称（由 PanelName 注册）
        foreach (var cfg in configs)
        {
            if (!string.IsNullOrEmpty(cfg.panelName) && !names.Contains(cfg.panelName))
            {
                names.Add(cfg.panelName);
            }
        }

        return names;
    }

    // ==================== EUIType.cs 文件自动生成 ====================
    /// <summary>
    /// 根据当前配置中所有 type 值重新生成 EUIType.cs 文件，
    /// 保证 Max 枚举始终在最下方。
    /// </summary>
    void RegenerateEUITypeFile()
    {
        // 收集所有唯一的 type 值（排除空字符串和 "Max"）
        var typeNames = configs
            .Select(c => c.type)
            .Where(t => !string.IsNullOrEmpty(t) && t != "Max")
            .Distinct()
            .ToList();

        string filePath = Path.Combine(Application.dataPath, "Scripts/UI/EUIType.cs");

        var sb = new StringBuilder();
        sb.AppendLine("public enum EUIType");
        sb.AppendLine("{");

        for (int i = 0; i < typeNames.Count; i++)
        {
            sb.AppendLine($"    {typeNames[i]},");
        }

        sb.AppendLine("    Max");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[UIConfigEditor] EUIType.cs 已更新，包含 {typeNames.Count} 个类型</color>");
    }
}
}