# 框架架构与运行流程

## 1. 技术栈

| 类别 | 技术/版本 | 用途 | 依赖形态 |
|---|---|---|---|
| 引擎 | 团结引擎 1.9.3 / Unity 2022.3.62t11 | 游戏运行与小游戏构建 | 引擎本体 |
| 热更新 | HybridCLR 8.14.1 | 运行时加载 `HotUpdate.dll` | UPM 包（`#v8.14.1`）；生成物在 `Assets/HybridCLRGenerate/`；本地 il2cpp 为 `v2022-tuanjie-8.14.0` |
| 资源系统 | YooAsset 2.3.19 | Bundle、清单、下载、缓存和异步资源加载 | UPM 包；小游戏文件系统在 `Assets/ToolPackage/YooAsset` |
| 微信平台 | `com.qq.weixin.minigame`（git 提交钉死）+ `Assets/WX-WASM-SDK-V2` + `cn.tuanjie.wx-uploader` | 小游戏转换、运行时 API 与上传 | UPM 转换工具 + Assets 内运行时 SDK + 上传器 |
| 抖音平台 | StarkSDK 6.9.0 | 抖音小游戏构建与运行时 API | `LocalPackages/com.bytedance.starksdk@6.9.0` 本地包 |
| 异步 | UniTask 2.5.10 | 异步方法（`CloudManager` 云存档链路 async/await） | `Assets/ToolPackage/UniTask` 本地源码 |
| UI | UGUI + TextMeshPro | 页面和文本 | TextMeshPro 在 `Assets/ToolPackage/TextMesh Pro` |
| 动画 | DOTween | UI 补间 | `Assets/ToolPackage/DOTween` 预编译 `DOTween.dll` + `Modules/` 源码模块（`DOTweenModule.asmdef`） |
| 配置 | ExcelDataReader + 自定义生成器 | Excel 转 bytes 与生成代码 | `Assets/Plugins/ExcelDataReader.dll` 预编译库 |
| JSON | Newtonsoft.Json | 云存档序列化等 | NuGetForUnity；亦为 AOT 元数据 DLL 之一 |
| 其他 | Spine、UIParticle、UOS CDN | 动画、UI 粒子和 CDN | UPM |
| UOS 服务 | UOS Launcher / CloudSave / Func Stateless | 云存档与云函数 | UPM；另有 `Assets/UOSLauncherEncrypt`（Launcher 自带加密模块，勿改） |
| 开发环境 | Unity MCP + Cursor IDE 集成 | 编辑器集成 | UPM git 包（`com.coplaydev.unity-mcp`、`com.boxqkrtm.ide.cursor`），仅编辑器 |

## 2. 程序集架构

项目内程序集（`Invariable` / `CloudService` / `MyTools` / `HotUpdate`）之间的引用统一写名称；第三方包用 GUID。

### 2.1 `Invariable`

位置：`Assets/Scripts/Invariable/Invariable.asmdef`

职责：

- 首场景入口与资源更新状态机；
- YooAsset、HybridCLR 和平台 SDK 初始化；
- 游戏全局事件、计时器、音频、UI 注册表；
- UI 基础控件和通用工具；
- 微信/抖音统一接口（含平台登录）；
- 云存档读写入口（`SetCloudData` / `GetCloudData`）；
- 云存档初始化与排行榜拉取（`CloudManager`）；
- 配置表运行时底座（`ConfigManagerCore` / `ConfigReader` / `BinReader` / `DictionaryForConfig`，首包不随导表变化）。

特点：

- `autoReferenced: false`；
- 运行于热更新 DLL 加载之前；
- 修改后不能只替换 `HotUpdate.dll`，通常需要重新构建并发布小游戏基础包；
- 不直接引用 `HotUpdate`，通过字符串和反射启动热更新层；
- 引用 `CloudService`（名称引用），通过 Func Stateless 远程代理调用云函数。

主要命名空间：`Invariable`。

### 2.2 `HotUpdate`

位置：`Assets/Scripts/HotUpdate/HotUpdate.asmdef`

职责：

- 业务入口；
- 业务 UI；
- 可更新的玩法逻辑；
- Excel 生成配置。

特点：

- 被 HybridCLR 标记为热更新程序集；
- 可以引用 `Invariable` 的公共能力（名称引用）；
- 引用 `CloudService`（名称引用，消费 Model DTO 如 `PlayerCloudData`、`CloudDataKeys`）；
- 发布时作为加密 `.dll.bin` 放入 YooAsset 资源；
- 入口必须保持为 `HotUpdate.StartGame.Play()`，除非同时修改反射入口。

主要命名空间：`HotUpdate`。

### 2.3 `CloudService`

位置：`Assets/Scripts/CloudService/`

职责：

- UOS Func Stateless 云函数（平台登录换取云存档令牌、排行榜快照读写等）；
- 客户端经 SDK 远程代理调用，不在客户端执行函数体中的密钥逻辑；
- 云存档相关数据模型（一类一文件，放在 `CloudService/Model/`）。

特点：

