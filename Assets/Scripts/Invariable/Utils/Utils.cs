using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;



namespace Invariable
{
    public class Utils
    {
        private static Camera[] m_uiCamera = null;
        private static RectTransform m_uiRoot = null;
        private static Dictionary<string, Sprite> m_remoteSpriteCache = null;

        public static Camera[] UICamera
        {
            get
            {
                m_uiCamera ??= new Camera[]
                {
                    GameObject.Find(InvariableConst.UICameraPath_0).GetComponent<Camera>(),
                    GameObject.Find(InvariableConst.UICameraPath_1).GetComponent<Camera>(),
                    GameObject.Find(InvariableConst.UICameraPath_2).GetComponent<Camera>(),
                    GameObject.Find(InvariableConst.UICameraPath_3).GetComponent<Camera>(),
                };

                return m_uiCamera;
            }
        }

        public static RectTransform UIRoot
        {
            get
            {
                m_uiRoot ??= GameObject.Find(InvariableConst.UIRootPath).GetComponent<RectTransform>();

                return m_uiRoot;
            }
        }



        /// <summary>
        /// 从对象或子路径获取 GameObject
        /// </summary>
        public static GameObject GetGameObject(UnityEngine.Object obj, string childPath = "")
        {
            GameObject gameObject = null;

            if (obj is GameObject)
            {
                gameObject = obj as GameObject;
            }
            else if (obj is Component)
            {
                Component component = obj as Component;
                gameObject = component.gameObject;
            }

            if (!string.IsNullOrEmpty(childPath))
            {
                Transform trans = gameObject.transform.Find(childPath);

                if (trans != null)
                {
                    return trans.gameObject;
                }

                return null;
            }

            return gameObject;
        }

        /// <summary>
        /// 从对象或子路径获取 Transform
        /// </summary>
        public static Transform GetTransform(UnityEngine.Object obj, string childPath = "")
        {
            GameObject gameObject = GetGameObject(obj, childPath);

            if (gameObject != null)
            {
                return gameObject.transform;
            }

            return null;
        }

        /// <summary>
        /// 克隆 GameObject 并设置名称与父节点
        /// </summary>
        public static GameObject Clone(UnityEngine.Object obj, string name = "cloneName", UnityEngine.Object parent = null)
        {
            GameObject item = GetGameObject(obj);
            GameObject clone;

            if (parent != null)
            {
                Transform parentTrans = GetTransform(parent);
                clone = GameObject.Instantiate<GameObject>(item, Vector3.zero, Quaternion.identity, parentTrans);
            }
            else
            {
                clone = GameObject.Instantiate<GameObject>(item, Vector3.zero, Quaternion.identity);
            }

            clone.name = name;

            return clone;
        }

