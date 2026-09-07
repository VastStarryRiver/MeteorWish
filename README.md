# 框架内容概述

## 1. 文档

先阅读本文件，再按需求阅读对应文档：

1. [FrameworkAndProcess.md](Doc/FrameworkAndProcess.md) `框架架构与运行流程`
   - 项目技术栈
   - 程序集边界
   - 启动、资源更新、HybridCLR 热更新流程
   - 管理器、事件、计时器、UI、配置、UOS 云存档等模块
2. [ScriptDevGuide.md](Doc/ScriptDevGuide.md) `脚本开发与修改指南`
   - 新脚本应该放在哪里
   - 新增 UI、业务功能、配置表、资源、平台能力的方法
   - 常用 API、命名约定和修改检查清单
3. [HotUpdateBuildAdapt.md](Doc/HotUpdateBuildAdapt.md) `构建热更新与平台适配`
   - 微信/抖音平台差异
   - HybridCLR DLL、YooAsset、CDN 和小游戏构建顺序
   - 热更新发布边界（含云函数上传与远程调用）
4. [NewProjectSetup.md](Doc/NewProjectSetup.md) `新项目准备`
   - 复制工程后的环境恢复与版本控制注意
   - UOS 后台、微信/抖音平台后台、服务器域名
   - 工程内配置修改、云函数部署、首发构建与验证清单

## 2. 亮点分析

| 维度 | 质量 | 亮点 | 关键实现 |
|---|---|---|---|
| 架构与热更 | 优 | 单向依赖零反向引用，AOT↔热更仅 3 处反射触点；热更 DLL / 配置 bytes / UI Prefab 全走 YooAsset，可纯热更发布；HybridCLR AOT 元数据并行加载 | `HotUpdateOver` / `Utils` / `YooAssetManager` |
| 平台适配 | 优 | 微信 / 抖音 / 编辑器三分支 `#if` 隔离；登录、键盘、安全区、广告、分享、侧边栏等能力统一入口封装 | `SdkManager`、`MINIGAME_SUBPLATFORM_*` |
| 启动流程 | 优 | 状态机串行驱动启动链，各节点职责单一，失败可定位到具体节点 | `Launcher`、`StateMachine`、`InitializeYooAsset` → `HotUpdateOver` |
| 事件与计时 | 优 | 泛型事件总线触发快照（快照列表经 `PoolUtils` 池化复用）并逐 listener 隔离；秒/帧双最小堆计时器由 `Update` 驱动；事件 / 计时器 key 全常量化 | `GameManager`、`InvariableConst` / `HotUpdateConst` |
| 配置表系统 | 优 | CFGT magic + schemaHash 三处同源；导表严格失败保证一致性；三层缓存与大表分帧物化；独立回读交叉验证 | `ConfigReader` / `ConfigManagerCore` / `ConfigValidator`、schemaHash |
| UI 系统 | 优 | 打开页面单一入口且加载中去重；UIPanel / UIPopup 职责切分；FloatText 内部 item 对象池复用、播完自动隐藏与对称清理 | `Utils.OpenUIPrefabPanel`、`UIPanel` / `UIPopup`、`FloatTextPanel` |
| 音频系统 | 优 | BGM 单通道循环，SFX 每 clip 一源打断重播；仅挂载播放；音量经平台层本地持久化 | `AudioManager`、`SdkManager` |
| 资源与性能 | 优 | 同地址在途去重；闲置句柄 180s / 30s 扫描逐出并白名单兜底；配置分帧物化、字符串缓存、`PoolUtils` 对象池降峰值 | `YooAssetManager`、`ConfigManagerCore`、`TryUnloadUnusedAsset` |
| 云服务 | 优 | 密钥走环境变量分层；写后 2s 防抖 + 串行上传 + dirty 重标记；世界榜/日榜快照增量维护 Top100，查看只读 3 次请求；命名空间服务端自拼 | `CloudHelper` / `CloudManager` / `SdkManager`、`ReportRankScore` / `GetRankList` |
| 编辑器工具链 | 优 | Excel→bytes→生成代码→运行时校验→独立回读闭环；菜单 priority 编码流水线顺序；生成代码 UTF-8 无 BOM + LF + 防注入 | `ConfigImporter` / `CodeGenerator` / `DllTool` / `AssetBundleTool` |

## 3. 一句话概述

这是一个基于团结引擎1.9.3的微信/抖音小游戏框架，使用：

- `Invariable` 程序集承载不可热更的启动框架、平台 SDK、资源管理和基础组件；
- `HotUpdate` 程序集承载可通过 HybridCLR 更新的业务代码、UI 脚本和生成配置；
- `CloudService` 程序集承载 UOS Func Stateless 云函数与云存档数据模型；
- YooAsset 管理远程资源和热更新 DLL；
- 微信 SDK / 抖音 StarkSDK 提供平台能力；
- 启动完成后通过反射调用 `HotUpdate.StartGame.Play()` 进入业务层。

