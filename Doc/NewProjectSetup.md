# 新项目准备文档

---

## 1. 首次打开与环境恢复

1. 用与源工程**相同版本**的团结引擎打开新工程目录。源工程版本见 `ProjectSettings/ProjectVersion.txt`（`2022.3.62t11` / Tuanjie `1.9.3`）。版本不一致可能导致 HybridCLR、小游戏转换器、UOS 包行为差异。
2. 首次打开会重建 `Library/`，耗时较长，等待完成。
3. Package Manager 会按 `Packages/manifest.json` 拉取依赖（含 UOS CDN / CloudSave / Func Stateless / Launcher、YooAsset、HybridCLR、微信小游戏 SDK、抖音 SDK、NuGetForUnity 等）。若有 git 包拉取失败，检查网络与凭据后重试。
4. 使用 NuGetForUnity 还原 `Newtonsoft.Json`（与云函数、云存档 JSON 序列化相关）。
5. 打开 Console，确认**无编译错误**后再进入后续步骤。若出现 `UOSSettings` 相关加载异常，先完成第 5 章「UOS Launcher 重新 Link」，或按编辑器提示使用 `UOS/Launcher/Fix settings by reimport` / `UOS/Launcher/Fix settings by delete`。
6. HybridCLR 包为 `com.code-philosophy.hybridclr#v8.14.1`。换引擎大版本后打开 `HybridCLR/Installer` 重新安装，再执行 `HybridCLR/Generate/All`。微信 Publishing Settings 中 `Use Slim Format For global-metadata.dat` 必须关闭。
7. 微信转换 SDK 钉在 `com.qq.weixin.minigame` 的指定 git 提交（见 `Packages/manifest.json`）。Package Manager 更新 git 包后确认 lock hash 与 manifest 一致。

---

## 2. 版本控制初始化

工程已有 `.gitignore`。以下文件/目录被忽略，**不会进入 git**，但对运行与打包必需，须另行备份或私密存储：

| 路径 | 用途 |
|---|---|
| `Assets/Resources/UOSSettings.asset`（及 `.meta`） | UOS AppID / AppSecret / AppServiceSecret（加密） |
| `Assets/UOSLauncherEncrypt/` | UOSSettings 加密密钥 |
| `Assets/Settings/`（含 Build Profiles） | 微信/抖音 Profile：AppID、输出路径等（CDN 由打包菜单按当前平台常量自动写入；须与下方 SDK 配置成对修改） |
| `Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` | 微信 SDK 构建配置，须与 WeChat Profile 成对修改 |
| `Assets/Editor/StarkBuilderSetting.asset` | 抖音 SDK 构建配置，须与 DouYin Profile 成对修改 |
| `Assets/WX-WASM-SDK-V2/` | 微信 WASM SDK 本地内容 |
| `Assets/Editor/UnityOnlineServicesData/` | UOS 编辑器数据（含 CDN 设置） |
| `Assets/Editor/UOSEnvironments.asset`（及 `.meta`） | UOS 环境配置 |

---

## 3. UOS 后台

### 3.1 创建 App 并开通服务

1. 创建新 UOS App，名称建议与 `{新游戏显示名}` 一致。
2. 为该 App 开通以下服务：
   - CDN
   - 云存档（Cloud Save）
   - 云函数（Func Stateless）

### 3.2 CDN Bucket

1. 新建 Bucket。
2. 创建 Badge（建议沿用 `latest`，可改为 `{badge}`）。
3. 复制 client_api 根地址，格式：

```text
https://a.unity.cn/client_api/v1/buckets/{bucketUuid}/release_by_badge/{badge}/content
```

将该地址记为 `{新CDN根地址}`，后续分别写入 `InvariableConst.CDNPathWeChat` 与 `InvariableConst.CDNPathDouYin`（完整 URL，通常为 `{新CDN根地址}/WeChat` 与 `{新CDN根地址}/DouYin`），打包时自动同步平台配置。

### 3.3 记录三密钥

在 App 设置页记录（仅私密保存）：

