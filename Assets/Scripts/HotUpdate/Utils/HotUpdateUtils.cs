using Invariable;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;



namespace HotUpdate
{
    public class HotUpdateUtils
    {
        private static RectTransform[] m_panelParents = null;
        private static HashSet<string> m_loadingPanels = null;



        /// <summary>
        /// 打开 UI Prefab 面板（加载中重复调用直接忽略）
        /// </summary>
        public static void OpenUIPrefabPanel(string prefabPath, int layer, Action<GameObject> callBack = null)
        {
            string prefabName = Path.GetFileName(prefabPath);

            if (prefabName.Contains(".prefab"))
            {
                prefabName = prefabName.Replace(".prefab", "");
            }

            if (UIManager.Instance.AllPanel.TryGetValue(prefabName, out UIPanel existingPanel))
            {
                existingPanel.gameObject.SetActive(true);
                callBack?.Invoke(existingPanel.gameObject);

                return;
            }

            m_loadingPanels ??= new HashSet<string>();

            if (m_loadingPanels.Contains(prefabName))
            {
                return;
            }

            m_loadingPanels.Add(prefabName);

            string key = $"Prefabs_{prefabName}";
            Transform parentTrans = GetPanelParent(layer);

            YooAssetManager.Instance.AsyncLoadAsset<GameObject>(key, (asset) =>
            {
                m_loadingPanels.Remove(prefabName);

                if (asset == null)
                {
                    GameLog.Error($"打开面板失败，资源加载为空：{key}");

                    return;
                }

                if (UIManager.Instance.AllPanel.TryGetValue(prefabName, out UIPanel panel))
                {
                    panel.gameObject.SetActive(true);
                    callBack?.Invoke(panel.gameObject);

                    return;
                }

                if (asset.GetComponent<UIPanel>() == null)
                {
                    GameLog.Error($"打开面板失败，预制体未挂载 UIPanel：{prefabName}");

                    return;
                }

                GameObject gameObject = GameObject.Instantiate(asset, parentTrans);
                gameObject.name = prefabName;
                UIPanel uiPanel = gameObject.GetComponent<UIPanel>();
                UIManager.Instance.AddUIPanel(prefabName, uiPanel);
                callBack?.Invoke(gameObject);
            });
        }

        /// <summary>
        /// 打开提示弹窗
        /// </summary>
        public static void OpenTipsPanel(string content, string btn1, Action callBack1 = null, string btn2 = "", Action callBack2 = null, string title = "")
        {
            OpenUIPrefabPanel("TipsPanel", 2, (obj) =>
            {
                TipsPanel tipsPanel = obj.GetComponent<TipsPanel>();
                tipsPanel.ShowInfo(content, btn1, btn2, callBack1, callBack2, title);
            });
        }

        /// <summary>
        /// 显示浮动提示文本
        /// </summary>
        public static void ShowFloatText(string text)
        {
            OpenUIPrefabPanel("FloatTextPanel", 3, (obj) =>
            {
                obj.GetComponent<FloatTextPanel>().ShowInfo(text);
            });
        }



        /// <summary>
        /// 获取指定 Canvas 层的面板父节点（按层缓存）
        /// </summary>
        private static Transform GetPanelParent(int layer)
        {
            m_panelParents ??= new RectTransform[4];

            if (layer < 0 || layer >= m_panelParents.Length)
            {
                layer = 0;
            }

            if (m_panelParents[layer] == null)
            {
                string path = InvariableConst.UIPanelPath_0;

                if (layer == 1)
                {
                    path = InvariableConst.UIPanelPath_1;
                }
                else if (layer == 2)
                {
                    path = InvariableConst.UIPanelPath_2;
                }
                else if (layer == 3)
                {
                    path = InvariableConst.UIPanelPath_3;
                }

                GameObject parentObject = GameObject.Find(path);
                m_panelParents[layer] = parentObject.GetComponent<RectTransform>();
            }

            return m_panelParents[layer];
        }
    }
}