- 独立程序集，被 `Invariable` 与 `HotUpdate` 引用；
- `autoReferenced: false`；
- 修改后需重新构建并发布小游戏基础包，不能只热更；
- Model DTO 契约变更 = 重发基础包 + 必须同步重导热更 DLL（`HotUpdate` 编译期绑定 DTO）；
- 所有游戏可共享同一 UOS App；每个游戏工程在 `CloudHelper.Secrets` 中只配置本游戏密钥；
- 云函数类与 `Model/` 数据类同属 `Assets/Scripts/CloudService/` 目录树，满足 Func Stateless 同目录打包约束。

主要命名空间：`CloudService`。

### 2.4 `MyTools`

位置：`Assets/Editor/MyTools/MyTools.asmdef`

职责：

- Excel 导出；
- HybridCLR DLL 生成与复制；
- YooAsset Bundle 构建；
- 微信/抖音小游戏构建；
- 音频/图集导入设置（`AssetProcess`）；
- 图集和 `.bin` 导入；
- UIButton 自定义 Inspector（`InspectorEditor/UIButtonEditor.cs`）。

仅包含 Editor 平台，不进入运行时。引用 `Invariable` 用名称引用。

### 2.5 `YooAsset.MiniGame`

位置：`Assets/ToolPackage/YooAsset/YooAsset.MiniGame.asmdef`

职责：

- 微信小游戏 YooAsset 文件系统；
- 抖音小游戏 YooAsset 文件系统；
- 对接各平台缓存、文件读取、下载和 AssetBundle 加载 API。

## 3. 启动场景结构

构建场景列表只有：

```text
Assets/Scenes/Start.scene
```

场景提供：

- `UI_Root`；
- `Canvas_0` 到 `Canvas_3`；
- 每层的 `UI_Camera`；
- 每层的 `Ts_Panel` 页面挂载点；
- `EventSystem`；
- `Launcher` 启动组件。

UI 约定：

```text
UI_Root/
├─ Canvas_0/
│  ├─ UI_Camera
│  └─ Ts_Panel
├─ Canvas_1/
│  ├─ UI_Camera
│  └─ Ts_Panel
├─ Canvas_2/
│  ├─ UI_Camera
│  └─ Ts_Panel
└─ Canvas_3/
   ├─ UI_Camera
   └─ Ts_Panel
```

代码通过 `Utils.UICamera` / `Utils.UIRoot` 等属性访问这些固定路径，重命名节点时必须同步检查：

- `Launcher.cs`
- `Utils.cs`
- `UIDrag.cs`
- `Rocker.cs`

约定层级：

| layer 参数 | 典型用途 | 示例 |
|---:|---|---|
| 0 | 主界面/普通页面 | `MainPanel` |
| 1 | 较高普通界面 | 无示例 |
| 2 | 对话框/弹窗 | `TipsPanel` |
| 3 | 顶层提示 | `FloatTextPanel` |

## 4. 完整启动流程

### 4.1 `Launcher.Awake`

文件：`Assets/Scripts/Invariable/Workflow/Launcher.cs`

行为：

1. 设置 `Application.targetFrameRate = 60` 与 `Screen.sleepTimeout = NeverSleep`；
2. 编辑器使用 `EPlayMode.EditorSimulateMode`；
3. 非编辑器使用 `EPlayMode.WebPlayMode`；
4. `InitPoolParent`：查找或创建名为 `PoolParent` 的常驻节点（`InvariableConst.PoolParentName`），`DontDestroyOnLoad` 后调用 `PoolUtils.SetPoolParent` 注入为对象池根节点；
5. 创建常驻 `GameManager`；
6. 创建常驻 `AudioManager`，并附加 `AudioListener`。

Manager 创建依赖：

```csharp
Utils.CreateManagerInstance("GameManager");
Utils.CreateManagerInstance("AudioManager", new string[] { "AudioListener" });
```

因此 Manager 类名、GameObject 名和反射查找名存在强约定。

### 4.2 `Launcher.OnEnable`

注册三个字符串事件：

| 事件名 | 参数 | 监听用途 |
|---|---|---|
| `Launcher_ShowTips` | `string` | 显示加载描述 |
| `Launcher_ShowProgress` | `DownloadProgressInfo`（`CurrentBytes` / `TotalBytes`） | 显示下载进度 |
| `Launcher_StartGame` | `object`（传 `null`） | 销毁加载面板和 `Launcher`；由 HotUpdate 层 `MainPanel.Awake` 触发（Invariable 层只订阅不发布） |

### 4.3 `Launcher.Start`

创建状态机并注册：

```text
InitializeYooAsset
CheckCatalogUpdate
CheckResourceUpdates
HotUpdateOver
```

同时：

- 将 `EPlayMode` 写入状态机黑板；
- 对 `UI_Root` 调用 `DontDestroyOnLoad`；
- 查找本地加载面板；
- 若不存在，从 `Resources/LocalAssets/HotUpdatePanel` 实例化；
- 从 `InitializeYooAsset` 开始执行。

### 4.4 `InitializeYooAsset`

文件：`Assets/Scripts/Invariable/Workflow/InitializeYooAsset.cs`  
远程服务实现：`Assets/Scripts/Invariable/Workflow/RemoteServices.cs`

职责：

1. 若 `YooAssets.Initialized` 已为 true，直接跳到 `HotUpdateOver`（跳过清单检查与资源下载）；
2. 否则以 `SdkManager.Instance.GetCDNPath()` 为 CDN 根（小游戏远程根为 `GetCDNPath() + "/yoo"`，见下方「小游戏」分支）；
3. 初始化 YooAsset，并设置 `YooAssets.SetOperationSystemMaxTimeSlice(1000)`；
4. 创建或获取包 `MyPackage`；
5. 设置为默认包；
6. 按模式初始化文件系统。

