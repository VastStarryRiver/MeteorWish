using CloudService;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using YooAsset;

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
using WeChatWASM;

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
using TTSDK;
using static TTSDK.TTKeyboard;
using TTSDK.UNBridgeLib.LitJson;
#endif



namespace Invariable
{
    public enum PlatformAuthStates
    {
        Unsupported, // 编辑器等非小游戏运行时
        NotAuthorized, // 平台侧未授权
        Denied, // 本次玩家点了拒绝
        Authorized, // 平台侧已授权或本次同意
    }

    public class SdkManager : Singleton<SdkManager>
    {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
        private WXRewardedVideoAd m_rewardedVideoAd = null;
        private WXGameClubButton m_wxGameClubButton = null;
        private WXUserInfoButton m_wxUserInfoButton = null;
        private int m_wxUserInfoRequestId = 0;

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
        private TTRewardedVideoAd m_rewardedVideoAd = null;
        private int m_douYinUserInfoRequestId = 0;
#endif

        private TMP_InputField m_inputField = null;
        private bool m_isKeyboardShowing = false;
        private List<ScreenAdapter> m_screenAdapters = null;
        private bool m_isShowRewardedVideoAd = false;
        private Action<bool> m_rewardedVideoAdCallBack = null;
        private string m_platformNickName = null;
        private string m_platformAvatarUrl = null;
        private bool m_platformUserInfoLoading = false;



        #region 初始化
        /// <summary>
        /// 初始化平台 SDK
        /// </summary>
        public void InitSDK(Action callBack = null)
        {
#if UNITY_EDITOR
            callBack?.Invoke();

#elif MINIGAME_SUBPLATFORM_WEIXIN
            WX.InitSDK((code) =>
            {
                AddOnShowListener();
                AddGameUpdateListener();
                callBack?.Invoke(); // 回调即成功
            });

#elif MINIGAME_SUBPLATFORM_DOUYIN
            TT.InitSDK((code, env) =>
            {
                if (code == 0) // 无错误码即成功
                {
                    AddOnShowListener();
                    AddGameUpdateListener();
                    callBack?.Invoke();
                }
                else
                {
                    GameLog.Error($"抖音 SDK 初始化失败 错误码: {code}");
                }
            });
#endif
        }
        #endregion

        #region 登录
        /// <summary>
        /// 平台登录并返回授权 code
        /// </summary>
        public Task<string> PlatformLogin()
        {
#if UNITY_EDITOR
            return Task.FromResult<string>(null);

#elif MINIGAME_SUBPLATFORM_WEIXIN
            var tcs = new TaskCompletionSource<string>();

            WX.Login(new LoginOption
            {
                success = res => tcs.TrySetResult(res.code),
                fail = err =>
                {
                    GameLog.Error($"微信登录失败: {err.errMsg}");
                    tcs.TrySetResult(null);
                }
            });

            return tcs.Task;

#elif MINIGAME_SUBPLATFORM_DOUYIN
            var tcs = new TaskCompletionSource<string>();

            TT.Login((code, anonymousCode, isLogin) =>
            {
                tcs.TrySetResult(code);
            }, err =>
            {
                GameLog.Error($"抖音登录失败: {err}");
                tcs.TrySetResult(null);
            }, true);

            return tcs.Task;
#endif
        }
        #endregion

        #region 游戏生命周期事件监听
        /// <summary>
        /// 注册应用回到前台监听
        /// </summary>
        private void AddOnShowListener()
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN


#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            TT.GetAppLifeCycle().OnShow += (data) =>
            {
                if (!data.ContainsKey("launchFrom") || !data.ContainsKey("location"))
                {
                    return;
                }

                string launchFrom = data["launchFrom"].ToString();
                string location = data["location"].ToString();

                if (launchFrom == "homepage" && location == "sidebar_card")
                {
                    GameLog.Info("侧边栏复访");
                }
            };
#endif
        }

        /// <summary>
        /// 注册小游戏热更新检查监听
        /// </summary>
        private void AddGameUpdateListener()
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            WXUpdateManager wXUpdateManager = WX.GetUpdateManager();