        /// <summary>
        /// 隐藏全部子节点
        /// </summary>
        public static void HideAllChildren(Transform transform)
        {
            if (transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform item = transform.GetChild(i);
                    item.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 将字节大小格式化为可读字符串
        /// </summary>
        public static string FormatFileByteSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "G", "T" };
            int unitIndex = 0;
            double size = bytes;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        /// <summary>
        /// 关闭指定名称的 UI 面板
        /// </summary>
        public static void CloseUIPrefabPanel(string prefabName)
        {
            UIManager.Instance.CloseUIPanel(prefabName);
        }

        /// <summary>
        /// 设置图片灰态材质
        /// </summary>
        public static void SetGray(UnityEngine.Object obj, string childPath = "", bool isGray = true, bool isMask = false)
        {
            Transform trans = GetTransform(obj);

            if (!string.IsNullOrEmpty(childPath))
            {
                trans = trans.Find(childPath);
            }

            if (trans == null)
            {
                return;
            }

            Image image = trans.GetComponent<Image>();
            RawImage rawImage = trans.GetComponent<RawImage>();

            if (image == null && rawImage == null)
            {
                return;
            }

            if (isGray)
            {
                string key;

                if (isMask)
                {
                    key = "Materials_UIMaskGrayscaleMaterial";
                }
                else
                {
                    key = "Materials_GrayscaleMaterial";
                }

                YooAssetManager.Instance.AsyncLoadAsset<Material>(key, (material) =>
                {
                    if (image != null)
                    {
                        image.material = material;
                    }
                    else if (rawImage != null)
                    {
                        rawImage.material = material;
                    }
                });
            }
            else if (image != null)
            {
                image.material = null;
            }
            else if (rawImage != null)
            {
                rawImage.material = null;
            }
        }

        /// <summary>
        /// 按远程 URL 设置 Image/RawImage，URL 为空/非法或下载失败时赋值兜底图，兜底图为空则保留旧图
        /// </summary>
        public static void SetRemoteImage(UnityEngine.Object obj, string childPath = "", string url = "", bool isSetNativeSize = false, Sprite fallBackSprite = null)
        {
            Transform trans = GetTransform(obj, childPath);

            if (trans == null)
            {
                return;
            }

            Image image = trans.GetComponent<Image>();
            RawImage rawImage = trans.GetComponent<RawImage>();

            if (image == null && rawImage == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                if (fallBackSprite != null)
                {
                    ApplyRemoteSprite(image, rawImage, fallBackSprite, isSetNativeSize);
                }

                return;
            }

            if (m_remoteSpriteCache != null && m_remoteSpriteCache.TryGetValue(url, out Sprite cachedSprite) && cachedSprite != null)
            {
                ApplyRemoteSprite(image, rawImage, cachedSprite, isSetNativeSize);

                return;
            }

            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (obj == null || (image == null && rawImage == null))
                    {
                        return;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        GameLog.Info($"远程图片下载失败: {request.error}");

                        if (fallBackSprite != null)
                        {
                            ApplyRemoteSprite(image, rawImage, fallBackSprite, isSetNativeSize);
                        }

                        return;
                    }

                    Texture2D texture = DownloadHandlerTexture.GetContent(request);

                    if (texture == null)
                    {
                        GameLog.Info("远程图片下载结果为空");

                        if (fallBackSprite != null)
                        {
                            ApplyRemoteSprite(image, rawImage, fallBackSprite, isSetNativeSize);
                        }

                        return;
                    }

                    Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                    m_remoteSpriteCache ??= new Dictionary<string, Sprite>();
                    m_remoteSpriteCache[url] = sprite;
                    ApplyRemoteSprite(image, rawImage, sprite, isSetNativeSize);
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        /// <summary>
        /// 将远程 Sprite 赋给 Image 或 RawImage
        /// </summary>
        private static void ApplyRemoteSprite(Image image, RawImage rawImage, Sprite sprite, bool isSetNativeSize)
        {
            if (sprite == null)
            {
                return;
            }

            if (image != null)
            {
                image.sprite = sprite;

                if (isSetNativeSize)
                {
                    image.SetNativeSize();
                }
            }
            else if (rawImage != null)
            {
                rawImage.texture = sprite.texture;

                if (isSetNativeSize)
                {
                    rawImage.SetNativeSize();
                }
            }
        }

        /// <summary>
        /// 播放 Animation 动画
        /// </summary>
        public static void PlayAnimation(UnityEngine.Object obj, string childPath = "", string animName = "", WrapMode wrapMode = WrapMode.Once, Action callBack = null)
        {
            GameManager.Instance.StartCoroutine(PlayAnimation2(obj, childPath, animName, wrapMode, callBack));
        }

        /// <summary>
        /// 创建并常驻 Manager 实例
        /// </summary>
        public static void CreateManagerInstance<T>(params Type[] extraComponents) where T : MonoBehaviour
        {
            string managerName = typeof(T).Name;
            GameObject obj = GameObject.Find(managerName);

            if (obj != null)
            {
                return;
            }

            obj = new GameObject(managerName);

            if (extraComponents != null && extraComponents.Length > 0)
            {
                for (int i = 0; i < extraComponents.Length; i++)
                {
                    Type extraType = extraComponents[i];

                    if (extraType != null)
                    {
                        obj.AddComponent(extraType);
                    }
                }
            }

            obj.AddComponent<T>();

            UnityEngine.Object.DontDestroyOnLoad(obj);
        }

        /// <summary>
        /// 将颜色字符串解析为 Color
        /// </summary>
        public static Color GetColorByString(string colorStr)
        {
            if (ColorUtility.TryParseHtmlString(colorStr, out Color color))
            {
                return color;
            }

            return Color.white;
        }



        /// <summary>
        /// 播放动画并在完成后回调
        /// </summary>
        private static IEnumerator PlayAnimation2(UnityEngine.Object obj, string childPath = "", string animName = "", WrapMode wrapMode = WrapMode.Once, Action callBack = null)
        {
            if (string.IsNullOrEmpty(animName))
            {
                yield break;
            }

            Transform trans = GetTransform(obj);

            if (trans == null)
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(childPath))
            {
                trans = trans.Find(childPath);
            }

            if (trans == null)
            {
                yield break;
            }

            Animation animation = trans.GetComponent<Animation>();

            if (animation == null)
            {
                yield break;
            }

            animation.wrapMode = wrapMode;

            animation.Play(animName);

            if (wrapMode == WrapMode.Once)
            {
                yield return new WaitWhile(() =>
                {
                    return animation.isPlaying;
                });
                callBack?.Invoke();
            }
        }
    }
}