编辑器：

```text
EditorSimulateModeHelper.SimulateBuild("MyPackage")
```

小游戏：

```text
CDN 根地址 = SdkManager.Instance.GetCDNPath() + "/yoo"
defaultHostServer = fallbackHostServer（主备同址，备线未单独配置）
WebPlayModeParameters
  -> RemoteServices（IRemoteServices 实现，提供主/备 URL 拼接）
  -> SdkManager.InitializeYooAsset 按平台创建微信或抖音自定义文件系统
```

失败时仅提示「资源加载失败，请检查网络后重启游戏」，无重试，状态机停在本节点。

注意：编辑器域重载或二次进入启动流程时，若 YooAsset 已初始化，会走短路进入 `HotUpdateOver`，并再次执行清缓存、`InitSDK`、`InitCloudData` 全套流程；排查「未重新拉清单/未下载」时需先确认此分支。

### 4.5 `CheckCatalogUpdate`

按顺序执行：

1. `RequestPackageVersionAsync(false)` 获取远程包版本；
2. `UpdatePackageManifestAsync(packageVersion)` 更新清单；
3. 成功后进入 `CheckResourceUpdates`。
4. 失败后则无法进入下一步。

### 4.6 `CheckResourceUpdates`

创建下载器：

```csharp
Package.CreateResourceDownloader(
    downloadingMaxNum: 10,
    failedTryAgain: 3
);
```

- 无文件需下载：直接进入 `HotUpdateOver`；
- 有文件：注册下载回调并开始下载；
- 进度通过 `Launcher_ShowProgress` 传递；
- 下载成功进入 `HotUpdateOver`；
- 失败后则无法进入下一步。

### 4.7 `HotUpdateOver`

按顺序执行：

1. 清理未使用清单缓存；
2. 清理未使用 Bundle 缓存；
3. 初始化平台 SDK；
4. 初始化云存档（平台登录 → 云函数换取云存档令牌 → 拉取云端存档）；
5. 加载 AOT 补充元数据；
6. 加载 `HotUpdate.dll`；
7. 反射调用热更新入口。

反射契约：

```csharp
Type type = hotUpdateAss.GetType("HotUpdate.StartGame");
MethodInfo method = type.GetMethod("Play", ...);
method.Invoke(null, null);
```

因此以下任一变化都必须同步修改 `HotUpdateOver.cs`：

- 命名空间；
- 类名；
- 方法名；
- 方法是否为静态；
- 方法参数列表。

### 4.8 `HotUpdate.StartGame.Play`

行为：

```csharp
Utils.OpenUIPrefabPanel("MainPanel", 0);
```

进入前由 `HotUpdateOver` 提示“即将进入游戏...”。`MainPanel.Awake` 触发 `Launcher_StartGame`，销毁加载面板和 Launcher。

## 5. HybridCLR DLL 加载

文件：`Assets/Scripts/Invariable/Manager/YooAssetManager.cs`

### 5.1 编辑器

不加载 DLL 二进制，直接查找已编译程序集：

```csharp
AppDomain.CurrentDomain.GetAssemblies()
    .First(a => a.GetName().Name == "HotUpdate");
```

这意味着编辑器测试成功并不能证明 `.dll.bin`、AOT 元数据或远程 Bundle 正确。

### 5.2 真机小游戏

运行时硬编码资源平台前缀为 `MiniGame`，AOT 程序集列表单一事实源为 `InvariableConst.AotDllNames`（与编辑器 `DllTool` 复制元数据 DLL 共用），并行加载：

```text
MiniGame_mscorlib.dll
MiniGame_System.dll
MiniGame_System.Core.dll
MiniGame_Newtonsoft.Json.dll
```

对每个 DLL 调用：

```csharp
RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
```

全部完成后加载：

```text
MiniGame_HotUpdate.dll
```

再执行：

```csharp
Assembly.Load(bytes)
```

对应资源源文件：

```text
Assets/GameAssets/DLL/MiniGame/
├─ mscorlib.dll.bin
├─ System.dll.bin
├─ System.Core.dll.bin
├─ Newtonsoft.Json.dll.bin
└─ HotUpdate.dll.bin
```

这些文件不是普通 DLL 原文，而是经 `ConfigUtils.SaveSafeFile` 序列化、GZip 压缩并 AES 加密后的 `.bin`。

任一 AOT / HotUpdate DLL 异步加载失败时，`GameLog.Error` 输出具体 DLL 名；失败路径停止后续加载 `HotUpdate.dll`，启动停在该阶段。

## 6. YooAsset 资源架构

包名固定为：

```text
MyPackage
```

资源收集配置：

```text
Assets/AssetBundleCollectorSetting.asset
```

收集组：