            wXUpdateManager.OnCheckForUpdate((result) =>
            {
                if (result.hasUpdate)
                {
                    GameLog.Info("有新版本发布了！");
                }
            });

            wXUpdateManager.OnUpdateReady((result) =>
            {
                GameLog.Info("重启游戏应用新版本！");
                wXUpdateManager.ApplyUpdate();
            });

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            TTUpdateManager tTUpdateManager = TT.GetUpdateManager();

            tTUpdateManager.OnCheckForUpdate((result) =>
            {
                if (result.HasUpdate)
                {
                    GameLog.Info("有新版本发布了！");
                }
            });

            tTUpdateManager.OnUpdateReady(() =>
            {
                GameLog.Info("重启游戏应用新版本！");
                tTUpdateManager.ApplyUpdate(new ApplyUpdateParams());
            });
#endif
        }
        #endregion

        #region 数据存储
        /// <summary>
        /// 写入本地存储
        /// </summary>
        public void SetLocalData(string key, string data)
        {
#if UNITY_EDITOR
            PlayerPrefs.SetString(key, data);

#elif MINIGAME_SUBPLATFORM_WEIXIN
            WX.StorageSetStringSync(key, data);

#elif MINIGAME_SUBPLATFORM_DOUYIN
            TT.Save<string>(data, key);
#endif
        }

        /// <summary>
        /// 读取本地存储
        /// </summary>
        public string GetLocalData(string key, string defaultValue = "")
        {
            string data = "";

#if UNITY_EDITOR
            data = PlayerPrefs.GetString(key, defaultValue);

#elif MINIGAME_SUBPLATFORM_WEIXIN
            data = WX.StorageGetStringSync(key, defaultValue);

#elif MINIGAME_SUBPLATFORM_DOUYIN
            data = TT.LoadSaving<string>(key);
#endif

            if (string.IsNullOrEmpty(data))
            {
                data = defaultValue;
            }

            return data;
        }

        /// <summary>
        /// 写入云端缓存
        /// </summary>
        public void SetCloudData(string key, string data)
        {
#if UNITY_EDITOR
            SetLocalData(key, data);
#else
            CloudManager.Instance.SetCloudCache(key, data);
#endif
        }

        /// <summary>
        /// 读取云端缓存
        /// </summary>
        public string GetCloudData(string key, string defaultValue = "")
        {
#if UNITY_EDITOR
            return GetLocalData(key, defaultValue);
#else
            return CloudManager.Instance.GetCloudCache(key, defaultValue);
#endif
        }
        #endregion

        #region 平台用户信息
        /// <summary>
        /// 同步平台昵称与头像，已授权则刷新，未授权则走首次授权入口，authStateCallBack 上报授权状态，显隐由业务层负责
        /// </summary>
        public void SyncPlatformUserInfo(RectTransform authAnchor, Action<PlatformAuthStates> authStateCallBack = null, Action<bool> userInfoCallBack = null)
        {
            if (m_platformUserInfoLoading)
            {
                userInfoCallBack?.Invoke(false);

                return;
            }

#if UNITY_EDITOR
            authStateCallBack?.Invoke(PlatformAuthStates.Unsupported);
            userInfoCallBack?.Invoke(false);

#elif MINIGAME_SUBPLATFORM_WEIXIN
            m_platformUserInfoLoading = true;
            RequestWeChatUserInfo(authStateCallBack, userInfoCallBack, authAnchor);

#elif MINIGAME_SUBPLATFORM_DOUYIN
            m_platformUserInfoLoading = true;
            RequestDouYinUserInfo(authStateCallBack, userInfoCallBack);
#endif
        }

        /// <summary>
        /// 读取内存中的平台昵称与头像，不触发平台调用
        /// </summary>
        public bool TryGetPlatformUserInfo(out string nickName, out string avatarUrl)
        {
            nickName = m_platformNickName;
            avatarUrl = m_platformAvatarUrl;

            return !string.IsNullOrEmpty(nickName) || !string.IsNullOrEmpty(avatarUrl);
        }

        /// <summary>
        /// 发起平台授权，authStateCallBack 上报授权状态，userInfoCallBack 返回资料获取结果，显隐由业务层负责
        /// </summary>
        public void RequestPlatformUserInfoAuth(RectTransform authAnchor, Action<PlatformAuthStates> authStateCallBack = null, Action<bool> userInfoCallBack = null)
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            if (m_platformUserInfoLoading)
            {
                return;
            }

            m_platformUserInfoLoading = true;
            int requestId = m_douYinUserInfoRequestId;
            TT.Authorize("scope.userInfo", (msg, data) =>
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                authStateCallBack?.Invoke(PlatformAuthStates.Authorized);
                RequestDouYinUserInfoDirect(authStateCallBack, userInfoCallBack, PlatformAuthStates.Authorized);
            }, (msg, err) =>
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                m_platformUserInfoLoading = false;
                GameLog.Info("抖音用户信息授权未完成");
                authStateCallBack?.Invoke(PlatformAuthStates.Denied);
            });
