using CloudService;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Unity.UOS.Auth;
using Unity.UOS.CloudSave;
using Unity.UOS.CloudSave.Exception;
using Unity.UOS.CloudSave.Model.Files;



namespace Invariable
{
    public class CloudManager : Singleton<CloudManager>
    {
        private const string CloudSaveGameId = "MeteorWish"; // 每个游戏项目必须改为唯一值，并与 CloudHelper 内配置的 GameId 保持一致
        private const int CloudGetAllMaxCount = 100;
        private const float UploadDebounceSeconds = 2f;
        private const float TokenRefreshAheadSeconds = 600f; // 临期阈值：对齐 AuthTokenManager.RefreshGracePeriod（10 分钟）
        private static readonly string CloudSaveNamespace = $"kv_{CloudSaveGameId}_player";
        private Dictionary<string, string> m_cloudDataCache = null;
        private SemaphoreSlim m_cloudUploadLock = null;
        private static CloudHelper m_cloudHelper = null;
        private bool m_cloudDataDirty = false;
        private DateTime m_tokenExpiresAt = DateTime.MinValue;
        private string m_userId = null;

        /// <summary>
        /// 上传任务排队串行执行，同一时间只会执行一个上传任务
        /// </summary>
        private SemaphoreSlim CloudUploadLock
        {
            get
            {
                m_cloudUploadLock ??= new SemaphoreSlim(1, 1);

                return m_cloudUploadLock;
            }
        }

        /// <summary>
        /// UOS云函数类
        /// </summary>
        private static CloudHelper CloudHelper
        {
            get
            {
                m_cloudHelper ??= new CloudHelper();

                return m_cloudHelper;
            }
        }



        /// <summary>
        /// 初始化云存档数据
        /// </summary>
        public void InitCloudData(Action callBack = null)
        {
#if UNITY_EDITOR
            callBack?.Invoke();
#else
            InitCloudDataAsync(callBack).Forget();
#endif
        }

        /// <summary>
        /// 异步初始化云存档并登录平台
        /// </summary>
        private async UniTask InitCloudDataAsync(Action callBack)
        {
            try
            {
                await CloudSaveSDK.InitializeAsync();

                if (!await LoginAndSaveToken())
                {
                    return;
                }

                SaveItem saveItem = await CloudSaveSDK.Instance.Files.GetLinearAsync(CloudSaveNamespace);

                if (saveItem != null && !string.IsNullOrEmpty(saveItem.SaveId))
                {
                    byte[] fileBytes = await CloudSaveSDK.Instance.Files.LoadBytesAsync(saveItem.SaveId);
                    string json = Encoding.UTF8.GetString(fileBytes);

                    if (string.IsNullOrEmpty(json))
                    {
                        m_cloudDataCache = new Dictionary<string, string>();
                    }
                    else
                    {
                        m_cloudDataCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    }
                }
                else
                {
                    m_cloudDataCache = new Dictionary<string, string>();
                }

                callBack?.Invoke();
            }
            catch (Exception error)
            {
                GameLog.Error($"初始化云数据失败: {error}");
                m_cloudDataCache = null;
            }
        }

        /// <summary>
        /// 按排名字段拉取指定类型排行榜
        /// </summary>
        public void GetRankList(string rankKey, string rankType, Action<List<PlayerCloudData>> callBack = null)
        {
#if UNITY_EDITOR
            callBack?.Invoke(new List<PlayerCloudData>());
#else
            GetRankListAsync(rankKey, rankType, callBack).Forget();
#endif
        }

        /// <summary>
        /// 异步按排名字段拉取指定类型排行榜
        /// </summary>
        private async UniTask GetRankListAsync(string rankKey, string rankType, Action<List<PlayerCloudData>> callBack)
        {
            List<PlayerCloudData> result = new List<PlayerCloudData>();

            try
            {
                if (m_cloudDataCache == null)
                {
                    return;
                }

                string platform = null;

                if (SdkManager.Instance.IsWeChat())
                {
                    platform = "wx";
                }
                else if (SdkManager.Instance.IsDouYin())
                {
                    platform = "dy";
                }

                if (platform == null)
                {
                    GameLog.Error("获取排行榜失败: 当前环境不是微信或抖音");

                    return;
                }

                List<PlayerCloudData> saveList = await CloudHelper.GetRankList(CloudSaveGameId, rankKey, CloudGetAllMaxCount, platform, rankType);

                if (saveList != null)
                {
                    for (int i = 0; i < saveList.Count; i++)
                    {
                        PlayerCloudData save = saveList[i];

                        if (save == null)
                        {
                            continue;
                        }

                        if (save.Data == null)
                        {
                            save.Data = new Dictionary<string, string>();
                        }

                        result.Add(save);
                    }
                }
            }
            catch (Exception error)
            {
                GameLog.Error($"获取排行榜失败: {error}");
                result = new List<PlayerCloudData>();
            }
            finally
            {
                callBack?.Invoke(result);
            }
        }