| 占位符 | 说明 |
|---|---|
| `{UOS_AppID}` | App ID |
| `{UOS_AppSecret}` | App Secret |
| `{UOS_AppServiceSecret}` | App Service Secret |

云函数服务端会通过环境变量 `UOS_APP_ID` / `UOS_APP_SERVICE_SECRET` 使用服务密钥，无需写进客户端业务代码。

---

## 4. 平台后台（微信 + 抖音）

### 4.1 注册小游戏

| 平台 | 操作 | 记录值 |
|---|---|---|
| 微信公众平台 | 注册新小游戏 | `{微信AppID}`、`{微信AppSecret}` |
| 抖音开放平台 | 注册新小游戏 | `{抖音AppID}`、`{抖音AppSecret}` |

### 4.2 服务器域名配置

在微信/抖音小游戏后台的服务器域名（或等价配置页）中，按平台分别添加。格式以平台后台输入框规则为准（微信后台通常带 `https://`，抖音后台通常不带）。

**微信 request / uploadFile / downloadFile 合法域名** — 三类全部添加：

```text
https://a.unity.cn
https://a.unity3dcloud.cn
https://a2.unity3dcloud.cn
https://a3.unity3dcloud.cn
https://metrics2.unity.cn
https://p.unity.cn
https://save.unity.cn
https://stateless.unity.cn
https://thirdwx.qlogo.cn
https://uos-save-bluecloud-1301389817.cos.ap-shanghai.myqcloud.com
https://wx.qlogo.cn
```

**抖音 request / uploadFile / downloadFile 合法域名** — 三类全部添加：

```text
a.unity.cn
a.unity3dcloud.cn
a2.unity3dcloud.cn
a3.unity3dcloud.cn
p.unity.cn
save.unity.cn
stateless.unity.cn
uos-save-bluecloud-1301389817.cos.ap-shanghai.myqcloud.com
douyinpic.com
metrics2.unity.cn
```

**DNS 预解析域名、预连接域名** — 只添加 4 个（通常不带 `https://`，以平台后台输入框规则为准）：

```text
a.unity.cn
a.unity3dcloud.cn
a2.unity3dcloud.cn
a3.unity3dcloud.cn
```

以上域名以 UOS 官方文档/控制台提示为准。若后续接入其他 UOS 服务（例如额外云函数入口、数据上报域名），再按 UOS 官方文档补充。

---

## 5. 工程内配置修改清单

按下列顺序修改。改完后等待脚本编译成功。

### 5.1 选定游戏标识

| 占位符 | 要求 |
|---|---|
| `{新GameId}` | 每个游戏唯一的英文字符串（建议与项目/产品英文名一致）。将用于云存档 namespace：`kv_{新GameId}_player` / `kv_{新GameId}_rank_{平台}` |

**三处必须相同：**

1. `CloudHelper.Secrets.GameId`
2. `CloudManager.CloudSaveGameId`
3. 由此派生的 namespace `kv_{新GameId}_player` 与 `kv_{新GameId}_rank_{平台}`（`wx` / `dy`）

### 5.2 UOS Launcher 重新绑定

1. 菜单：`UOS/Open Launcher`
2. 使用 `{UOS_AppID}` / `{UOS_AppSecret}` / `{UOS_AppServiceSecret}` 重新 Link 新 App
3. 结果写入 `Assets/Resources/UOSSettings.asset`（加密字段）
4. **禁止**手改 `UOSSettings.asset` 中的 `encrypted*` 字段；`Assets/UOSLauncherEncrypt/` 已随工程复制，一般无需改动

### 5.3 CDN 根地址

文件：`Assets/Scripts/Invariable/Utils/InvariableConst.cs`（`#region 游戏资源`）

将两字段改为对应平台完整 URL：

```csharp
public const string CDNPathDouYin = "{新CDN根地址}/DouYin";
public const string CDNPathWeChat = "{新CDN根地址}/WeChat";
```

运行时 YooAsset 远程根为 `SdkManager.Instance.GetCDNPath() + "/yoo"`。留空后果、打包写入与发布边界见 HotUpdateBuildAdapt §4。