#endif
        }

        /// <summary>
        /// 销毁平台用户信息授权按钮
        /// </summary>
        public void DestroyPlatformUserInfoButton()
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            m_wxUserInfoRequestId++;
            DestroyWeChatUserInfoButton();
            m_platformUserInfoLoading = false;

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            m_douYinUserInfoRequestId++;
            m_platformUserInfoLoading = false;
#endif
        }

        /// <summary>
        /// 写入平台资料缓存并同步到云存档键
        /// </summary>
        private void ApplyPlatformUserInfo(string nickName, string avatarUrl, Action<bool> userInfoCallBack)
        {
            m_platformUserInfoLoading = false;

            if (string.IsNullOrEmpty(nickName) && string.IsNullOrEmpty(avatarUrl))
            {
                userInfoCallBack?.Invoke(false);

                return;
            }

            m_platformNickName = nickName;
            m_platformAvatarUrl = avatarUrl;

            if (!string.IsNullOrEmpty(nickName))
            {
                SetCloudData(CloudDataKeys.NickName, nickName);
            }

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                SetCloudData(CloudDataKeys.AvatarUrl, avatarUrl);
            }

            userInfoCallBack?.Invoke(true);
        }

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
        /// <summary>
        /// 微信已授权则直接取资料，未授权则先回调 NotAuthorized 再创建用户信息按钮，以便业务层先激活锚点
        /// </summary>
        private void RequestWeChatUserInfo(Action<PlatformAuthStates> authStateCallBack, Action<bool> userInfoCallBack, RectTransform authAnchor)
        {
            int requestId = m_wxUserInfoRequestId;
            WX.GetSetting(new GetSettingOption
            {
                success = res =>
                {
                    if (requestId != m_wxUserInfoRequestId)
                    {
                        return;
                    }

                    bool authorized = res.authSetting != null
                        && res.authSetting.TryGetValue("scope.userInfo", out bool value)
                        && value;

                    if (authorized)
                    {
                        authStateCallBack?.Invoke(PlatformAuthStates.Authorized);
                        RequestWeChatUserInfoDirect(userInfoCallBack);
                    }
                    else
                    {
                        authStateCallBack?.Invoke(PlatformAuthStates.NotAuthorized);
                        CreateWeChatUserInfoButton(authStateCallBack, userInfoCallBack, authAnchor);
                    }
                },
                fail = err =>
                {
                    if (requestId != m_wxUserInfoRequestId)
                    {
                        return;
                    }

                    GameLog.Error($"微信 GetSetting 失败: {err.errMsg}");
                    authStateCallBack?.Invoke(PlatformAuthStates.NotAuthorized);
                    CreateWeChatUserInfoButton(authStateCallBack, userInfoCallBack, authAnchor);
                }
            });
        }

        /// <summary>
        /// 微信已授权后直接拉取最新昵称头像
        /// </summary>
        private void RequestWeChatUserInfoDirect(Action<bool> userInfoCallBack)
        {
            WX.GetUserInfo(new GetUserInfoOption
            {
                lang = "zh_CN",
                withCredentials = false,
                success = res =>
                {
                    string nickName = res.userInfo.nickName;
                    string avatarUrl = res.userInfo.avatarUrl;
                    ApplyPlatformUserInfo(nickName, avatarUrl, userInfoCallBack);
                },
                fail = err =>
                {
                    m_platformUserInfoLoading = false;
                    GameLog.Error($"微信 GetUserInfo 失败: {err.errMsg}");
                    userInfoCallBack?.Invoke(false);
                }
            });
        }

        /// <summary>
        /// 创建微信用户信息授权按钮，点击后按结果上报状态，同意时销毁原生按钮，拒绝由业务层决定是否销毁
        /// </summary>
        private void CreateWeChatUserInfoButton(Action<PlatformAuthStates> authStateCallBack, Action<bool> userInfoCallBack, RectTransform authAnchor)
        {
            int requestId = m_wxUserInfoRequestId;
            DestroyWeChatUserInfoButton();
            GetScreenRectByNodePos(authAnchor, out Rect rect);
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            m_wxUserInfoButton = WX.CreateUserInfoButton(x, y, width, height, "zh_CN", false);
            m_wxUserInfoButton.OnTap(res =>
            {
                if (requestId != m_wxUserInfoRequestId)
                {
                    return;
                }

                bool hasUserInfo = res != null
                    && (!string.IsNullOrEmpty(res.userInfo.nickName) || !string.IsNullOrEmpty(res.userInfo.avatarUrl));
                bool ok = res != null
                    && (hasUserInfo || (!string.IsNullOrEmpty(res.errMsg) && res.errMsg.IndexOf(":ok", StringComparison.Ordinal) >= 0));

                if (!ok)
                {
                    m_platformUserInfoLoading = false;
                    GameLog.Info("微信用户信息授权未完成");
                    authStateCallBack?.Invoke(PlatformAuthStates.Denied);

                    return;
                }

                DestroyWeChatUserInfoButton();
                authStateCallBack?.Invoke(PlatformAuthStates.Authorized);
                string nickName = hasUserInfo ? res.userInfo.nickName : null;
                string avatarUrl = hasUserInfo ? res.userInfo.avatarUrl : null;
                ApplyPlatformUserInfo(nickName, avatarUrl, userInfoCallBack);
            });
        }

        /// <summary>
        /// 销毁微信用户信息授权按钮
        /// </summary>
        private void DestroyWeChatUserInfoButton()
        {
            if (m_wxUserInfoButton == null)
            {
                return;
            }

            m_wxUserInfoButton.OffTap();
            m_wxUserInfoButton.Destroy();
            m_wxUserInfoButton = null;
        }

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
        /// <summary>
        /// 抖音已授权则直接取资料，未授权则上报 NotAuthorized 由业务层显示授权入口
        /// </summary>
        private void RequestDouYinUserInfo(Action<PlatformAuthStates> authStateCallBack, Action<bool> userInfoCallBack)
        {
            int requestId = m_douYinUserInfoRequestId;
            TT.GetSetting((setting) =>
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                if (setting != null && setting.UserInfo)
                {
                    RequestDouYinUserInfoDirect(authStateCallBack, userInfoCallBack, PlatformAuthStates.NotAuthorized);
                }
                else
                {
                    m_platformUserInfoLoading = false;
                    authStateCallBack?.Invoke(PlatformAuthStates.NotAuthorized);
                    userInfoCallBack?.Invoke(false);
                }
            }, (err) =>
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                m_platformUserInfoLoading = false;
                GameLog.Info($"抖音 GetSetting 失败: {err}");
                authStateCallBack?.Invoke(PlatformAuthStates.NotAuthorized);
                userInfoCallBack?.Invoke(false);
            });
        }

        /// <summary>
        /// 抖音已授权后直接拉取最新昵称头像，userInfoFailState 在拉资料失败时上报，成功时上报 Authorized
        /// </summary>
        private void RequestDouYinUserInfoDirect(Action<PlatformAuthStates> authStateCallBack, Action<bool> userInfoCallBack, PlatformAuthStates userInfoFailState)
        {
            int requestId = m_douYinUserInfoRequestId;
            TT.GetUserInfo(false, delegate (ref TTUserInfo userInfo)
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                string nickName = userInfo != null ? userInfo.nickName : null;
                string avatarUrl = userInfo != null ? userInfo.avatarUrl : null;
                ApplyPlatformUserInfo(nickName, avatarUrl, userInfoCallBack);
                authStateCallBack?.Invoke(PlatformAuthStates.Authorized);
            }, err =>
            {
                if (requestId != m_douYinUserInfoRequestId)
                {
                    return;
                }

                m_platformUserInfoLoading = false;
                GameLog.Error($"抖音 GetUserInfo 失败: {err}");
                authStateCallBack?.Invoke(userInfoFailState);
                userInfoCallBack?.Invoke(false);
            });
        }