核心调用链：

```text
Assets/Scenes/Start.scene
  -> Invariable.Launcher
  -> InitializeYooAsset
  -> CheckCatalogUpdate
  -> CheckResourceUpdates
  -> HotUpdateOver
  -> SdkManager.InitSDK
  -> CloudManager.InitCloudData
  -> YooAssetManager.PreLoadDll
  -> 反射 HotUpdate.StartGame.Play
  -> Utils.OpenUIPrefabPanel("MainPanel", 0)
```

## 4. 最重要的目录边界

```text
Assets/
├─ Scripts/
│  ├─ Invariable/          # 不可热更：启动、平台、资源、基础能力、云存档客户端、配置底座
│  ├─ HotUpdate/           # 可热更：业务、UI、生成配置
│  └─ CloudService/        # 不可热更：UOS 云函数与云存档数据模型
├─ GameAssets/             # YooAsset 收集的动态资源
│  ├─ DLL/MiniGame/        # 加密后的 HotUpdate/AOT DLL .bin
│  ├─ Prefabs/UI/          # UI 预制体
│  ├─ Audios/              # 音频
│  ├─ Atlas/               # 图集
│  ├─ Animation/           # 动画
│  ├─ Materials/           # 材质
│  ├─ Png/                 # 独立图片
│  ├─ Config/              # 导表 bytes（YooAsset Config 组）
│  └─ Scenes/              # 动态场景
├─ Resources/LocalAssets/  # 首包本地资源：加载面板
├─ Scenes/                 # 唯一构建场景 Start.scene
├─ ProjectSettings/        # UIParticle 等设置资产
├─ Editor/MyTools/         # 编辑器工具（仅 Editor 平台）
│  ├─ Config/              # Excel 导表与校验
│  ├─ DllTool/             # HybridCLR DLL 生成与复制
│  ├─ AssetBundle/         # YooAsset Bundle 构建
│  ├─ CustomBuild/         # 微信/抖音打包与 CDN 复制
│  ├─ AssetImporter/       # .bin 导入为 BinAsset
│  ├─ AssetProcess/        # 音频/图片/图集导入设置（VastStarryRiver/资源处理 菜单）
│  ├─ AtlasBuilder/        # 通用纹理打包（多图合 Multiple Sprite PNG，ContextMenu BuildAtlas，输出在 Editor 目录）
│  └─ InspectorEditor/     # 自定义 Inspector（UIButtonEditor）
├─ ToolPackage/            # 本地第三方库
│  ├─ DOTween/             # 预编译 DLL + Modules 源码
│  ├─ TextMesh Pro/
│  ├─ UniTask/
│  └─ YooAsset/            # 微信/抖音 YooAsset 自定义文件系统
├─ Plugins/                # 预编译库（ExcelDataReader.dll 等）
├─ WX-WASM-SDK-V2/         # 微信运行时 SDK（Editor + Runtime）
├─ UOSLauncherEncrypt/     # UOS Launcher 自带加密模块，勿改
├─ HybridCLRGenerate/      # HybridCLR 生成物（link.xml、AOTGenericReferences.cs）
└─ Settings/Build Profiles/# 微信和抖音 Build Profile

LocalPackages/             # 抖音 StarkSDK 本地包
Excel/                     # 配置源文件（Player.xlsx、RoleRune.xlsx）
```

## 5. 修改位置快速决策

| 需求 | 默认修改位置 | 是否可只发布热更新 |
|---|---|---:|
| 新玩法、数值逻辑、业务状态 | `Assets/Scripts/HotUpdate` | 是 |
| 新 UI 页面脚本 | `Assets/Scripts/HotUpdate/UI` | 是，但预制体也要进入 YooAsset 更新 |
| 修改 Excel 数值 | `Excel`，然后重新导出配置 | 是 |
| UI 基础控件、资源框架、启动链 | `Assets/Scripts/Invariable` | 否，通常需要重新发布小游戏基础包 |
| 微信/抖音 SDK 能力、平台登录、本地/云读写入口 | `Invariable/Manager/SdkManager.cs` | 否 |
| 云存档初始化、排行榜拉取、云缓存 | `Invariable/Manager/CloudManager.cs` | 否 |
| 云函数/平台密钥 | `Assets/Scripts/CloudService` | 否；改后需重新上传云函数并切远程调用 |
| 云存档数据模型 DTO | `Assets/Scripts/CloudService/Model` | 否；改契约需同步重新上传云函数 |
| YooAsset 小游戏文件系统 | `Assets/ToolPackage/YooAsset` | 否 |
| 编辑器导出/构建工具 | `Assets/Editor/MyTools` | 不属于运行时发布 |
| 修改启动加载面板 | `Assets/Resources/LocalAssets` 与 `Invariable/Workflow` | 通常否 |
| 新动态图片、音频、Prefab、场景 | `Assets/GameAssets` | 可通过资源更新发布 |
| 修改资源地址规则 | `Assets/AssetBundleCollectorSetting.asset` | 高风险，需重新构建并验证全量资源 |