### 5.4 DLL/配置加密密钥

文件：`Assets/Scripts/Invariable/Utils/InvariableConst.cs`（`#region 游戏资源`）

`ConfigUtils` 用这两项做 AES 加解密（`DllTool` 复制 `.dll.bin` 与运行时 `ReadSafeFile` 共用）：

```csharp
public const string EncryptKey = "{新EncryptKey}";
public const string EncryptIv = "{新EncryptIv}";
```

要求（按 UTF-8 字节计）：

- `EncryptKey`：16 / 24 / 32 字节
- `EncryptIv`：16 字节

留空时 `Encoding.UTF8.GetBytes("")` 得到 0 长度密钥，AES 直接抛异常，**DLL 加密复制与真机 DLL 解密都会失败**。

> `Invariable` 不可热更，以上改动只能随**基础包**生效。改密钥后必须重跑 DLL 导出/复制再打基础包。

### 5.5 云函数 Secrets 与云存档 GameId

**文件 A：** `Assets/Scripts/CloudService/CloudHelper.cs`

将 `Secrets` 改为：

```csharp
private static readonly GameSecrets Secrets = new GameSecrets
{
    GameId = "{新GameId}",
    WechatAppId = "{微信AppID}",
    WechatAppSecret = "{微信AppSecret}",
    DouyinAppId = "{抖音AppID}",
    DouyinAppSecret = "{抖音AppSecret}"
};
```

**文件 B：** `Assets/Scripts/Invariable/Manager/CloudManager.cs`

```csharp
private const string CloudSaveGameId = "{新GameId}"; // 必须与 CloudHelper.Secrets.GameId 一致
```

`CloudSaveNamespace` 会自动变为 `kv_{新GameId}_player`，排行榜快照为 `kv_{新GameId}_rank_{平台}`（`wx` / `dy` 分榜），无需单独改字符串字面量。

后台显示名（仅展示，不参与定位）：

| 存档 | 显示名 |
|---|---|
| 玩家存档 | 微信玩家数据 / 抖音玩家数据 |
| 排行榜快照 | 微信世界排行榜 / 微信每日排行榜 / 抖音世界排行榜 / 抖音每日排行榜 |

玩家资料字段统一为 `UserId` / `NickName` / `AvatarUrl`：玩家数据云存档直接写在 JSON 内容里；排行榜条目写在顶层，与 `UserId`、`Data` 并列（`Data` 只保留排行分数等业务数据）。世界榜快照 `userId` 为 `rank_world`，日榜为 `rank_day`。

微信需配置隐私协议。抖音需在开放平台开通 `scope.userInfo`。头像 URL 的实际域名必须加入对应 MP 后台 downloadFile 合法域名，不进 UOS 白名单。

> `CloudService` 程序集**不可热更**，以上改动只能随**基础包**生效。`CloudHelper.Secrets` 含平台 AppSecret 且随 `Assets/Scripts` 进入版本控制，注意仓库可见性。

### 5.6 广告位与分享文案

文件：`Assets/Scripts/Invariable/Utils/InvariableConst.cs`（`#region 游戏配置`）

```csharp
public const string RewardedVideoAdUnitId = "{激励视频广告位ID}";
public const string ShareGameTitle = "{新游戏显示名}";
```

> `Invariable` 不可热更，以上改动只能随**基础包**生效。

### 5.7 产品名称

在 Player Settings 或 `ProjectSettings/ProjectSettings.asset` 中：

- `productName` → `{新游戏显示名}`（源工程默认多为 `MiniGame`）
- `companyName` 按需保留或改为 `{公司名}`

### 5.8 Build Profile（微信 + 抖音）

路径：

```text
Assets/Settings/Build Profiles/WeChat Profile.asset
Assets/Settings/Build Profiles/DouYin Profile.asset
```

在团结引擎 Build Profile / 对应 SDK 设置面板中检查并修改：