#endif
        #endregion

        #region 输入框
        /// <summary>
        /// 显示平台原生键盘并绑定输入框
        /// </summary>
        public void ShowKeyboard(TMP_InputField inputField)
        {
            if (m_isKeyboardShowing)
            {
                return;
            }

            m_inputField = inputField;

            m_isKeyboardShowing = true;

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            WX.ShowKeyboard(new ShowKeyboardOption
            {
                defaultValue = m_inputField.text,
                maxLength = m_inputField.characterLimit > 0 ? m_inputField.characterLimit : 140,
                confirmType = "done"
            });

            WX.OnKeyboardInput(KeyboardInput);
            WX.OnKeyboardConfirm(KeyboardConfirm);
            WX.OnKeyboardComplete(KeyboardComplete);

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            TT.ShowKeyboard(new ShowKeyboardOptions
            {
                defaultValue = m_inputField.text,
                maxLength = m_inputField.characterLimit > 0 ? m_inputField.characterLimit : 140,
                confirmType = "done"
            });

            TT.OnKeyboardInput += KeyboardInput;
            TT.OnKeyboardConfirm += KeyboardConfirm;
            TT.OnKeyboardComplete += KeyboardComplete;
#endif
        }

        /// <summary>
        /// 隐藏平台原生键盘并移除监听
        /// </summary>
        private void HideKeyboard()
        {
            if (!m_isKeyboardShowing)
            {
                return;
            }

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            WX.OffKeyboardInput(KeyboardInput);
            WX.OffKeyboardConfirm(KeyboardConfirm);
            WX.OffKeyboardComplete(KeyboardComplete);
            WX.HideKeyboard(new HideKeyboardOption()
            {
                success = (data) =>
                {
                    m_isKeyboardShowing = false;
                }
            });

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            TT.OnKeyboardInput -= KeyboardInput;
            TT.OnKeyboardConfirm -= KeyboardConfirm;
            TT.OnKeyboardComplete -= KeyboardComplete;
            TT.HideKeyboard(() =>
            {
                m_isKeyboardShowing = false;
            });
#endif
        }

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
        /// <summary>
        /// 微信键盘输入回调
        /// </summary>
        private void KeyboardInput(OnKeyboardInputListenerResult result)
        {
            m_inputField.text = result.value;
        }

        /// <summary>
        /// 微信键盘确认回调
        /// </summary>
        private void KeyboardConfirm(OnKeyboardInputListenerResult result)
        {
            m_inputField.text = result.value;
            HideKeyboard();
        }

        /// <summary>
        /// 微信键盘完成回调
        /// </summary>
        private void KeyboardComplete(OnKeyboardInputListenerResult result)
        {
            HideKeyboard();
        }

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
        /// <summary>
        /// 抖音键盘输入回调
        /// </summary>
        private void KeyboardInput(string result)
        {
            m_inputField.text = result;
        }

        /// <summary>
        /// 抖音键盘确认回调
        /// </summary>
        private void KeyboardConfirm(string result)
        {
            m_inputField.text = result;
            HideKeyboard();
        }

        /// <summary>
        /// 抖音键盘完成回调
        /// </summary>
        private void KeyboardComplete(string result)
        {
            HideKeyboard();
        }