| 组 | 目录 | 收集器 | 地址规则 | 打包规则 |
|---|---|---|---|---|
| Animation | `GameAssets/Animation` | Depend | 无独立地址 | PackGroup |
| Atlas | `GameAssets/Atlas/Atlas01`、`Atlas02`、`Atlas03`（逐目录注册，非通配；新图集目录需手动加收集器） | Depend | 无独立地址 | PackCollector |
| Audios | `GameAssets/Audios` | Depend | 无独立地址 | PackGroup |
| Materials | `GameAssets/Materials` | Main | Group + FileName | PackGroup |
| Png | `GameAssets/Png` | Depend | 无独立地址 | PackGroup |
| Prefabs | `GameAssets/Prefabs/UI` | Main | Group + FileName | PackCollector |
| Scenes | `GameAssets/Scenes` | Main | Group + FileName | PackGroup |
| Config | `GameAssets/Config` | Main | Group + FileName | PackGroup |
| DLL | `GameAssets/DLL` | Main | Folder + FileName | PackGroup |

代码中的地址示例：

| 资源 | 地址 |
|---|---|
| 主页面 Prefab | `Prefabs_MainPanel` |
| 提示弹窗 | `Prefabs_TipsPanel` |
| 飘字面板 | `Prefabs_FloatTextPanel` |
| 灰度材质 | `Materials_GrayscaleMaterial` |
| 遮罩灰度材质 | `Materials_UIMaskGrayscaleMaterial` |
| 配置表 bytes | `Config_{表名}`（如 `Config_Player`） |
| 热更新 DLL | `MiniGame_HotUpdate.dll` |

图集为 Depend 模式，仅作 Prefab 依赖进包，无独立地址。业务脚本声明 `public SpriteAtlas` 并在 Inspector 挂载，按名 `GetSprite` 后直接赋值。单张图片声明 `public Sprite` 挂载，直接赋值。动画挂载到 Animation 组件，代码 `animation.Play("animName")`。`VastStarryRiver/资源处理/设置图片和图集` 菜单批量设置导入参数：图集压缩并关可读，Atlas 源图与 Png 散图统一最佳模式（强制 Sprite、关可读、关 mipmap、压缩）。

图集构建工具：

- 位置：`Assets/Editor/MyTools/AtlasBuilder/`（`AtlasBuilder.cs` + `AtlasBuilder.asset`）；
- 用途：通用纹理打包器，将配置目录下的图片合并为一张 Multiple Sprite PNG 并生成 spritesheet；不是 YooAsset UI 图集构建器；
- 无顶部菜单；在 `AtlasBuilder.asset` 的 Inspector 中配置 `m_atlasName`、`m_directorys` 后，通过 ContextMenu `BuildAtlas` 触发；
- 输出到 `Assets/Editor/MyTools/AtlasBuilder/{图集名}/`（Editor 目录），与 `GameAssets/Atlas` 的 YooAsset 收集路径相互独立；
- TMP 表情由 `Assets/ToolPackage/TextMesh Pro/Resources/Sprite Assets/emoji.asset` 提供（TMP Settings 默认表情图集），与该工具无关。

### 6.1 资源句柄

`YooAssetManager` 缓存：

- `Dictionary<string, AssetHandle> m_assetHandles`
- `Dictionary<string, List<Action<object>>> m_pendingCallbacks`（同地址在途去重）
- `Dictionary<string, float> m_lastAccessTimes`（最近访问时间，供闲置逐出）
- `Dictionary<string, SceneHandle> m_sceneHandles`

`AsyncLoadAsset<T>`：

- 已缓存时刷新访问时间并直接回调 `AssetObject`；
- 同地址加载中时追加回调，完成后一并通知；
- 首次异步加载并缓存句柄，成功后刷新访问时间；
- 失败写 `GameLog.Error`，并对全部等待回调传入 `null`。

`AsyncLoadScene`：

- `Additive` 模式直接加载目标场景；
- `Single` 模式先逐个卸载其他已缓存场景，再加载目标场景；加载完成后调用 `PoolUtils.ClearAllGameObjectPools()` 清空 GameObject 池；
- 缓存句柄对应场景已卸载时释放该句柄并重新加载；
- 加载失败释放句柄并回调 `default`。

闲置逐出（与配置表 `ConfigFormat` 节奏对齐）：

- 闲置阈值 `180s`，清扫周期 `30s`（计时器 key `InvariableConst.Timer_YooAsset_TickEvict`）；
- 逐出计时器在首次 `AsyncLoadAsset` 时惰性注册（`EnsureEvictTimer`，注册后不立即执行）；
- 白名单前缀不释放：`Config_`、`MiniGame_`（配置秒回/程序集无释放价值）；
- 其余地址闲置超时后调用 `ReleaseAsset`；已实例化的 Prefab 实例不受句柄释放影响（YooAsset 引用计数）。

资源释放：

- `ReleaseAsset(address)`：按地址释放句柄；
- `UnLoadAsset()`：释放已缓存的全部普通资源；
- `UnloadUnusedAssets(callBack)`：调用 YooAsset 卸载未使用资源；
- `UnLoadScene(address, callBack = null)`：仅释放对应场景句柄，不连带释放普通资源；回调可选，成功失败均触发。

## 7. 全局管理器

### 7.1 MonoBehaviour Manager