| 项 | 新值 |
|---|---|
| 小游戏 AppID | `{微信AppID}` / `{抖音AppID}` |
| CDN / 相关远程地址 | 与 `{新CDN根地址}/WeChat`、`{新CDN根地址}/DouYin` 策略一致（核对即可，打包菜单按平台常量自动写入，无需手填） |
| 构建输出路径 | 指向本机新工程下的路径（如 `{新工程根}/Build/WeChat`、`{新工程根}/Build/DouYin`） |
| 屏幕方向、内存、压缩等 | 按新游戏需求核对 |

**重要：** Profile 含机器相关绝对路径。整目录复制后必须重新检查，不能沿用源机器路径。

切子平台与启用 Profile 缺一不可：先切 minigame 子平台，域重载完成后再启用对应 Build Profile；顺序不可颠倒。手动操作一律在团结引擎 Build Profile 窗口勾选目标 Profile。不要只改宏文本。

平台配置必须成对修改：

- 微信：`Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` ↔ `Assets/Settings/Build Profiles/WeChat Profile.asset`
- 抖音：`Assets/Editor/StarkBuilderSetting.asset` ↔ `Assets/Settings/Build Profiles/DouYin Profile.asset`

打包菜单只自动双写 `CDN` 字段，手动改 AppID、输出路径、方向、内存等其它字段时必须人工同步两侧。

### 5.9 编辑器 CDN 上传目标

打开 UOS CDN 相关面板（`UOS/CDN/Manager` 或工程内已有 CDN 工具），将上传目标切换为**新 App 下的新 Bucket**，再执行后续「复制到 CDN / 上传」步骤，避免把资源传到其他游戏 Bucket。

### 5.10 明确无需改动的项

- `YooAssetManager.Instance.PreLoadDll` 内部平台目录名固定为 `MiniGame`（团结引擎 **BuildTarget 名**，与 `EditorUserBuildSettings.activeBuildTarget.ToString()` 对应），**不是**产品名 / GameId。同引擎版本下保持不变。
- YooAsset Package 名默认 `MyPackage`，可沿用，除非团队另有规范。
- `HotUpdate.asmdef` 引用 `Invariable` 与 `CloudService`（消费 Model DTO），统一写名称，**勿删**。项目内程序集之间用名称引用，第三方包用 GUID。

---

## 6. 游戏内容替换

按新游戏需求替换内容；与配置相关的步骤建议紧接在第 5 章之后完成。

### 6.1 Excel 配置表

1. 替换或新增 `Excel/` 下表格（.xlsx/.xls）
2. 菜单：`VastStarryRiver/Config/导出Excel配置`
3. 可选：`VastStarryRiver/Config/校验配置数据`
4. 等待生成代码编译成功（`Assets/Scripts/HotUpdate/Config/Generated/`、`Assets/GameAssets/Config/*.bytes`）

说明：

- 纯数值改动：导表后构建 AssetBundle 即可热更
- 表结构变更：导表并编译成功后，走完整 HybridCLR + YooAsset 流水线

### 6.2 动态资源

- 替换 `Assets/GameAssets` 下预制体、图集、音频等
- 需要合并多图为 Multiple Sprite PNG 时使用工程内 AtlasBuilder（`Assets/Editor/MyTools/AtlasBuilder/`，ContextMenu `BuildAtlas`；输出在 Editor 目录，与 YooAsset 收集的 `GameAssets/Atlas` UI 图集无关）
- 新增音频、图片或图集资源后，执行 `VastStarryRiver/资源处理` 对应菜单批量设置导入参数
- TMP 表情由 `Assets/ToolPackage/TextMesh Pro/Resources/Sprite Assets/emoji.asset` 提供（TMP Settings 默认表情图集），与 AtlasBuilder 无关

### 6.3 首包资源与启动内容

以下改动通常需要重新打**基础包**：

| 路径 | 说明 |
|---|---|
| `Assets/Resources/LocalAssets/Png/loading.png` | 加载图 |
| `Assets/Resources/LocalAssets/Png/age8+.png` | 适龄提示图 |
| `Assets/Resources/LocalAssets/HotUpdatePanel.prefab` | 热更/加载界面 |
| `Assets/Scenes/Start.scene` | 启动场景（按需） |
| 游戏内 UI 标题、产品名文案 | HotUpdate UI Prefab / 配置文本 |

