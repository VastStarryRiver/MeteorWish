using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.UOS.Func.Stateless.Core.Attributes;
using UnityEngine;



namespace CloudService
{
    // 注意事项
    //
    // 1. 云函数类所在脚本文件的名称必须与类名相同
    //
    // 2. 所有的类必须放置于命名空间内，且所用到的代码文件必须放到同一目录中
    //
    // 3. 使用 [CloudService] 标记有远程调用函数的类，使用 [CloudFunc] 标记需要远程调用的函数
    //
    // 4. 请在云函数类构造函数中初始化云函数，不要创建并调用其他带有参数的构造函数
    //
    // 5. 切换到远程模式后云函数类只会保留带有 [CloudFunc] 的方法，其他字段将会被隐藏
    //
    // 6. 使用 [CloudFunc] 标记的函数必须符合 public async Task<返回数据类型> 函数名称(输出参数) { 函数体 } 这样的格式
    //
    // 7. 编写代码中只能使用 UnityEngine 命名空间下的 Debug.Log，Debug.LogWarning，Debug.LogError 函数，不能使用其他函数
    //
    // 8. 打包之前先切换远程调用模式
    //

    [CloudService]
    public class CloudHelper
    {
        private class GenerateTokenResponse
        {
            public string accessToken;
            public long expiresAt;
        }

        private class RankSnapshot
        {
            public string saveId;
            public string rankDate;
            public List<PlayerCloudData> entries;
        }

        private class RankSnapshotPayload
        {
            public string rankDate;
            public List<PlayerCloudData> entries;
        }

        private const string WorldRankSnapshotUserId = "rank_world";
        private const string DayRankSnapshotUserId = "rank_day";
        private const int LeaderboardCapacity = 100;
        private const int DayRankFreezeEndHour = 5;

        // GameId 必须与客户端 CloudManager.CloudSaveGameId 一致，并填入微信/抖音 AppID/AppSecret
        private static readonly GameSecrets Secrets = new GameSecrets
        {
            GameId = "MeteorWish",
            WechatAppId = "",
            WechatAppSecret = "",
            DouyinAppId = "",
            DouyinAppSecret = ""
        };



        public CloudHelper()
        {
        }



        /// <summary>
        /// 微信小游戏登录，用 code 换取 openid 并换取云存档令牌
        /// </summary>
        [CloudFunc]
        public async Task<PlatformLoginResult> WechatLogin(string gameId, string code)
        {
            if (!TryGetGameSecrets(gameId, out GameSecrets secrets))
            {
                return null;
            }

            if (string.IsNullOrEmpty(secrets.WechatAppId) || string.IsNullOrEmpty(secrets.WechatAppSecret))
            {
                Debug.LogError($"WechatLogin: 游戏 {gameId} 未配置微信 AppID/AppSecret");

                return null;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://api.weixin.qq.com/sns/jscode2session?appid={secrets.WechatAppId}&secret={secrets.WechatAppSecret}&js_code={Uri.EscapeDataString(code)}&grant_type=authorization_code";
                    HttpResponseMessage response = await client.GetAsync(url);
                    string body = await response.Content.ReadAsStringAsync();
                    WeixinLoginResult result = JsonConvert.DeserializeObject<WeixinLoginResult>(body);

                    if (result == null || result.errcode != 0 || string.IsNullOrEmpty(result.openid))
                    {
                        Debug.LogError($"WechatLogin: jscode2session 失败 errcode={result?.errcode}, errmsg={result?.errmsg}");

                        return null;
                    }

                    return await GenerateToken(client, "wx-" + result.openid);
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"WechatLogin 异常: {error.Message}");

                return null;
            }
        }