基本原则：

1. **业务需求优先写入 `HotUpdate`。**
2. 只有“热更新 DLL 加载前必须执行”或“直接依赖平台 SDK”的代码才放入 `Invariable`。
3. `Invariable` 不得直接编译引用 `HotUpdate`，通过反射跨越程序集边界。
4. `HotUpdate/Config/Generated/Config_*.cs` 是生成文件，数值修改应改 Excel 后重新导出；底座在 `Invariable/Config`。
5. 不要直接修改 `Assets/GameAssets/DLL/MiniGame/*.dll.bin`；它们由 DLL 工具生成。

## 6. 后续需求建议

为了快速且安全地新增或修改脚本，最好想先清楚需求内容，具体包含：

```text
【目标】
要新增或修复什么？

【验收条件】
玩家执行什么操作后，应看到什么结果？

【影响平台】
编辑器 / 微信 / 抖音 / 全部

【热更新要求】
是否必须仅通过热更新发布？

【涉及资源】
Prefab、图片、音频、场景、Excel 表名及资源地址（如有）

【复现步骤】
BUG 出现前的操作、实际结果、预期结果、日志或截图

【兼容要求】
是否需要兼容已有存档、已发布资源清单或已发布客户端？
```

## 7. 后续修改代码时的流程

1. 读取本目录文档和相关源码；
2. 判断修改应位于 `HotUpdate`、`Invariable` 还是编辑器工具层；
3. 搜索调用方、Prefab 绑定、YooAsset 地址和平台条件编译；
4. 实施最小范围修改；
5. 检查编译边界、空引用、生命周期、事件/计时器清理；
6. 尽可能进行静态检查或引擎编译验证；
7. 汇报改动文件和内容、发布平台类型、需在编辑器/真机完成的验证。

## 8. 框架状态摘要

- 引擎：团结引擎 `1.9.3`，对应 Unity `2022.3.62t11`。
- 唯一构建场景：`Assets/Scenes/Start.scene`。
- YooAsset 包名：`MyPackage`。
- HybridCLR 热更新程序集：`HotUpdate`。
- 热更新入口：`HotUpdate.StartGame.Play()`。
- 首个业务页面：`MainPanel`，UI 层级 `0`。
- UI 根节点依赖固定路径：`UI_Root/Canvas_{0..3}/Ts_Panel`。
- 启动状态机：`Invariable.StateMachine`，节点为 `InitializeYooAsset` → `CheckCatalogUpdate` → `CheckResourceUpdates` → `HotUpdateOver`。
- 配置表类型：`int` / `int[]` / `float` / `float[]` / `string` / `string[]`；源表位于 `Excel/`（仅 .xlsx/.xls），导表产物为 `GameAssets/Config/*.bytes` 与 `HotUpdate/Config/Generated/Config_*.cs`。
- 微信/抖音平台通过条件宏切换：
  - `MINIGAME_SUBPLATFORM_WEIXIN`
  - `MINIGAME_SUBPLATFORM_DOUYIN`
- 安全区为固定偏移，不是根据设备实时安全区计算。
- UOS：Launcher / CloudSave / Func Stateless；玩家存档 namespace 为 `kv_{CloudManager.CloudSaveGameId}_player`，排行榜快照为 `kv_{CloudManager.CloudSaveGameId}_rank_{平台}`（`wx` / `dy` 分榜），须与 `CloudHelper.Secrets.GameId` 一致。后台显示名（仅展示）：玩家存档「微信玩家数据」/「抖音玩家数据」，快照「微信世界排行榜」/「微信每日排行榜」/「抖音世界排行榜」/「抖音每日排行榜」。玩家存档 JSON 含 `CloudDataKeys.UserId` / `NickName` / `AvatarUrl`；排行榜条目为 `UserId` / `NickName` / `AvatarUrl` / `Data` 并列（`Data` 只保留排行分数等业务数据）；授权入口 `SdkManager.RequestPlatformUserInfoAuth`，状态经 `PlatformAuthStates`（`Invariable` 顶层枚举）回业务层，授权按钮显隐由业务层负责（`MainPanel` 仅未授权显示，已授权本局上报一次排行榜）；微信首次需 `createUserInfoButton`，抖音需 `scope.userInfo` 授权；未授权不阻塞存档。
- `HotUpdate` 引用 `Invariable` 与 `CloudService`（消费 Model DTO，如 `PlayerCloudData`）；项目内程序集统一名称引用，第三方包用 GUID。
- 云读写业务入口：`SdkManager.SetCloudData` / `GetCloudData`；云初始化：`CloudManager.InitCloudData`。