---

## 7. 云函数部署

前置：第 5.5 节 Secrets 与 GameId 已填正确，且工程已编译通过。

1. 菜单：`UOS/Func Stateless/Open Panel`
2. 上传 `CloudHelper` 所在云函数工程（`Assets/Scripts/CloudService/`）
3. **切换为远程调用模式**
4. UOS 控制台给 `ResetDayRank` 配置定时触发器 cron `0 5 * * *`（需正式用户；控制台 cron 时区为 UTC+8（北京时间），每天凌晨 5 点触发）
5. 打包微信/抖音小游戏前确认处于远程模式；**禁止**以本地调用模式出正式包

提醒：

- 远程模式下客户端只保留带 `[CloudFunc]` 的方法；密钥仅在服务端执行
- 打包前置校验只在本地源码仍含 `Secrets` 赋值时扫字段；远程桩会剥掉密钥，不把缺失赋值当成未填。上传前须在本地模式确认已填齐
- 每次改 `CloudService` / 云函数体后：重新上传 → 确认远程模式 → 再出基础包

---

## 8. 首发构建流水线

新游戏首次或完整基础包发布的步骤顺序与 [HotUpdateBuildAdapt.md](./HotUpdateBuildAdapt.md) §12.1 一致，按该节执行。首发额外确认：

- `CDNPathWeChat` / `CDNPathDouYin` 已是对应平台完整 URL
- 云函数已上传且为远程调用模式，`ResetDayRank` 定时触发器已配置（第 7 章）
- UOS CDN 上传目标为新 Bucket，Badge 与 `{新CDN根地址}` 一致
- 用对应平台开发者工具打开构建产物，清缓存与保留缓存各测一轮

后续仅业务代码热更 / 仅资源热更，见 HotUpdateBuildAdapt §12.2 / §12.3。不能只靠热更、必须重发基础包的范围见 HotUpdateBuildAdapt §13。

---

## 9. 验证清单

### 9.1 编辑器侧（准备阶段自检）

- [ ] `CloudHelper.Secrets.GameId` 与 `CloudManager.CloudSaveGameId` 均为 `{新GameId}`
- [ ] 工程内无其他游戏 CDN bucket UUID、无其他 GameId 残留（搜索残留值）
- [ ] `CDNPathWeChat` / `CDNPathDouYin` 指向对应平台完整 URL（不可留空）
- [ ] `InvariableConst.EncryptKey` / `EncryptIv` 已填合法长度（key 16/24/32 字节、iv 16 字节）
- [ ] UOS Launcher 显示已绑定新 App
- [ ] Func Stateless 面板：云函数已上传且为远程模式
- [ ] UOS 控制台已给 `ResetDayRank` 配置日榜定时触发器 cron `0 5 * * *`（需正式用户，cron 时区 UTC+8）
- [ ] 微信隐私协议已配置（含 `scope.userInfo`）；抖音开放平台已开通 `scope.userInfo`
- [ ] 头像实际域名已加入微信/抖音 MP 后台 downloadFile 合法域名
- [ ] 微信/抖音 Build Profile 与对应 SDK 配置（`MiniGameConfig.asset` / `StarkBuilderSetting.asset`）的 AppID、输出路径已成对改为新值
- [ ] Console 无编译错误

### 9.2 真机 / 平台侧（双平台均测）

| 场景 | 微信真机 | 抖音真机 |
|---|:---:|:---:|
| 首次无缓存启动 | 必测 | 必测 |
| 有缓存启动 | 必测 | 必测 |
| 平台登录 → 云函数换取云存档令牌 | 必测 | 必测 |
| 云存档读写（`kv_{新GameId}_player`） | 必测 | 必测 |
| 世界榜/日榜上报/拉取（`kv_{新GameId}_rank_wx` / `kv_{新GameId}_rank_dy`，分榜不混） | 必测 | 必测 |
| CDN 热更资源下载（`{CDN}/{WeChat|DouYin}/yoo`） | 必测 | 必测 |
| DLL 加载与主界面 | 必测 | 必测 |
| 本地存档 | 必测 | 必测 |
| 分享 / 激励视频等平台能力（若启用） | 配置后必测 | 配置后必测 |
| CDN 异常 / 断网提示 | 必测 | 必测 |