#endif
        #endregion

        #region 屏幕适配
        /// <summary>
        /// 注册屏幕适配器并开启方向变化监听
        /// </summary>
        public void AddScreenAdapter(ScreenAdapter screenAdapter)
        {
            m_screenAdapters ??= new List<ScreenAdapter>();

            if (!m_screenAdapters.Contains(screenAdapter))
            {
                m_screenAdapters.Add(screenAdapter);
            }

            if (m_screenAdapters.Count == 1)
            {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
                WX.OnDeviceOrientationChange(DeviceOrientationChange);

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
                TT.OnDeviceOrientationChange(DeviceOrientationChange);
#endif
            }

            DeviceOrientationChange(screenAdapter);
        }

        /// <summary>
        /// 移除屏幕适配器，无剩余时关闭方向变化监听
        /// </summary>
        public void RemoveScreenAdapter(ScreenAdapter screenAdapter)
        {
            if (m_screenAdapters.Contains(screenAdapter))
            {
                m_screenAdapters.Remove(screenAdapter);
            }

            if (m_screenAdapters.Count <= 0)
            {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
                WX.OffDeviceOrientationChange(DeviceOrientationChange);

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
                TT.OffDeviceOrientationChange(DeviceOrientationChange);
#endif
            }
        }

        /// <summary>
        /// 按安全区刷新屏幕适配器锚点
        /// </summary>
        public void DeviceOrientationChange(ScreenAdapter screenAdapter = null)
        {
            if (screenAdapter != null)
            {
                GetSafeAnchor(out Vector2 offsetMin, out Vector2 offsetMax);
                RectTransform panel = screenAdapter.GetComponent<RectTransform>();
                panel.offsetMin = offsetMin;
                panel.offsetMax = offsetMax;
            }
            else
            {
                for (int i = 0; i < m_screenAdapters.Count; i++)
                {
                    GetSafeAnchor(out Vector2 offsetMin, out Vector2 offsetMax);
                    RectTransform panel = m_screenAdapters[i].GetComponent<RectTransform>();
                    panel.offsetMin = offsetMin;
                    panel.offsetMax = offsetMax;
                }
            }
        }

        /// <summary>
        /// 获取安全区域，固定写死
        /// </summary>
        /// <param name="offsetMin">相对于左下角的偏移量</param>
        /// <param name="offsetMax">相对于右上角的偏移量</param>
        public void GetSafeAnchor(out Vector2 offsetMin, out Vector2 offsetMax)
        {
            offsetMin = new Vector2(30, 130); // Left = 30, Bottom = 130
            offsetMax = new Vector2(-30, -90); // Right = 30, Top = 90
        }

        /// <summary>
        /// 由左上节点 topLeft pivot=(0,1) 与其子节点 Ts_Pos（右下）换算屏幕 Rect（物理像素，未除 pixelRatio）
        /// </summary>
        public static void GetScreenRectByNodePos(Transform topLeft, out Rect rect)
        {
            rect = default;

            if (topLeft == null)
            {
                return;
            }

            Vector2 pos1 = RectTransformUtility.WorldToScreenPoint(Utils.UICamera[0], topLeft.position);
            Transform bottomRight = topLeft.Find("Ts_Pos");

            if (bottomRight == null)
            {
                return;
            }

            Vector2 pos2 = RectTransformUtility.WorldToScreenPoint(Utils.UICamera[0], bottomRight.position);
            float width = pos2.x - pos1.x;
            float height = pos1.y - pos2.y;

            if (width <= 0f || height <= 0f)
            {
                return;
            }

            rect = new Rect(pos1.x, Screen.height - pos1.y, width, height);
        }