| Manager | 创建方式 | 用途 |
|---|---|---|
| `GameManager` | `Launcher.Awake` 创建常驻对象 | 事件、延迟/循环计时（秒/帧双最小堆，`Update` 驱动）；`Awake` 注册配置逐出计时器，`OnDestroy` 取消全部计时器；`OnApplicationPause` / `OnApplicationQuit` / `OnDestroy` 调用 `CloudManager.FlushCloudData` |
| `AudioManager` | `Launcher.Awake` 创建常驻对象 | 仅挂载 clip 播放；BGM 单通道循环，SFX 每 clip 独立通道同 clip 打断重播，通道上限 30，超出回收最久未用空闲通道，全忙打错误日志并跳过；BGM/SFX/全部三套停止暂停恢复；音量/静音本地持久化 |

二者使用私有静态字段 `m_instance`，在 `Awake` 赋值；对外暴露 `Instance`（为空时打 Error）与 `HasInstance`（判空不打日志）。

### 7.2 普通 C# Singleton

| Manager | 用途 |
|---|---|
| `YooAssetManager` | 包、资源、场景、DLL |
| `UIManager` | 已打开页面字典；提供 `CloseUIPanel` / `CloseAllUIPanel`；`TipsPanel` 池化复用 |
| `SdkManager` | 平台 SDK、平台登录、本地/云读写入口、平台用户信息同步（`SyncPlatformUserInfo` / `TryGetPlatformUserInfo`，`authStateCallBack` 上报 `PlatformAuthStates`（`Invariable` 顶层枚举）、`userInfoCallBack` 仅资料结果，授权按钮显隐由业务层负责）、授权锚点（`RequestPlatformUserInfoAuth` / `DestroyPlatformUserInfoButton`，同样双回调）、键盘、广告、分享、适配 |
| `CloudManager` | 云存档初始化、云缓存、世界榜/日榜快照上报（ReportRankScore）与拉取（GetRankList 读 Top100 快照）；私有持有 `CloudHelper`，业务禁止直调；写后防抖上传（2s），`FlushCloudData` 立即上传脏数据 |

基类：

```csharp
Singleton<T> where T : new()
```

它们不是 Unity 组件，没有 `Update`、`OnDestroy` 等生命周期。

### 7.3 静态工具类

`PoolUtils`（`Assets/Scripts/Invariable/Utils/PoolUtils.cs`）为 `public static class`，提供类型池与 GameObject 池，不继承 `Singleton<T>`。

- 类型池：`Get<T>()` / `Release<T>(item)` / `ClearPool<T>()`，每类型上限 `DefaultMaxSize` = 30；`IList` 归还时自动 `Clear`
- GameObject 池：`GetGameObject(key, prefab, parent)` / `ReleaseGameObject(key, instance)`，按 key 隔离，默认单 key 上限 `DefaultGameObjectMaxSize` = 50；`SetGameObjectPoolMaxSize(key, maxSize)` 可按 key 自定义上限（≤0 回落默认）；超限销毁并输出 `GameLog.Info` 提示
- 池根节点：`Launcher.Awake` 调用 `SetPoolParent` 注入 `PoolParent` 常驻节点，归还的 GameObject 统一挂入并隐藏；取出时激活并挂到指定 parent
- 清池：`ClearGameObjectPool(key)` 销毁指定 key 全部实例、`ClearAllGameObjectPools()` 清空全部，均保留自定义上限；`Single` 模式场景加载完成后自动调用后者
- 池 key 使用 `HotUpdateConst` `#region 对象池` 常量，禁止调用处散落字面量
- 示例：`FloatTextPanel`（`HotUpdateConst.Pool_FloatTextItem`）

## 8. 事件系统

实现：`GameManager`

数据结构：

```csharp
Dictionary<string, List<Delegate>>
```

API（仅泛型）：

```csharp
AddEventListener<T>(key, callback);
RemoveEventListener<T>(key, callback);
InvokeEventCallBack<T>(key, arg);
```

无参通知：

```csharp
InvokeEventCallBack<object>(key, null);
```

使用规则：

1. 事件/延迟调用 key 必须使用常量：跨层契约进 `InvariableConst`，HotUpdate 业务进 `HotUpdateConst`（均用 `#region` 分区）；
2. 注册通常放 `OnEnable`，移除放 `OnDisable`；
3. 监听与触发的泛型参数类型必须一致；
4. `InvokeEventCallBack` 使用快照遍历 + 单回调异常隔离，回调内增删监听或抛异常不会打断其他监听者。

## 9. 计时与重复调用

实现：`GameManager` 秒/帧双最小堆，`Update` 驱动。

API：

```csharp
DelayCallFrames(key, callback, frame);
DelayCallSeconds(key, callback, time);
RepeatingCallFrames(key, callback, frame = 1, immediately = true);
RepeatingCallSeconds(key, callback, time = 1f, immediately = true);
CancelInvokeByKey(key);
```

约束：

- `key` 全局唯一；
- 延迟键与循环键任一已存在时，新调用直接返回；
- 调用方销毁或禁用时应主动取消；
- 一次性延迟完成后会移除对应 key；
- 循环计时由 `Update` 驱动最小堆；`immediately` 为 true 时注册后立即执行一次；
- `DelayCallSeconds` 与 `RepeatingCallSeconds` 均受 `Time.timeScale` 影响；
- `CancelInvokeByKey` 仅当 key 存在时输出 `GameLog.Info(key + "取消调用")`，key 不存在直接返回。

使用示例：`FloatTextPanel`。

## 10. UI 框架

### 10.1 页面打开

打开页面应使用：