        /// <summary>
        /// 上报排行分数，云函数同时维护世界榜与日榜
        /// </summary>
        public void ReportRankScore(string rankKey, double score, Action<bool> callBack = null)
        {
#if UNITY_EDITOR
            callBack?.Invoke(true);
#else
            ReportRankScoreAsync(rankKey, score, callBack).Forget();
#endif
        }

        /// <summary>
        /// 异步上报排行分数，云函数同时维护世界榜与日榜
        /// </summary>
        private async UniTask ReportRankScoreAsync(string rankKey, double score, Action<bool> callBack)
        {
            string platform = null;

            if (SdkManager.Instance.IsWeChat())
            {
                platform = "wx";
            }
            else if (SdkManager.Instance.IsDouYin())
            {
                platform = "dy";
            }

            if (m_cloudDataCache == null || string.IsNullOrEmpty(m_userId) || string.IsNullOrEmpty(rankKey) || platform == null || double.IsNaN(score) || double.IsInfinity(score))
            {
                callBack?.Invoke(false);

                return;
            }

            try
            {
                SdkManager.Instance.TryGetPlatformUserInfo(out string nickName, out string avatarUrl);
                bool entered = await CloudHelper.ReportRankScore(CloudSaveGameId, m_userId, rankKey, score, platform, nickName, avatarUrl);
                callBack?.Invoke(entered);
            }
            catch (Exception error)
            {
                GameLog.Error($"上报排行分数失败: {error}");
                callBack?.Invoke(false);
            }
        }

        /// <summary>
        /// 上传本地云缓存到远程存档
        /// </summary>
        private async UniTask UploadCloudData()
        {
            await CloudUploadLock.WaitAsync();

            try
            {
                if (!await EnsureTokenValid())
                {
                    GameLog.Error("云存档上传前令牌无效且重签失败");

                    return;
                }

                await SaveCloudDataInternal();
            }
            catch (Exception error) when (IsUnauthorized(error))
            {
                GameLog.Info("云存档上传遇 401，重新获取令牌后重试一次");

                try
                {
                    if (!await LoginAndSaveToken())
                    {
                        GameLog.Error("云存档上传 401 重试时重签失败");

                        return;
                    }

                    await SaveCloudDataInternal();
                }
                catch (Exception retryError)
                {
                    GameLog.Error($"上传云数据重试失败: {retryError}");
                }
            }
            catch (Exception error)
            {
                GameLog.Error($"上传云数据失败: {error}");
            }
            finally
            {
                CloudUploadLock.Release();
            }
        }

        /// <summary>
        /// 将本地云缓存序列化并写入远程存档
        /// </summary>
        private async UniTask SaveCloudDataInternal()
        {
            string saveName = "玩家数据";

            if (SdkManager.Instance.IsWeChat())
            {
                saveName = "微信玩家数据";
            }
            else if (SdkManager.Instance.IsDouYin())
            {
                saveName = "抖音玩家数据";
            }

            Dictionary<string, string> uploadData = new Dictionary<string, string>(m_cloudDataCache);

            if (!string.IsNullOrEmpty(m_userId))
            {
                uploadData[CloudDataKeys.UserId] = m_userId;
            }

            if (SdkManager.Instance.TryGetPlatformUserInfo(out string nickName, out string avatarUrl))
            {
                if (!string.IsNullOrEmpty(nickName))
                {
                    uploadData[CloudDataKeys.NickName] = nickName;
                }

                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    uploadData[CloudDataKeys.AvatarUrl] = avatarUrl;
                }
            }

            string json = JsonConvert.SerializeObject(uploadData);
            byte[] fileBytes = Encoding.UTF8.GetBytes(json);
            await CloudSaveSDK.Instance.Files.SaveLinearAsync(CloudSaveNamespace, new UpdateOptions
            {
                Name = saveName,
                File = new FileOptions
                {
                    UpdateFileWay = UpdateFileWay.ByFileBytes,
                    FileBytes = fileBytes
                }
            });
        }

        /// <summary>
        /// 平台登录并换取云存档令牌后写入 AuthTokenManager
        /// </summary>
        private async UniTask<bool> LoginAndSaveToken()
        {
            string code = await SdkManager.Instance.PlatformLogin();

            if (string.IsNullOrEmpty(code))
            {
                GameLog.Error("平台登录未获取到 code");

                return false;
            }

            PlatformLoginResult loginResult = null;

            if (SdkManager.Instance.IsWeChat())
            {
                loginResult = await CloudHelper.WechatLogin(CloudSaveGameId, code);
            }
            else if (SdkManager.Instance.IsDouYin())
            {
                loginResult = await CloudHelper.DouyinLogin(CloudSaveGameId, code);
            }

            if (loginResult == null || string.IsNullOrEmpty(loginResult.accessToken))
            {
                GameLog.Error("云函数登录失败");

                return false;
            }

            AuthTokenManager.SaveToken(new TokenInfo
            {
                AccessToken = loginResult.accessToken,
                UserId = loginResult.userID
            });
            m_userId = loginResult.userID;
            m_tokenExpiresAt = ResolveTokenExpiresAt(loginResult.accessToken, loginResult.expiresAt);

            return true;
        }