#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
        /// <summary>
        /// 微信设备方向变化回调
        /// </summary>
        private void DeviceOrientationChange(OnDeviceOrientationChangeListenerResult result)
        {
            DeviceOrientationChange();
        }

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
        /// <summary>
        /// 抖音设备方向变化回调
        /// </summary>
        private void DeviceOrientationChange(OnDeviceOrientationChangeResult result)
        {
            DeviceOrientationChange();
        }
#endif
        #endregion

        #region 广告
        /// <summary>
        /// 展示激励视频广告
        /// </summary>
        public void ShowRewardedVideoAd(Action<bool> callBack = null)
        {
            m_rewardedVideoAdCallBack = callBack;

#if UNITY_EDITOR
            m_rewardedVideoAdCallBack?.Invoke(true);

#elif MINIGAME_SUBPLATFORM_WEIXIN
            if (m_rewardedVideoAd != null)
            {
                m_rewardedVideoAd.Show();
                return;
            }

            m_rewardedVideoAd = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
            {
                adUnitId = InvariableConst.RewardedVideoAdUnitId
            });

            m_isShowRewardedVideoAd = true;
            m_rewardedVideoAd.OnLoad((res) =>
            {
                if (m_isShowRewardedVideoAd)
                {
                    m_isShowRewardedVideoAd = false;
                    m_rewardedVideoAd.Show();
                }
            });

            m_rewardedVideoAd.OnError((res) =>
            {
                GameLog.Error($"激励视频广告错误: {res.errMsg}");
            });

            m_rewardedVideoAd.OnClose((res) =>
            {
                m_rewardedVideoAdCallBack?.Invoke(res != null && res.isEnded);
            });