        /// <summary>
        /// 抖音小游戏登录，用 code 换取 openid 并换取云存档令牌
        /// </summary>
        [CloudFunc]
        public async Task<PlatformLoginResult> DouyinLogin(string gameId, string code)
        {
            if (!TryGetGameSecrets(gameId, out GameSecrets secrets))
            {
                return null;
            }

            if (string.IsNullOrEmpty(secrets.DouyinAppId) || string.IsNullOrEmpty(secrets.DouyinAppSecret))
            {
                Debug.LogError($"DouyinLogin: 游戏 {gameId} 未配置抖音 AppID/AppSecret");

                return null;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://minigame.zijieapi.com/mgplatform/api/apps/jscode2session?appid={secrets.DouyinAppId}&secret={secrets.DouyinAppSecret}&code={Uri.EscapeDataString(code)}";
                    HttpResponseMessage response = await client.GetAsync(url);
                    string body = await response.Content.ReadAsStringAsync();
                    DouyinLoginResult result = JsonConvert.DeserializeObject<DouyinLoginResult>(body);

                    if (result == null || result.error != 0 || string.IsNullOrEmpty(result.openid))
                    {
                        string errText = !string.IsNullOrEmpty(result?.errmsg) ? result.errmsg : result?.message;
                        Debug.LogError($"DouyinLogin: jscode2session 失败 error={result?.error}, msg={errText}");

                        return null;
                    }

                    return await GenerateToken(client, "dy-" + result.openid);
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"DouyinLogin 异常: {error.Message}");

                return null;
            }
        }

        /// <summary>
        /// 读取指定类型排行榜快照，截取前 maxCount 名
        /// </summary>
        [CloudFunc]
        public async Task<List<PlayerCloudData>> GetRankList(string gameId, string rankKey, int maxCount, string platform, string rankType)
        {
            // 禁止客户端直接指定命名空间，避免共享 UOS App 时跨游戏拉取
            if (!TryGetGameSecrets(gameId, out _))
            {
                return null;
            }

            if (!IsValidPlatform(platform))
            {
                Debug.LogError("GetRankList: platform 无效");

                return new List<PlayerCloudData>();
            }

            if (!IsValidRankType(rankType))
            {
                Debug.LogError("GetRankList: rankType 无效");

                return new List<PlayerCloudData>();
            }

            if (string.IsNullOrEmpty(rankKey))
            {
                Debug.LogError("GetRankList: rankKey 不能为空");

                return null;
            }

            if (maxCount <= 0)
            {
                return new List<PlayerCloudData>();
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    ApplyBasicAuth(client);
                    RankSnapshot snapshot = await LoadRankSnapshot(client, gameId, platform, GetRankSnapshotUserId(rankType));

                    if (rankType == CloudRankTypes.Day && snapshot.rankDate != GetDayRankViewDate())
                    {
                        return new List<PlayerCloudData>();
                    }

                    List<PlayerCloudData> results = snapshot.entries;

                    if (results.Count > maxCount)
                    {
                        results.RemoveRange(maxCount, results.Count - maxCount);
                    }

                    return results;
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"GetRankList 异常: {error.Message}");

                return null;
            }
        }

        /// <summary>
        /// 定时任务，每天 5 点（UTC+8）清空微信与抖音日榜快照
        /// </summary>
        [CloudFunc(CronJob = true)]
        public async Task ResetDayRank()
        {
            string gameId = Secrets.GameId;
            string writeDate = GetDayRankWriteDate();
            string[] platforms = { "wx", "dy" };

            using (HttpClient client = new HttpClient())
            {
                ApplyBasicAuth(client);

                for (int i = 0; i < platforms.Length; i++)
                {
                    string platform = platforms[i];

                    try
                    {
                        RankSnapshot snapshot = await LoadRankSnapshot(client, gameId, platform, DayRankSnapshotUserId);
                        List<PlayerCloudData> entries = snapshot.entries ?? new List<PlayerCloudData>();

                        if (snapshot.rankDate == writeDate && entries.Count == 0)
                        {
                            continue;
                        }

                        await SaveRankSnapshot(client, gameId, platform, DayRankSnapshotUserId, CloudRankTypes.Day, snapshot.saveId, new List<PlayerCloudData>(), writeDate);
                    }
                    catch (Exception error)
                    {
                        Debug.LogError($"ResetDayRank {platform} 失败: {error.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 上报排行分数，同时维护世界榜与日榜，返回本次是否至少更新一榜
        /// </summary>
        [CloudFunc]
        public async Task<bool> ReportRankScore(string gameId, string userId, string rankKey, double score, string platform, string nickName, string avatarUrl)
        {
            if (!TryGetGameSecrets(gameId, out _))
            {
                return false;
            }

            if (!IsValidPlatform(platform))
            {
                Debug.LogError("ReportRankScore: platform 无效");

                return false;
            }

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(rankKey) || double.IsNaN(score) || double.IsInfinity(score))
            {
                Debug.LogError("ReportRankScore: userId、rankKey 或 score 无效");

                return false;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    ApplyBasicAuth(client);
                    bool worldUpdated = await TryUpdateRankSnapshot(client, gameId, platform, CloudRankTypes.World, userId, rankKey, score, nickName, avatarUrl);
                    bool dayUpdated = false;

                    if (IsDayRankWritable())
                    {
                        dayUpdated = await TryUpdateRankSnapshot(client, gameId, platform, CloudRankTypes.Day, userId, rankKey, score, nickName, avatarUrl);
                    }

                    return worldUpdated || dayUpdated;
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"ReportRankScore 异常: {error.Message}");

                throw;
            }
        }



        /// <summary>
        /// 按快照用户读取排行榜，不存在时返回空列表
        /// </summary>
        private async Task<RankSnapshot> LoadRankSnapshot(HttpClient client, string gameId, string platform, string snapshotUserId)
        {
            RankSnapshot snapshot = new RankSnapshot
            {
                saveId = null,
                rankDate = null,
                entries = new List<PlayerCloudData>()
            };
            string namespaces = GetRankNamespace(gameId, platform);
            string listUrl = $"https://save.unity.cn/v1/saves?namespaces={Uri.EscapeDataString(namespaces)}&userId={Uri.EscapeDataString(snapshotUserId)}&start=0&count=1&skipTotal=true";
            HttpResponseMessage listResponse = await client.GetAsync(listUrl);

            if (!listResponse.IsSuccessStatusCode)
            {
                string errBody = await listResponse.Content.ReadAsStringAsync();
                Debug.LogError($"LoadRankSnapshot: 列表请求失败 status={(int)listResponse.StatusCode}, body={errBody}");
                listResponse.EnsureSuccessStatusCode();
            }

            string listBody = await listResponse.Content.ReadAsStringAsync();
            ListSavesResponse listResult = JsonConvert.DeserializeObject<ListSavesResponse>(listBody);
            SaveInfo info = listResult?.saves != null && listResult.saves.Count > 0 ? listResult.saves[0] : null;

            if (info == null || string.IsNullOrEmpty(info.saveId))
            {
                return snapshot;
            }

            snapshot.saveId = info.saveId;
            string getUrl = $"https://save.unity.cn/v1/saves/{Uri.EscapeDataString(info.saveId)}?includeDownloadURL=true";
            HttpResponseMessage getResponse = await client.GetAsync(getUrl);

            if (!getResponse.IsSuccessStatusCode)
            {
                Debug.LogError($"LoadRankSnapshot: 获取存档失败 saveId={info.saveId}, status={(int)getResponse.StatusCode}");
                getResponse.EnsureSuccessStatusCode();
            }

            string getBody = await getResponse.Content.ReadAsStringAsync();
            GetSaveResponse getResult = JsonConvert.DeserializeObject<GetSaveResponse>(getBody);
            string fileUrl = getResult?.save?.file?.fileURL;

            if (string.IsNullOrEmpty(fileUrl))
            {
                FailRankWrite($"LoadRankSnapshot: 存档缺少下载地址 saveId={info.saveId}");

                return snapshot;
            }

            HttpResponseMessage fileResponse = await client.GetAsync(fileUrl);

            if (!fileResponse.IsSuccessStatusCode)
            {
                Debug.LogError($"LoadRankSnapshot: 下载存档内容失败 saveId={info.saveId}, status={(int)fileResponse.StatusCode}");
                fileResponse.EnsureSuccessStatusCode();
            }

            string fileText = await fileResponse.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(fileText))
            {
                return snapshot;
            }

            FillSnapshotFromFile(snapshot, fileText);

            return snapshot;
        }

        /// <summary>
        /// 将排行榜快照写回独立 namespace
        /// </summary>
        private async Task SaveRankSnapshot(HttpClient client, string gameId, string platform, string snapshotUserId, string rankType, string saveId, List<PlayerCloudData> entries, string rankDate)
        {
            UploadTokenRequest tokenRequest = new UploadTokenRequest
            {
                userId = snapshotUserId,
                saveId = saveId,
                fileUploadRequest = new FileUploadSpec
                {
                    format = "",
                    originalName = "rank.json"
                }
            };
            string tokenJson = JsonConvert.SerializeObject(tokenRequest, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            HttpContent tokenContent = new StringContent(tokenJson, Encoding.UTF8, "application/json");
            HttpResponseMessage tokenResponse = await client.PostAsync("https://save.unity.cn/v1/saves/upload-token", tokenContent);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                string errBody = await tokenResponse.Content.ReadAsStringAsync();
                Debug.LogError($"SaveRankSnapshot: 获取上传令牌失败 status={(int)tokenResponse.StatusCode}, body={errBody}");
                tokenResponse.EnsureSuccessStatusCode();
            }

            string tokenBody = await tokenResponse.Content.ReadAsStringAsync();
            UploadTokenResponse tokenResult = JsonConvert.DeserializeObject<UploadTokenResponse>(tokenBody);

            if (tokenResult == null || tokenResult.fileUploadToken == null || string.IsNullOrEmpty(tokenResult.fileUploadToken.objectId))
            {
                FailRankWrite("SaveRankSnapshot: 上传令牌返回数据无效");

                return;
            }

            string resolvedSaveId = !string.IsNullOrEmpty(tokenResult.saveId) ? tokenResult.saveId : saveId;

            if (string.IsNullOrEmpty(resolvedSaveId))
            {
                FailRankWrite("SaveRankSnapshot: 上传令牌未返回 saveId");

                return;
            }

            RankSnapshotPayload payload = new RankSnapshotPayload
            {
                rankDate = rankDate,
                entries = entries
            };
            byte[] fileBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            await UploadObjectToCos(tokenResult.fileUploadToken, fileBytes);
            CreateSaveRequest createRequest = new CreateSaveRequest
            {
                userId = snapshotUserId,
                saveId = resolvedSaveId,
                name = GetRankSnapshotName(platform, rankType),
                saveNamespace = GetRankNamespace(gameId, platform),
                progressType = "LINEAR",
                fileUploadRequest = new FileUploadConfirmation
                {
                    clearExisting = false,
                    objectId = tokenResult.fileUploadToken.objectId
                }
            };
            string createJson = JsonConvert.SerializeObject(createRequest);
            HttpContent createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
            HttpResponseMessage createResponse = await client.PostAsync("https://save.unity.cn/v1/saves", createContent);

            if (!createResponse.IsSuccessStatusCode)
            {
                string errBody = await createResponse.Content.ReadAsStringAsync();
                Debug.LogError($"SaveRankSnapshot: 创建或更新存档失败 status={(int)createResponse.StatusCode}, body={errBody}");
                createResponse.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 使用临时密钥将快照文件上传到 COS
        /// </summary>
        private static async Task UploadObjectToCos(UploadToken token, byte[] fileBytes)
        {
            if (string.IsNullOrEmpty(token.tmpSecretId) || string.IsNullOrEmpty(token.tmpSecretKey) || string.IsNullOrEmpty(token.token)
                || string.IsNullOrEmpty(token.objectName))
            {
                FailRankWrite("UploadObjectToCos: 上传令牌字段不完整");

                return;
            }

            string objectPath = BuildCosObjectPath(token.objectDir, token.objectName);
            string host = "";
            string url = "";

            if (!string.IsNullOrEmpty(token.bucketUrl))
            {
                Uri bucketUri = new Uri(token.bucketUrl);
                host = bucketUri.Host;
                url = token.bucketUrl.TrimEnd('/') + objectPath;
            }
            else if (!string.IsNullOrEmpty(token.bucketName) && !string.IsNullOrEmpty(token.region))
            {
                host = $"{token.bucketName}.cos.{token.region}.myqcloud.com";
                url = $"https://{host}{objectPath}";
            }
            else
            {
                FailRankWrite("UploadObjectToCos: 缺少 bucketUrl 或 bucketName/region");

                return;
            }

            long startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string keyTime = $"{startTime};{startTime + 600}";
            string signKey = HmacSha1Hex(token.tmpSecretKey, keyTime);
            string httpHeaders = $"host={Uri.EscapeDataString(host)}&x-cos-security-token={Uri.EscapeDataString(token.token)}";
            string httpString = $"put\n{objectPath}\n\n{httpHeaders}\n";
            string stringToSign = $"sha1\n{keyTime}\n{Sha1Hex(httpString)}\n";
            string signature = HmacSha1Hex(signKey, stringToSign);
            string authorization = $"q-sign-algorithm=sha1&q-ak={token.tmpSecretId}&q-sign-time={keyTime}&q-key-time={keyTime}&q-header-list=host;x-cos-security-token&q-url-param-list=&q-signature={signature}";

            using (HttpClient cosClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
                request.Headers.TryAddWithoutValidation("x-cos-security-token", token.token);
                request.Content = new ByteArrayContent(fileBytes);
                HttpResponseMessage response = await cosClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"UploadObjectToCos: 上传失败 status={(int)response.StatusCode}, body={errBody}");
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>
        /// 排行榜写路径失败时中止调用，避免客户端把失败当成已处理
        /// </summary>
        private static void FailRankWrite(string message)
        {
            Debug.LogError(message);

            using (HttpResponseMessage failed = new HttpResponseMessage(HttpStatusCode.BadGateway))
            {
                failed.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// 按 rankKey 数值降序排列快照条目
        /// </summary>
        private static void SortRankEntries(List<PlayerCloudData> entries, string rankKey)
        {
            entries.Sort((left, right) =>
            {
                double leftScore = GetRankScore(left?.Data, rankKey);
                double rightScore = GetRankScore(right?.Data, rankKey);

                return rightScore.CompareTo(leftScore);
            });
        }

        /// <summary>
        /// 查找指定用户在快照中的下标，未找到返回 -1
        /// </summary>
        private static int FindRankEntryIndex(List<PlayerCloudData> entries, string userId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].UserId == userId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 非空昵称头像写入排行榜条目顶层，空值保留旧资料
        /// </summary>
        private static void ApplyProfileData(PlayerCloudData entry, string nickName, string avatarUrl)
        {
            if (entry == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(nickName))
            {
                entry.NickName = nickName;
            }

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                entry.AvatarUrl = avatarUrl;
            }
        }

        /// <summary>
        /// 校验平台标识是否为 wx 或 dy
        /// </summary>
        private static bool IsValidPlatform(string platform)
        {
            return platform == "wx" || platform == "dy";
        }

        /// <summary>
        /// 拼接排行榜快照 namespace
        /// </summary>
        private static string GetRankNamespace(string gameId, string platform)
        {
            return $"kv_{gameId}_rank_{platform}";
        }

        /// <summary>
        /// 校验排行榜类型是否为 world 或 day
        /// </summary>
        private static bool IsValidRankType(string rankType)
        {
            return rankType == CloudRankTypes.World || rankType == CloudRankTypes.Day;
        }

        /// <summary>
        /// 按排行榜类型返回快照归属用户
        /// </summary>
        private static string GetRankSnapshotUserId(string rankType)
        {
            if (rankType == CloudRankTypes.Day)
            {
                return DayRankSnapshotUserId;
            }

            return WorldRankSnapshotUserId;
        }

        /// <summary>
        /// 后台显示名，仅展示不参与定位
        /// </summary>
        private static string GetRankSnapshotName(string platform, string rankType)
        {
            bool isDay = rankType == CloudRankTypes.Day;

            if (platform == "wx")
            {
                return isDay ? "微信每日排行榜" : "微信世界排行榜";
            }

            if (platform == "dy")
            {
                return isDay ? "抖音每日排行榜" : "抖音世界排行榜";
            }

            return isDay ? "每日排行榜" : "世界排行榜";
        }

        /// <summary>
        /// 以 UTC+8 计算当前时间
        /// </summary>
        private static DateTime GetChinaNow()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        /// <summary>
        /// 日榜 5 点后才允许写入
        /// </summary>
        private static bool IsDayRankWritable()
        {
            return GetChinaNow().Hour >= DayRankFreezeEndHour;
        }

        /// <summary>
        /// 日榜读取日期，0-5 点取前一天
        /// </summary>
        private static string GetDayRankViewDate()
        {
            DateTime chinaNow = GetChinaNow();
            DateTime viewDate = chinaNow.Date;

            if (chinaNow.Hour < DayRankFreezeEndHour)
            {
                viewDate = viewDate.AddDays(-1);
            }

            return viewDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 日榜写入日期，仅在可写窗口使用
        /// </summary>
        private static string GetDayRankWriteDate()
        {
            return GetChinaNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 解析快照文件，兼容旧版纯列表格式
        /// </summary>
        private static void FillSnapshotFromFile(RankSnapshot snapshot, string fileText)
        {
            if (string.IsNullOrEmpty(fileText))
            {
                return;
            }

            string trimmed = fileText.TrimStart();

            if (trimmed.StartsWith("["))
            {
                snapshot.entries = JsonConvert.DeserializeObject<List<PlayerCloudData>>(fileText) ?? new List<PlayerCloudData>();

                return;
            }

            RankSnapshotPayload payload = JsonConvert.DeserializeObject<RankSnapshotPayload>(fileText);

            if (payload == null)
            {
                snapshot.entries = new List<PlayerCloudData>();

                return;
            }

            snapshot.rankDate = payload.rankDate;
            snapshot.entries = payload.entries ?? new List<PlayerCloudData>();
        }

        /// <summary>
        /// 加载并按 Top100 规则更新指定类型快照
        /// </summary>
        private async Task<bool> TryUpdateRankSnapshot(HttpClient client, string gameId, string platform, string rankType, string userId, string rankKey, double score, string nickName, string avatarUrl)
        {
            string snapshotUserId = GetRankSnapshotUserId(rankType);
            RankSnapshot snapshot = await LoadRankSnapshot(client, gameId, platform, snapshotUserId);
            string rankDate = snapshot.rankDate;

            if (rankType == CloudRankTypes.Day)
            {
                string writeDate = GetDayRankWriteDate();

                if (snapshot.rankDate != writeDate)
                {
                    snapshot.entries = new List<PlayerCloudData>();
                    rankDate = writeDate;
                }
            }

            if (!TryApplyRankEntries(snapshot.entries, userId, rankKey, score, nickName, avatarUrl))
            {
                return false;
            }

            await SaveRankSnapshot(client, gameId, platform, snapshotUserId, rankType, snapshot.saveId, snapshot.entries, rankDate);

            return true;
        }

        /// <summary>
        /// 按现有上榜规则写入条目，返回本次是否更新
        /// </summary>
        private static bool TryApplyRankEntries(List<PlayerCloudData> entries, string userId, string rankKey, double score, string nickName, string avatarUrl)
        {
            SortRankEntries(entries, rankKey);
            int existingIndex = FindRankEntryIndex(entries, userId);

            if (existingIndex >= 0)
            {
                if (entries[existingIndex].Data == null)
                {
                    entries[existingIndex].Data = new Dictionary<string, string>();
                }

                entries[existingIndex].Data[rankKey] = score.ToString(CultureInfo.InvariantCulture);
                ApplyProfileData(entries[existingIndex], nickName, avatarUrl);
            }
            else
            {
                if (entries.Count >= LeaderboardCapacity)
                {
                    double lastScore = GetRankScore(entries[entries.Count - 1].Data, rankKey);

                    if (score <= lastScore)
                    {
                        return false;
                    }

                    entries.RemoveAt(entries.Count - 1);
                }
                else if (score <= 0)
                {
                    return false;
                }

                PlayerCloudData entry = new PlayerCloudData
                {
                    UserId = userId,
                    Data = new Dictionary<string, string>
                    {
                        { rankKey, score.ToString(CultureInfo.InvariantCulture) }
                    }
                };
                ApplyProfileData(entry, nickName, avatarUrl);
                entries.Add(entry);
            }

            SortRankEntries(entries, rankKey);

            if (entries.Count > LeaderboardCapacity)
            {
                entries.RemoveRange(LeaderboardCapacity, entries.Count - LeaderboardCapacity);
            }

            return true;
        }

        /// <summary>
        /// 拼接 COS 对象路径并对每一段做 URL 编码
        /// </summary>
        private static string BuildCosObjectPath(string objectDir, string objectName)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('/');

            if (!string.IsNullOrEmpty(objectDir))
            {
                string[] dirParts = objectDir.Split('/');

                for (int i = 0; i < dirParts.Length; i++)
                {
                    if (string.IsNullOrEmpty(dirParts[i]))
                    {
                        continue;
                    }

                    builder.Append(Uri.EscapeDataString(dirParts[i]));
                    builder.Append('/');
                }
            }

            builder.Append(Uri.EscapeDataString(objectName));

            return builder.ToString();
        }

        /// <summary>
        /// 计算 UTF-8 文本的 SHA1 十六进制摘要
        /// </summary>
        private static string Sha1Hex(string text)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                return BytesToHex(sha1.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        /// <summary>
        /// 计算 HMAC-SHA1 十六进制摘要
        /// </summary>
        private static string HmacSha1Hex(string key, string text)
        {
            using (HMACSHA1 hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key)))
            {
                return BytesToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        /// <summary>
        /// 将字节数组转为小写十六进制
        /// </summary>
        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// 解析排名字段数值，缺失或无法解析时返回负无穷以便降序排到末尾
        /// </summary>
        private static double GetRankScore(Dictionary<string, string> data, string rankKey)
        {
            if (data == null || !data.TryGetValue(rankKey, out string raw) || string.IsNullOrEmpty(raw))
            {
                return double.NegativeInfinity;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double score))
            {
                return score;
            }

            return double.NegativeInfinity;
        }

        /// <summary>
        /// 校验游戏标识并获取对应密钥配置
        /// </summary>
        private bool TryGetGameSecrets(string gameId, out GameSecrets secrets)
        {
            if (string.IsNullOrEmpty(gameId) || gameId != Secrets.GameId)
            {
                Debug.LogError($"游戏标识不匹配: gameId={gameId}, expected={Secrets.GameId}");
                secrets = null;

                return false;
            }

            secrets = Secrets;

            return true;
        }

        /// <summary>
        /// 用用户 ID 换取云存档令牌
        /// </summary>
        private async Task<PlatformLoginResult> GenerateToken(HttpClient client, string userId)
        {
            ApplyBasicAuth(client);

            string requestStr = JsonConvert.SerializeObject(new { userID = userId });
            HttpContent content = new StringContent(requestStr, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("https://p.unity.cn/v1/login/token", content);

            if (!response.IsSuccessStatusCode)
            {
                string errBody = await response.Content.ReadAsStringAsync();
                Debug.LogError($"GenerateToken 失败 status={(int)response.StatusCode}, body={errBody}");

                return null;
            }

            string body = await response.Content.ReadAsStringAsync();
            GenerateTokenResponse tokenResult = JsonConvert.DeserializeObject<GenerateTokenResponse>(body);

            if (tokenResult == null || string.IsNullOrEmpty(tokenResult.accessToken))
            {
                Debug.LogError("GenerateToken 返回数据无效");

                return null;
            }

            return new PlatformLoginResult
            {
                accessToken = tokenResult.accessToken,
                expiresAt = tokenResult.expiresAt,
                userID = userId
            };
        }

        /// <summary>
        /// 为 HttpClient 设置 UOS Basic 认证头
        /// </summary>
        private void ApplyBasicAuth(HttpClient client)
        {
            string appId = Environment.GetEnvironmentVariable("UOS_APP_ID");
            string appServiceSecret = Environment.GetEnvironmentVariable("UOS_APP_SERVICE_SECRET");
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{appId}:{appServiceSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }
}