        /// <summary>
        /// 令牌有效则直接返回，临期或过期则重新登录签新令牌
        /// </summary>
        private async UniTask<bool> EnsureTokenValid()
        {
            TimeSpan remain = m_tokenExpiresAt - DateTime.UtcNow;

            if (remain.TotalSeconds > TokenRefreshAheadSeconds)
            {
                return true;
            }

            return await LoginAndSaveToken();
        }

        /// <summary>
        /// 优先用 AccessToken JWT 的 Expiration，与 SDK 临期判断对齐，解码失败则回退 expiresAt
        /// </summary>
        private static DateTime ResolveTokenExpiresAt(string accessToken, long expiresAt)
        {
            try
            {
                DateTime jwtExpiresAt = JsonWebToken.Decode(accessToken).Expiration;

                if (jwtExpiresAt.Year > 1970)
                {
                    return jwtExpiresAt;
                }
            }
            catch (Exception error)
            {
                GameLog.Error($"解析 AccessToken JWT 过期时间失败，回退 expiresAt: {error.Message}");
            }

            return ParseTokenExpiresAt(expiresAt);
        }

        /// <summary>
        /// 解析令牌过期时间，expiresAt 可能是 Unix 秒、Unix 毫秒或剩余秒数
        /// </summary>
        private static DateTime ParseTokenExpiresAt(long expiresAt)
        {
            if (expiresAt <= 0)
            {
                return DateTime.UtcNow.AddHours(1);
            }

            if (expiresAt > 1000000000000L)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).UtcDateTime;
            }

            if (expiresAt > 365L * 24 * 3600)
            {
                return DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime;
            }

            return DateTime.UtcNow.AddSeconds(expiresAt);
        }

        /// <summary>
        /// 判定是否为令牌失效（401 / InvalidToken / TokenExpired）
        /// </summary>
        private static bool IsUnauthorized(Exception error)
        {
            if (error is CloudSaveClientException clientException
                && (clientException.ErrorCode == 51 || clientException.ErrorCode == 52))
            {
                return true;
            }

            string text = error.Message ?? string.Empty;

            return text.IndexOf("401", StringComparison.Ordinal) >= 0
                || text.IndexOf("AccessTokenInvalid", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("InvalidToken", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("TokenExpired", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("NeedLogin", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 写入云缓存并触发防抖上传
        /// </summary>
        internal void SetCloudCache(string key, string data)
        {
            if (m_cloudDataCache == null)
            {
                return;
            }

            m_cloudDataCache[key] = data;
            m_cloudDataDirty = true;
            ScheduleDebouncedUpload();
        }

        /// <summary>
        /// 读取云缓存值
        /// </summary>
        internal string GetCloudCache(string key, string defaultValue = "")
        {
            if (m_cloudDataCache == null)
            {
                return defaultValue;
            }

            if (m_cloudDataCache.TryGetValue(key, out string data) && !string.IsNullOrEmpty(data))
            {
                return data;
            }

            return defaultValue;
        }

        /// <summary>
        /// 立即上传脏数据（取消进行中的防抖）
        /// </summary>
        public void FlushCloudData()
        {
            if (GameManager.HasInstance)
            {
                GameManager.Instance.CancelInvokeByKey(InvariableConst.Timer_CloudManager_UploadDebounce);
            }

            FlushCloudDataAsync().Forget();
        }

        /// <summary>
        /// 安排防抖上传
        /// </summary>
        private void ScheduleDebouncedUpload()
        {
            if (!GameManager.HasInstance)
            {
                FlushCloudDataAsync().Forget();

                return;
            }

            GameManager.Instance.CancelInvokeByKey(InvariableConst.Timer_CloudManager_UploadDebounce);
            GameManager.Instance.DelayCallSeconds(InvariableConst.Timer_CloudManager_UploadDebounce, () =>
            {
                FlushCloudDataAsync().Forget();
            }, UploadDebounceSeconds);
        }

        /// <summary>
        /// 异步上传脏数据；上传期间再次写入会重新标记 dirty
        /// </summary>
        private async UniTask FlushCloudDataAsync()
        {
            if (!m_cloudDataDirty || m_cloudDataCache == null)
            {
                return;
            }

            m_cloudDataDirty = false;
            await UploadCloudData();

            if (m_cloudDataDirty)
            {
                ScheduleDebouncedUpload();
            }
        }
    }
}