```csharp
Utils.OpenUIPrefabPanel(string prefabPath, int layer, Action<GameObject> callBack = null);
```

`prefabPath` 传路径或文件名均可，内部取文件名并去掉 `.prefab`。Tips/FloatText 业务封装走 `HotUpdateUtils.OpenTipsPanel` / `ShowFloatText`（内部仍调用 `Utils.OpenUIPrefabPanel`）。

执行过程：

1. 根据文件名得到页面名；
2. 若 `UIManager.AllPanel` 已有该页面则重新激活并回调；
3. 若同名页面正在加载中则忽略本次打开；
4. 加载地址 `Prefabs_{页面名}`；
5. 实例化到 `UI_Root/Canvas_{layer}/Ts_Panel`；
6. 通过类型名查找或动态添加组件（解析顺序：无名空间 → `Invariable.` → `HotUpdate.` → `FindTypeTool` 内置 UGUI 映射表 → 热更 DLL 程序集反射）；
7. 将其作为 `UIPanel` 注册；
8. 调用回调。

### 10.2 页面关闭

所有页面基类：

```csharp
public class UIPanel : MonoBehaviour
```

基类无虚拟生命周期，子面板直接写 Unity 原生生命周期（Awake/OnEnable/Start/OnDisable）。

调用 `Close()`：

- 带 `UIPopup`：先播放关闭动画，再走关闭流程；
- `TipsPanel`（`UIManager` 池化名单）：隐藏复用，不从字典移除；
- 其余页面：从 UIManager 移除并 `Destroy`。

### 10.3 已有页面

| 页面 | 脚本 | 层级 | 用途 |
|---|---|---:|---|
| MainPanel | `HotUpdate.UI/MainPanel/MainPanel.cs` | 0 | 主界面（含云功能测试按钮） |
| TipsPanel | `HotUpdate.UI/Popup/TipsPanel.cs` | 2 | 单/双按钮提示 |
| FloatTextPanel | `HotUpdate.UI/Popup/FloatTextPanel.cs` | 3 | 可复用飘字提示 |

MainPanel 含 4 个测试点击方法（OnTestClick1-4）与授权按钮演示逻辑，属模板演示代码。

辅助接口：

```csharp
HotUpdateUtils.OpenTipsPanel(...);
HotUpdateUtils.ShowFloatText(...);
```

### 10.4 UI 基础组件

| 组件 | 用途 |
|---|---|
| `UIButton` | 单击、双击、按下、抬起、长按、缩放反馈；每类监听覆盖赋值（非追加）；5 个 public UnityEvent 字段（`m_clickEvent` 等）可在 Inspector 绑定监听；`m_isNotChangeScale`（默认 false）、`m_scaleType` 枚举（Small/Medium/Big=1.1/1.2/1.3，默认 Medium，由 `UIButtonEditor` 条件显示）；`m_audioClip` 挂载点击音效，留空且未被手动修改过时 Inspector 打开即自动挂载 `Assets/GameAssets/Audios/Sfx/defaultBtn.mp3`（EditorPrefs 按 GlobalObjectId 记忆手动修改）；双击判定窗口 0.15s；长按阈值 0.2s；注册双击后单击会延迟 0.15s 且可能被双击吞掉；双击/单击分发由全局 `UIButtonDriver` 驱动；长按触发后吞掉本次单击 |
| `UIPanel` | 页面基类 |
| `UIPopup` | 弹窗开关动画；入场动画在 `OnEnable`（每次激活重播并重置缩放）；`m_tsTrans` 所在物体须同时挂 `CanvasGroup`；DOTween 使用 `SetTarget` / `DOKill` |
| `LoopScrollList` | 横向/纵向循环列表；列表项缓存索引 |
| `LoopScrollItem` | 循环列表项，缓存索引，配合 `LoopScrollList` |
| `MiniInputField` | 调起小游戏原生键盘 |
| `ScreenAdapter` | `[ExecuteInEditMode]`；注册安全区适配；实际偏移由 `SdkManager.GetSafeAnchor` 写死为 Left/Bottom=30/130、Right/Top=30/90，非设备 SafeArea；编辑期 `OnEnable` 即改节点偏移 |
| `BgAdapter` | `[ExecuteInEditMode]`；背景等比铺满；编辑期同样生效 |
| `UIDrag` | UI 拖拽及 ScrollRect 事件转发；拖拽回调阶段 1=开始/2=拖拽中/3=结束 |
| `Rocker` | 虚拟摇杆（`SetMoveFunc(Action<Vector2>)` 输出方向归一化 × 力度 0~1，`SetStayFunc(Action)` 静止回调；按下时摇杆整体移至触点，手柄跟随并松开回中；无输入时自动隐藏，再次按下重新出现） |
| `CircleImage` | 圆形 Sprite UI 网格 |
| `CircleRawImage` | 圆形 RawImage UI 网格 |
| `PolygonImage` | 基于 PolygonCollider2D 的非矩形射线检测 |

## 11. 音频

`AudioManager`：BGM 单通道循环（子物体 `BGM`）；SFX 每 clip 独立 `AudioSource`，同 clip 打断重播，不同 clip 叠加；通道上限 30，超出回收最久未用空闲通道，全忙打错误日志并跳过。音频一律 Inspector 挂载 `AudioClip`，禁止按名加载，为 Depend 模式、由 Prefab 依赖加载。音量/静音经 `SdkManager` 本地存储持久化。播放、停止与导入设置见 ScriptDevGuide §10。

