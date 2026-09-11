using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace LiteGameFramework
{
    public class UIConfig
    {
        public EUIType uiType;
        public string packageName;
        public string panelName;
        public EUILayer uiLayer;
        public bool isWindow;
        public bool isPersistent;

        public static List<UIConfig> GetAllConfigs(string _jsonString)
        {
            var uiConfigs = JsonConvert.DeserializeObject<List<UIConfigJson>>(_jsonString);
            var list = new List<UIConfig>();
            foreach (var cfg in uiConfigs)
            {
                if (!Enum.TryParse<EUIType>(cfg.type, out EUIType uiType))
                {
                    Debug.LogError($"UIConfig.json 中的 {cfg.type} UIType解析异常，已跳过该配置");
                    continue;
                }

                if (!Enum.TryParse<EUILayer>(cfg.layer, out EUILayer layer))
                {
                    Debug.LogError($"UIConfig.json 中的 {cfg.layer} UILayer解析异常，已跳过该配置");
                    continue;
                }

                list.Add(new UIConfig
                {
                    uiType = uiType,
                    packageName = cfg.packageName,
                    panelName = cfg.panelName,
                    uiLayer = layer,
                    isWindow = cfg.isWindow,
                    isPersistent = cfg.isPersistent
                });
            }

            return list;
        }
    }
}