完整矩阵另见 HotUpdateBuildAdapt §14。

---

## 附录 A：占位符汇总

| 占位符 | 获取来源 |
|---|---|
| `{新GameId}` | 团队自定，唯一英文标识 |
| `{新EncryptKey}` / `{新EncryptIv}` | 团队自定 AES 密钥（key 16/24/32 字节、iv 16 字节，按 UTF-8 计） |
| `{新游戏显示名}` | 产品命名 |
| `{公司名}` | 可选 |
| `{UOS_AppID}` / `{UOS_AppSecret}` / `{UOS_AppServiceSecret}` | UOS 控制台新 App 设置 |
| `{bucketUuid}` / `{badge}` / `{新CDN根地址}` | UOS CDN Bucket |
| `{微信AppID}` / `{微信AppSecret}` | 微信公众平台 |
| `{抖音AppID}` / `{抖音AppSecret}` | 抖音开放平台 |
| `{新工程根}` | 复制后的本地路径 |

## 附录 B：关键菜单速查

打包微信/抖音小游戏菜单会先跑前置校验。远程调用模式须由用户在 UOS / Func Stateless 面板自行确认，工程内无法验证。

### Config

- `VastStarryRiver/Config/导出Excel配置`（产物：`GameAssets/Config/*.bytes` + `HotUpdate/Config/Generated/Config_*.cs` + `ConfigManager.Preload.cs`）
- `VastStarryRiver/Config/校验配置数据`（Excel 与 bytes 全量比对）

### DLL

- `VastStarryRiver/DLL/导出所有DLL`
- `VastStarryRiver/DLL/复制热更新DLL`
- `VastStarryRiver/DLL/复制元数据DLL`

### 资源处理

- `VastStarryRiver/资源处理/设置音频资源`（按 `Audios/Bgm` 与 `Audios/Sfx` 分别设置 CompressedInMemory / DecompressOnLoad，Sfx 强制 Force To Mono，Bgm 不改）
- `VastStarryRiver/资源处理/设置图片和图集`（Atlas 图集压缩/关可读；Atlas 源图与 Png 散图统一最佳模式：强制 Sprite、关可读、关 mipmap、压缩）

### 构建

- `VastStarryRiver/构建AssetBundle`

### 打包

- `VastStarryRiver/打包/复制bundle到CDN目录`
- `VastStarryRiver/打包/打包微信小游戏`
- `VastStarryRiver/打包/打包抖音小游戏`
- `VastStarryRiver/打包/复制unityweb.bin到CDN目录`

### UOS

- `UOS/Func Stateless/Open Panel`（上传云函数；发布前需切远程调用，见 NewProjectSetup §7）
- `UOS/Open Launcher`（关联 UOS App / 凭证）
- `UOS/CDN/Manager`（切换 CDN 上传目标 Bucket）

切平台见 HotUpdateBuildAdapt §1（先切子平台、等域重载、再启用 Profile）；成对修改见本章 §5.8 与 HotUpdateBuildAdapt §9 / §10。不要只改宏文本。

## 附录 C：准备进度勾选（可选）

- [ ] 1. 首次打开与环境恢复
- [ ] 2. 版本控制初始化与忽略文件备份策略
- [ ] 3. UOS 新 App / 三服务 / Bucket / 三密钥
- [ ] 4. 微信+抖音注册与域名配置
- [ ] 5. 工程内配置（UOS Link、CDNPathWeChat/CDNPathDouYin、EncryptKey/EncryptIv、GameId、Secrets、广告/分享常量、productName、Profile、CDN 目标）
- [ ] 6. 游戏内容替换与导表
- [ ] 7. 云函数上传并远程模式，ResetDayRank 定时触发器已配置
- [ ] 8. 首发构建流水线跑通
- [ ] 9. 双平台验证清单通过