## 12. 配置系统

配置源：

```text
Excel/*（.xlsx/.xls）
```

`.xlsx/.xls` 只读第一个 sheet。

目录：

```text
Assets/Scripts/Invariable/Config/          # 运行时底座（首包）
Assets/Scripts/HotUpdate/Config/           # ConfigManager 转发层
Assets/Scripts/HotUpdate/Config/Generated/ # Config_*.cs + ConfigManager.Preload.cs
```

菜单：

```text
VastStarryRiver/Config/导出Excel配置
VastStarryRiver/Config/校验配置数据
```

「校验配置数据」回读 `GameAssets/Config/*.bytes` 与 Excel 源表逐字段比对（float 容差 `1e-4`），结果以 `[OK]` / `[ERROR]` / `[MISSING]` 输出到 Console；不依赖 HotUpdate 程序集。

导表产物：

- `Assets/GameAssets/Config/{Table}.bytes`（YooAsset Config 组，可热更；单文件含 magic(CFGT)+schemaHash+count+ids+rowSize+数据区+字符串区；运行时以 `TextAsset` 加载，地址 `Config_{表名}`；加载时校验 schemaHash，不匹配即报错需重新导表）
- `Assets/Scripts/HotUpdate/Config/Generated/Config_{Table}.cs`（行类型 + `ConfigManager` partial API，含 `SchemaHash`）
- `Assets/Scripts/HotUpdate/Config/Generated/ConfigManager.Preload.cs`（`PreloadAll` / `ClearAll`）

运行时 API（回调式）。每表由生成代码提供 `Get{表}ByID` / `Get{表}ByIDs` / `Get{表}` / `GetAll{表}` / `Clear{表}`；`PreloadAll` / `ClearAll` 由 `ConfigManager.Preload.cs` 提供：

```csharp
ConfigManager.GetPlayerByID(1, row => { ... }); // Config_Player
ConfigManager.GetPlayerByIDs(new[] { 1, 2 }, list => { ... });
ConfigManager.GetRoleRune(dic => { ... });
ConfigManager.GetAllRoleRune(
    list => { ... },                    // 完成才交付完整列表
    (loaded, total) => { ... });        // 可选进度；>500 行分帧物化
ConfigManager.ClearRoleRune();
ConfigManager.PreloadAll(() => { ... });
ConfigManager.ClearAll();
```

缓存与逐出：闲置超过 180s 的表会逐出解析层（Reader/行对象/字符串缓存），YooAsset handle 与 bytes 常驻，再次访问同帧秒回。不要跨帧长期缓存 `DictionaryForConfig`；逐出后继续用会报错，需重新调用 `ConfigManager.Get{表}`。

### 12.1 Excel 源表格式

源表格式、约束与示例见 ScriptDevGuide §11.1。

### 12.2 设计要点

- Excel 为唯一事实源；
- 底座在 `Invariable.ConfigManagerCore`，HotUpdate `ConfigManager` 转发；
- 整表 bytes 可驻留，行对象按 ID 惰性反序列化；
- 纯数值改动只需导表 + 构建 AssetBundle；表结构变更才需重导 HybridCLR DLL；改底座需重发基础包；
- 不应手工修改生成文件。

生成表：

- `Config_Player`
- `Config_RoleRune`

## 13. CDN 根地址

运行时 CDN 根地址由 `SdkManager.Instance.GetCDNPath()` 按平台返回 `CDNPathWeChat` 或 `CDNPathDouYin`（`Assets/Scripts/Invariable/Utils/InvariableConst.cs`）。YooAsset 远程根为 `{GetCDNPath()}/yoo`。完整配置、打包写入、本地暂存目录与留空后果见 HotUpdateBuildAdapt §4。

## 14. 工具类

`Utils` 为普通类，成员均为静态。

主要能力：

- 固定 UI Camera、UI Root 查找（`Utils.UICamera` / `Utils.UIRoot`，路径常量来自 `InvariableConst`）；
- GameObject/Transform 获取和克隆、`HideAllChildren`；
- 远程头像 URL 走 `SetRemoteImage`（下载为 Texture2D 后赋 `Image.sprite` / `RawImage.texture`）；图集不走 Utils，由业务脚本挂载 `SpriteAtlas` 后 `GetSprite` 赋值（见 §6）；
- 灰度材质（`SetGray`）；
- Animation 播放；
- 按字符串查找/添加组件（`GetComponent` / `AddComponent`，含 `HotUpdate` 程序集类型解析）；
- UI 页面打开/关闭（`OpenUIPrefabPanel` / `CloseUIPrefabPanel`）；面板父节点按 layer 选择 `InvariableConst.UIPanelPath_0..3`；
- Manager GameObject 创建；
- 文件大小格式化（`FormatFileByteSize`，与 `ConfigUtils.FormatFileByteSize` 重复实现）；
- HTML 颜色解析。

这类 API 依赖字符串路径和资源地址，使用前应优先检查空对象与地址有效性。

## 15. 日志输出

规范见 ScriptDevGuide §3.13。

## 16. UOS 服务与云存档