#elif MINIGAME_SUBPLATFORM_DOUYIN
            if (m_rewardedVideoAd != null)
            {
                m_rewardedVideoAd.Show();
                return;
            }

            m_rewardedVideoAd = TT.CreateRewardedVideoAd(new CreateRewardedVideoAdParam
            {
                AdUnitId = InvariableConst.RewardedVideoAdUnitId
            });

            m_isShowRewardedVideoAd = true;
            m_rewardedVideoAd.OnLoad += () =>
            {
                if (m_isShowRewardedVideoAd)
                {
                    m_isShowRewardedVideoAd = false;
                    m_rewardedVideoAd.Show();
                }
            };

            m_rewardedVideoAd.OnError += (code, message) =>
            {
                GameLog.Error($"激励视频广告错误: {message}");
            };

            m_rewardedVideoAd.OnClose += (isEnded, count) =>
            {
                m_rewardedVideoAdCallBack?.Invoke(isEnded);
            };
#endif
        }
        #endregion

        #region 侧边栏复访
        /// <summary>
        /// 打开平台侧边栏复访入口
        /// </summary>
        public void ShowSidebar()
        {
#if UNITY_EDITOR
            GameLog.Info("展示侧边栏");

#elif MINIGAME_SUBPLATFORM_WEIXIN
            GameLog.Info("展示侧边栏");

#elif MINIGAME_SUBPLATFORM_DOUYIN
            TT.CheckScene(TTSideBar.SceneEnum.SideBar, (isJump) =>
            {
                if (isJump)
                {
                    var data = new JsonData
                    {
                        ["scene"] = "sidebar",
                    };

                    TT.NavigateToScene(data, () =>
                    {
                        SetLocalData("IsGetReward", "1");
                    }, null, null);
                }
            }, null, null);
#endif
        }
        #endregion

        #region 游戏圈
        /// <summary>
        /// 展示微信游戏圈按钮
        /// </summary>
        public void ShowGameClubButton(RectTransform anchor, float fontSize = 0)
        {
#if UNITY_EDITOR
            GameLog.Info("展示游戏圈按钮");

#elif MINIGAME_SUBPLATFORM_WEIXIN
            if (m_wxGameClubButton == null)
            {
                var info = WX.GetWindowInfo();
                double pixelRatio = info.pixelRatio;

                GetScreenRectByNodePos(anchor, out Rect rect);

                m_wxGameClubButton = WX.CreateGameClubButton(new WXCreateGameClubButtonParam()
                {
                    type = GameClubButtonType.text,
                    text = "",
                    style = new GameClubButtonStyle()
                    {
                        //位置和大小
                        left = (int)(rect.x / pixelRatio),
                        top = (int)(rect.y / pixelRatio),
                        width = (int)(rect.width / pixelRatio),
                        height = (int)(rect.height / pixelRatio),

                        // 背景颜色
                        backgroundColor = "#FFFFFF00",

                        // 文字样式
                        color = "#BBDD88",
                        textAlign = GameClubButtonTextAlign.center,
                        fontSize = (int)(fontSize / pixelRatio),
                        lineHeight = (int)((fontSize + 10) / pixelRatio),

                        // 边框样式
                        borderColor = "#00000000",
                        borderWidth = 1,
                        //borderRadius = 30 // 圆角
                    }
                });
            }
            else
            {
                m_wxGameClubButton.Show();
            }

#elif MINIGAME_SUBPLATFORM_DOUYIN
            GameLog.Info("展示游戏圈按钮");
#endif
        }

        /// <summary>
        /// 隐藏微信游戏圈按钮
        /// </summary>
        public void HideGameClubButton()
        {
#if UNITY_EDITOR
            GameLog.Info("隐藏游戏圈按钮");

#elif MINIGAME_SUBPLATFORM_WEIXIN
            if (m_wxGameClubButton == null)
            {
                return;
            }

            m_wxGameClubButton.Hide();

#elif MINIGAME_SUBPLATFORM_DOUYIN
            GameLog.Info("隐藏游戏圈按钮");
#endif
        }
        #endregion

        #region 分享
        /// <summary>
        /// 调用平台分享
        /// </summary>
        public void Share(string desc)
        {
#if UNITY_EDITOR
            GameLog.Info("分享");

#elif MINIGAME_SUBPLATFORM_WEIXIN
            WX.ShareAppMessage(new ShareAppMessageOption
            {
                title = desc, // 小游戏名称和icon都会单独展示，这里写自定义文本
                imageUrl = "",
                query = ""
            });

#elif MINIGAME_SUBPLATFORM_DOUYIN
            var data = new JsonData
            {
                ["title"] = InvariableConst.ShareGameTitle, // 小游戏名称固定写上去
                ["desc"] = desc, // 自定义文本
                ["imageUrl"] = "",
                ["query"] = ""
            };

            TT.ShareAppMessage(data, (res) =>
            {
                GameLog.Info("分享成功");
            }, (errMsg) =>
            {
                GameLog.Error($"分享失败: {errMsg}");
            }, () =>
            {
                GameLog.Info("分享取消");
            });
#endif
        }
        #endregion

        #region 环境
        /// <summary>
        /// 当前是否微信小游戏运行时
        /// </summary>
        public bool IsWeChat()
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 当前是否抖音小游戏运行时
        /// </summary>
        public bool IsDouYin()
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            return true;
#else
            return false;
#endif
        }
        #endregion

        #region YooAsset
        /// <summary>
        /// 按平台配置 YooAsset WebPlayMode 文件系统参数
        /// </summary>
        public void InitializeYooAsset(ref WebPlayModeParameters createParameters, RemoteServices remoteServices)
        {
#if !UNITY_EDITOR && MINIGAME_SUBPLATFORM_WEIXIN
            string packageRoot = $"{WX.env.USER_DATA_PATH}/__GAME_FILE_CACHE/yoo";
            createParameters.WebServerFileSystemParameters = WechatFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);

#elif !UNITY_EDITOR && MINIGAME_SUBPLATFORM_DOUYIN
            string packageRoot = "yoo";
            createParameters.WebServerFileSystemParameters = TiktokFileSystemCreater.CreateFileSystemParameters(packageRoot, remoteServices);
#endif
        }

        /// <summary>
        /// 获取当前平台 CDN 根地址
        /// </summary>
        public string GetCDNPath()
        {
            return IsDouYin() ? InvariableConst.CDNPathDouYin : InvariableConst.CDNPathWeChat;
        }
        #endregion
    }
}