`Assets/Resources/UOSSettings.asset` 经 UOS Launcher 关联 UOS App（所有游戏共享同一 App）。

云存档链路：

1. `CloudManager.InitCloudData`；
2. `SdkManager.PlatformLogin`（`WX.Login` / `TT.Login`（`forceLogin=true`））；
3. Func Stateless 云函数（code 换 openid，再签发云存档令牌）；
4. `AuthTokenManager.SaveToken`（只存 AccessToken + UserId）；
5. CloudSave 单存档 KV 拉取/上传。上传前校验令牌有效性，临期/过期自动重签；遇 401 再重签并重试一次。

数据隔离规则：

- `userID = wx-` / `dy-` + openid（openid 按平台小游戏隔离）；
- 玩家存档 `namespace = kv_{游戏标识}_player`（`CloudManager.CloudSaveGameId`，每个游戏项目必须唯一）；
- 排行榜快照 `namespace = kv_{游戏标识}_rank_{平台}`（`wx` / `dy`），同 namespace 下用 `userId` 区分：世界榜 `rank_world`、日榜 `rank_day`，微信与抖音分榜不混排；
- 后台显示名（仅展示，不参与定位）：玩家存档「微信玩家数据」/「抖音玩家数据」，快照「微信世界排行榜」/「微信每日排行榜」/「抖音世界排行榜」/「抖音每日排行榜」；
- 玩家资料字段统一为 `UserId` / `NickName` / `AvatarUrl`：玩家存档写在 JSON 内容里，排行榜条目写在顶层与 `Data` 并列（`Data` 只保留排行分数等业务数据）；
- 同游戏同账号才同数据。

客户端入口：`CloudManager` 负责 `InitCloudData` / `GetRankList` / `ReportRankScore`；`SdkManager` 负责 `SetCloudData` / `GetCloudData`（与本地存储同属数据存储模块）以及 `SyncPlatformUserInfo` / `TryGetPlatformUserInfo` / `RequestPlatformUserInfoAuth` / `DestroyPlatformUserInfoButton`（`SyncPlatformUserInfo` 与 `RequestPlatformUserInfoAuth` 为双回调：`authStateCallBack` 上报 `PlatformAuthStates`（`Invariable` 顶层枚举：`Unsupported` / `NotAuthorized` / `Denied` / `Authorized`），`userInfoCallBack` 返回昵称/头像获取结果，授权按钮显隐由业务层按状态决定，`MainPanel` 仅 `NotAuthorized` 显示授权按钮，`Authorized` 时本局上报一次排行榜）。`GetRankList(rankKey, rankType, callBack)`：`rankType` 必填（`CloudRankTypes.World` / `Day`），云函数校验 `gameId` 与 `CloudHelper.Secrets.GameId` 一致后，按客户端传入的平台标识自行拼 `kv_{gameId}_rank_{platform}`，再按 `rankType` 读 `rank_world` 或 `rank_day` Top100 快照（list + 详情 + 下载共 3 次请求）；快照由 `ReportRankScore` 写入时维护 `rankKey` 降序，读取仅截取前 `CloudGetAllMaxCount = 100` 名返回。日榜 0-5 点（UTC+8）只读前一天完整数据，5 点由云函数定时任务 `ResetDayRank` 主动清空（5 点后日榜立即为空），写入侧惰性清空兜底。`ReportRankScore(rankKey, score)`：数据变化时上报，云函数同时增量维护世界榜与日榜，并写入条目顶层 `UserId` / `NickName` / `AvatarUrl`（与 `Data` 并列，空值保留已有资料，`Data` 只保留排行分数等业务数据）；上榜判断由云函数以云端快照为准：已上榜者每次上报直接覆盖分数和其它排行榜数据；未上榜者榜满 100 需超过榜尾才上榜、榜不满时分数需大于 0。日榜 0-5 点停止写入（世界榜照常更新），5 点后允许写入，若快照日期不是当天则惰性清空再写入。玩家存档上传同样写入 `CloudDataKeys.UserId` / `NickName` / `AvatarUrl`。微信首次/未授权必须 `WX.CreateUserInfoButton` 用户点击，已授权后 `WX.GetUserInfo` 可刷新。抖音授权为两段链路：同步时 `TT.GetUserInfoAuth` 检查，已授权走 `TT.GetUserInfo`，未授权显示授权锚点；`TT.Authorize("scope.userInfo")` 在 `SdkManager.RequestPlatformUserInfoAuth`（锚点点击）中，授权成功后再 `TT.GetUserInfo`。未授权不阻塞存档。头像只存 URL，显示走 `Utils.SetRemoteImage`，域名配到 MP 后台 downloadFile，不进 UOS 白名单。客户端 SDK 只能读当前玩家自己的存档，也不能直接指定 namespace。业务侧回调类型为 `CloudService.PlayerCloudData`（位于 `Assets/Scripts/CloudService/Model/PlayerCloudData.cs`）。资料键契约：`CloudService.CloudDataKeys`。

编辑器环境 `SdkManager.SetCloudData` / `GetCloudData` 走本地存储；真机转发 `CloudManager` 云缓存；云初始化失败后 Set 静默丢弃、Get 返回默认值。

云函数部署步骤见 NewProjectSetup §7。微信/抖音 MP 后台合法域名配置见 NewProjectSetup §4.2。