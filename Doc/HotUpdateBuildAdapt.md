# 构建、热更新与平台适配

## 1. 平台概览

项目同时支持：

- 微信小游戏；
- 抖音小游戏；
- 编辑器模拟模式。

平台条件宏：

```csharp
MINIGAME_SUBPLATFORM_WEIXIN
MINIGAME_SUBPLATFORM_DOUYIN
```

平台文件系统还要求：

```csharp
UNITY_WEBGL
```

因此小游戏运行时通常需要同时满足：

```text
UNITY_WEBGL + 对应 MINIGAME_SUBPLATFORM 宏
```

Build Profile：

```text
Assets/Settings/Build Profiles/
├─ WeChat Profile.asset
└─ DouYin Profile.asset
```

切平台两步缺一不可：

1. 切换 minigame 子平台（`PlayerSettings.MiniGame.SetActiveSubplatform`），写入编译宏
2. 启用对应 Build Profile（`EditorBuildSettings.buildProfiles` 目标 enabled，其余关闭）

代码方式固定顺序：先切子平台 → 等域重载完成 → 再启用对应 Profile。顺序不可颠倒，域重载会清空 Profile enabled。手动操作一律在团结引擎 Build Profile 窗口勾选目标 Profile，引擎会同时完成子平台切换与 Profile 启用。不要只改宏文本。

打包菜单由 `MINIGAME_SUBPLATFORM_WEIXIN` / `MINIGAME_SUBPLATFORM_DOUYIN` 编译期宏决定可用性。菜单开头跑 `PreBuildValidator`：CDNPath、本地源码里的 `CloudHelper.Secrets` 赋值（远程桩会剥掉密钥，不扫源码字段）、Profile enabled 与当前目标一致、平台宏唯一、当前目标平台的 SDK 配置与 Profile 成对字段一致。硬条件失败则中止打包。校验通过后只在控制台提醒自行确认远程调用模式，工程内无法验证 Func Stateless 是否已切远程。非目标平台的配对漂移只记日志，不阻断本次打包。

> Profile 中包含 AppID、绝对构建路径等环境相关信息。CDN 字段由打包菜单按当前平台常量（`CDNPathWeChat` / `CDNPathDouYin`）自动写入，无需手填。文档不重复记录具体值；修改或分享时应注意凭据和环境隔离。

## 2. `SdkManager` 平台能力

文件：

```text
Assets/Scripts/Invariable/Manager/SdkManager.cs
```

统一封装：

| 能力 | Editor | 微信 | 抖音 |
|---|---|---|---|
| SDK 初始化 | 立即回调 | `WX.InitSDK` | `TT.InitSDK` |
| 平台登录 | 返回 null | `WX.Login` | `TT.Login`（`forceLogin=true`） |
| 小游戏版本更新 | 无 | `WXUpdateManager` | `TTUpdateManager` |
| 本地字符串存储 | PlayerPrefs | WX Storage | TT Save |
| 云存档读写入口 | 走本地存储 | 转发 `CloudManager` 云缓存 | 转发 `CloudManager` 云缓存 |
| 原生键盘 | `ShowKeyboard` 会置位 `m_isKeyboardShowing`，但编辑器分支 `HideKeyboard` 无复位，后续调用全部直接返回 | WX Keyboard | TT Keyboard |
| 方向变化 | 直接适配 | WX 监听 | TT 监听 |
| 激励视频 | 直接回调成功 | 已有框架 | 已有框架 |
| 侧边栏复访 | `GameLog.Info` | `GameLog.Info` | 有（跳转成功写入本地 IsGetReward=1） |
| 游戏圈按钮 | `GameLog.Info` | 有 | `GameLog.Info` |
| 分享 | `GameLog.Info` | WX.ShareAppMessage | TT.ShareAppMessage（成功/失败/取消回调） |
| 用户信息授权/获取 | `SyncPlatformUserInfo` 上报 `Unsupported`，`userInfoCallBack` 回 false | 已授权 `WX.GetUserInfo` 并上报 `Authorized`；未授权先上报 `NotAuthorized` 再 `WX.CreateUserInfoButton`，拒绝上报 `Denied` | 同步时 `GetSetting` 检查，已授权 `TT.GetUserInfo` 成功上报 `Authorized`、失败上报 `NotAuthorized`；未授权上报 `NotAuthorized`，锚点按钮触发 `RequestPlatformUserInfoAuth`（`TT.Authorize`），同意上报 `Authorized`、拒绝上报 `Denied`；授权按钮显隐由业务层负责（`MainPanel` 仅 `NotAuthorized` 显示） |
| 环境判断 | IsWeChat/IsDouYin 均返回 false | IsWeChat 返回 true | IsDouYin 返回 true |
| YooAsset 文件系统 | 不走此接口 | 微信 FS | 抖音 FS |

## 3. YooAsset 小游戏平台文件系统

### 3.1 微信

创建位置：

```csharp
string packageRoot =
    $"{WX.env.USER_DATA_PATH}/__GAME_FILE_CACHE/yoo";
```

文件系统：

```text
WechatFileSystem
```

主要行为：

- 使用 `WXFileSystemManager`；
- 下载远程 Bundle 到微信缓存；
- 通过 `WX.GetCachePath` 判断缓存；
- 支持清理全部或未使用 Bundle；
- 创建时检查远程 URL 是否含双斜杠；
- 远程根地址来自 `SdkManager.Instance.GetCDNPath()` 的 `/yoo` 子路径。

### 3.2 抖音

创建位置：

```csharp
string packageRoot = "yoo";
```

文件系统：

```text
TiktokFileSystem
```

主要行为：

- 使用 `TTFileSystemManager`；
- 通过 URL 缓存接口判断和加载；
- 远程根地址同样来自 `SdkManager.Instance.GetCDNPath()` 的 `/yoo` 子路径。

## 4. CDN 根地址配置

运行时 CDN 根地址由 `SdkManager.Instance.GetCDNPath()` 按平台返回 `InvariableConst.CDNPathWeChat` 或 `InvariableConst.CDNPathDouYin`（`Assets/Scripts/Invariable/Utils/InvariableConst.cs`）。YooAsset 远程根为 `{GetCDNPath()}/yoo`。

打包微信/抖音小游戏时，菜单会把对应平台常量写入平台配置资产的 `CDN` 字段后再构建，无需手填 Profile：

- 微信：写入 `CDNPathWeChat` 到 `Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset`、`Assets/Settings/Build Profiles/WeChat Profile.asset`
- 抖音：写入 `CDNPathDouYin` 到 `Assets/Settings/Build Profiles/DouYin Profile.asset`、`Assets/Editor/StarkBuilderSetting.asset`

只同步 `CDN` 字段；`StreamCDN` / `AssetsUrl` / `wasmResourceUrl` 不改。`ConfigUtils.CdnPath` 是本地 `CDN/` 暂存目录，复制菜单按当前平台落到 `CDN/WeChat` 或 `CDN/DouYin`，与远程根不是同一概念。

`CDNPathWeChat` / `CDNPathDouYin` 必须填写完整 URL。留空时编辑器模拟模式可跑，但真机 WebPlayMode 远程根变为 `/yoo`，资源下载必失败；打包菜单会把空串写入上述平台配置资产的 `CDN` 字段。打包前必查。

两字段属 `Invariable`，修改后必须重新构建基础包。

工程未启用微信 Instant Game AutoStreaming；若开启，Build Profile 路径可能用 SDK 的 AutoStreaming CDN 覆盖 `ProjectConf.CDN`，需单独确认。

## 5. Excel 配置导出

菜单：

```text
VastStarryRiver/Config/导出Excel配置
```

编排入口：`ConfigImporter.RebuildAll`（由 `ConfigTool` 菜单调用）。

流程：

```text
Excel/*（xlsx/xls）
  -> ConfigImporter.RebuildAll（先清空已有 Config_*.cs / Preload / .bytes）
  -> ExcelReader（InvariantCulture 读单元格；xlsx/xls 只读第一个 sheet）
  -> FieldAnalyzer（表头 3 行解析，首列 Id；字段名合法标识符/非关键字/不重名校验）
  -> ConfigBinaryWriter（写 GameAssets/Config/{表}.bytes；单元格解析失败带 表名+字段+行号）
  -> CodeGenerator（写 HotUpdate/Config/Generated/Config_{表}.cs + ConfigManager.Preload.cs；GetByID 使用 TryGetValue）
  -> 任一表失败：中断并再次清空全部产物 → 编译进入 HotUpdate.dll（成功时）
```

说明：

- `.bytes` 进入 YooAsset Config 组，地址为 `Config_{表名}`，可随资源热更；
- **严格失败模式**：任一张表失败即中断，并清空全部配置产物（要么全量正确，要么全空，避免带着问题表发布）；
- 导表清理范围为已有 `Config_*.cs` / `ConfigManager.Preload.cs` / `.bytes`，不删除整个 `HotUpdate/Config` 目录；
- **纯数值改动**：导表后只需构建 AssetBundle；
- **表结构变更**：等待脚本重新编译后，再按完整 HybridCLR + YooAsset 流水线导出（见 §12.2）。

## 6. HybridCLR DLL 构建

HybridCLR 包钉在 `v8.14.1`（`Packages/manifest.json` 的 `#v8.14.1`）。团结 1.9.3 必须使用该版本或更新：Installer 会拉取 `hybridclr` 分支 `v8.13.0` 与 `il2cpp_plus` 分支 `v2022-tuanjie-8.14.0`，并覆盖 `HybridCLRData/LocalIl2CppData-WindowsEditor`。引擎大版本升级后必须走一遍 `HybridCLR/Installer` 再 `HybridCLR/Generate/All`，不要沿用旧 il2cpp。

微信 Player Settings 的 `Use Slim Format For global-metadata.dat`（`weixinMiniGameUseSlimMetaFileFormat`）必须保持关闭。1.9.3 空工程默认开启该选项，开启后 HybridCLR 无法在微信小游戏运行。

编辑器菜单：

```text
VastStarryRiver/DLL/导出所有DLL
VastStarryRiver/DLL/复制热更新DLL
VastStarryRiver/DLL/复制元数据DLL
```

### 6.1 导出所有 DLL

调用：

```csharp
PrebuildCommand.GenerateAll();
```

主要输出：

```text
HybridCLRData/
├─ HotUpdateDlls/{ActiveBuildTarget}/HotUpdate.dll
└─ AssembliesPostIl2CppStrip/{ActiveBuildTarget}/*.dll

Assets/HybridCLRGenerate/
├─ link.xml
└─ AOTGenericReferences.cs
```

### 6.2 复制热更新 DLL

读取：

```text
HybridCLRData/HotUpdateDlls/{platform}/HotUpdate.dll
```

加密输出：

```text
Assets/GameAssets/DLL/{platform}/HotUpdate.dll.bin
```

### 6.3 复制 AOT 元数据 DLL

固定列表，单一事实源为 `InvariableConst.AotDllNames`（编辑器复制与运行时加载共用）：

```text
mscorlib
System
System.Core
Newtonsoft.Json
```

输出：

```text
Assets/GameAssets/DLL/{platform}/{AOT程序集}.dll.bin
```

### 6.4 平台名强约束

编辑器工具使用：

```csharp
EditorUserBuildSettings.activeBuildTarget.ToString()
```

运行时代码却固定调用：

```csharp
LoadMetadataForAOTAssemblies("MiniGame", ...)
```

因此构建目标字符串必须产生目录 `MiniGame`，资源地址才会匹配：

```text
MiniGame_HotUpdate.dll
MiniGame_System.dll
...
```

若 BuildTarget 名变化，需同步修改运行时或生成路径。

若重建或重命名 `HotUpdate.asmdef`，需在 HybridCLR 设置面板确认热更程序集引用仍然有效（静态检查可能出现设置内引用与 `.meta` 标识编码不一致，以编辑器面板为准）。项目内程序集（`Invariable` / `CloudService` / `MyTools` / `HotUpdate`）之间的引用统一写名称；第三方包用 GUID。

## 7. YooAsset 构建

菜单：

```text
VastStarryRiver/构建AssetBundle
```

实现：

```text
AssetBundleTool.cs
```

参数：

- Pipeline：`ScriptableBuildPipeline`
- Package：`MyPackage`
- Bundle 类型：AssetBundle
- BuildTarget：已激活目标
- 版本号：`yyyyMMddHHmmss` 风格的数字字符串
- 共享打包：开启
- 构建结果校验：开启
- 压缩 / 文件名样式 / 加密服务：取自 YooAsset Builder Settings（EditorPrefs）；默认为 LZ4、HashName、不加密

输出路径：

```text
<项目根>/Bundles/{构建目标}/MyPackage/{数字版本}
```

最新输出路径通过遍历版本目录并选择最大数字版本得到。内置资源根为 `Assets/StreamingAssets/yoo`，工程不存在该目录（首包不内置 Bundle）。

## 8. 复制资源到 CDN

菜单：

```text
VastStarryRiver/打包/复制bundle到CDN目录
```

流程：

1. 删除本地 `CDN/{WeChat|DouYin}/yoo`（仅当前激活平台子目录）；
2. 找到 `MyPackage` 最新数字版本目录；
3. 将该目录顶层文件复制到 `CDN/{WeChat|DouYin}/yoo`。

远程目录必须满足：

```text
{SdkManager.Instance.GetCDNPath()}/yoo/{YooAsset清单和Bundle文件}
```

注意：

- 工具只复制最新输出目录的顶层文件；
- 目标目录会被整个删除；
- 上传 CDN 前确认没有混入错误平台或不一致的产物；
- 清单和 Bundle 必须作为一个一致版本上传；
- CDN 缓存规则不能导致新清单引用尚未生效的 Bundle。

## 9. 微信小游戏构建

菜单：

```text
VastStarryRiver/打包/打包微信小游戏
```

仅在微信宏激活时可用。菜单开头先跑 `PreBuildValidator`，不通过则中止，不会写入 CDN 或调用 `WXConvertCore.DoExport()`。

核心调用：

```csharp
WXConvertCore.DoExport()
```

Build Profile：

```text
Assets/Settings/Build Profiles/WeChat Profile.asset
```

输出约定：

```text
Build/WeChat
```

打包前会整体删除已有的 `Build/WeChat` 目录再重建。

Profile 中包含：

- AppID；
- CDN；
- 竖屏方向；
- WebGL2；
- 内存；
- Brotli；
- 首包/StreamingAssets 相关设置；
- 绝对输出路径。

路径是机器相关配置。迁移工程目录后应重新检查。

手动修改平台配置必须成对同步：`Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` 与 `WeChat Profile.asset`。打包菜单只自动双写 `CDN` 字段，AppID、输出路径、方向、内存等其它字段需人工同步两侧。

## 10. 抖音小游戏构建

菜单：

```text
VastStarryRiver/打包/打包抖音小游戏
```

仅在抖音宏激活时可用。菜单开头先跑 `PreBuildValidator`，不通过则中止，不会写入 CDN 或调用 `DouYinSubplatformInterface.Build`。

核心调用：

```csharp
BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(
    "Assets/Settings/Build Profiles/DouYin Profile.asset"
);

new DouYinSubplatformInterface()
    .Build(profile, BuildOptions.None);
```

输出约定：

```text
Build/DouYin
```

打包前会整体删除已有的 `Build/DouYin` 目录再重建。

Profile/Stark 设置中包含：

- AppID；
- CDN；
- wasm 内存；
- URL 缓存白名单；
- 压缩；
- 方向；
- 绝对输出路径。

手动修改平台配置必须成对同步：`Assets/Editor/StarkBuilderSetting.asset` 与 `DouYin Profile.asset`。打包菜单只自动双写 `CDN` 字段，AppID、输出路径、方向、内存等其它字段需人工同步两侧。

## 11. 复制代码数据文件到 CDN

菜单：

```text
VastStarryRiver/打包/复制unityweb.bin到CDN目录
```

按编译期平台宏二选一源目录（`#if MINIGAME_SUBPLATFORM_WEIXIN` → `Build/WeChat/webgl`，`#elif MINIGAME_SUBPLATFORM_DOUYIN` → `Build/DouYin/webgl`）；两宏都未激活时路径为空并静默返回。

查找包含以下名称之一的文件：

```text
.webgl.data.unityweb.bin.br
.webgl.data.unityweb.bin.txt
```

复制到：

```text
CDN/{WeChat|DouYin}/
```

匹配到第一个符合条件的文件后即 `break`，因此同一目录存在多个候选文件时只复制首个。

该步骤与 YooAsset 的 `CDN/{WeChat|DouYin}/yoo` 不同，属于平台 WebGL 数据文件发布。

## 12. 推荐的完整构建顺序

切换平台后建议按以下顺序执行。上传 CDN 服务器与真机验证由用户完成。打包菜单会先跑前置校验。远程调用模式须由用户在 UOS / Func Stateless 面板自行确认，工程内无法验证。

### 12.1 首次或基础包完整发布

1. 按切平台顺序激活目标子平台与对应 Build Profile（先切子平台，域重载后再启用 Profile；手动则在 Build Profile 窗口勾选）；
2. 确认只激活正确的平台宏，且 Profile enabled 与当前子平台一致；
3. 检查 Profile 的 AppID、输出路径和方向（CDN 由打包菜单按当前平台常量自动写入）；
4. 确认 `CDNPathWeChat` / `CDNPathDouYin` 为对应平台目标地址；
5. 修改 Excel 后导出 Excel 配置；
6. 等待脚本编译成功；
7. 导出所有 HybridCLR DLL；
8. 复制热更新 DLL；
9. 复制 AOT 元数据 DLL；
10. 构建 YooAsset；
11. 复制 Bundle 到 `CDN/{WeChat|DouYin}/yoo`；
12. 上传/发布 CDN 内容；
13. 上传云函数（`UOS/Func Stateless/Open Panel`）并切换为远程调用模式（首次发布还需在 UOS 控制台配置 `ResetDayRank` 定时触发器，见 NewProjectSetup §7）；
14. 构建微信或抖音小游戏；
15. 如平台数据走 CDN，复制并上传 unityweb 数据文件；
16. 使用平台开发者工具启动；
17. 清缓存和保留缓存两种情况各测试一次；
18. 提交审核或发布。

### 12.2 仅业务代码热更新

前提：只改 `HotUpdate`，没有改 `Invariable`、平台 SDK、首场景和首包资源。

1. 确认目标平台仍正确；
2. 如改了 Excel，重新导出配置；
3. 生成 HybridCLR DLL；
4. 复制 `HotUpdate.dll.bin`；
5. 若 AOT 依赖未变化，可不一定重复制元数据，完整流水线建议一并复制；
6. 构建 YooAsset 新版本；
7. 复制并上传 `CDN/{WeChat|DouYin}/yoo`；
8. 验证远程新清单和 Bundle；
9. 在已发布基础包上启动验证热更新。

### 12.3 仅动态资源更新

1. 修改 `Assets/GameAssets`；
2. 不需要重新构建 HotUpdate DLL，除非脚本也变更；
3. 构建 YooAsset；
4. 上传最新清单和 Bundle；
5. 在已发布基础包上验证下载与加载。

## 13. 什么不能只靠热更新发布

以下修改通常需要重新发布基础小游戏包：

- `Assets/Scripts/Invariable`；
- `Assets/Scripts/CloudService`（同时需重新上传云函数并确认远程调用模式）；
- `Assets/ToolPackage/YooAsset`；
- `Assets/Scenes/Start.scene`；
- `Assets/Resources/LocalAssets`；
- 微信/抖音 SDK 版本；
- Build Profile 和 PlayerSettings 中影响运行时的设置；
- AOT 代码和基础程序集引用变化；
- 引擎版本；
- HybridCLR 框架配置；
- 首包加载界面或平台转换配置。

Editor 工具本身不进入运行时，但其产物变化可能要求重新构建。

## 14. 平台功能验证矩阵

每次发布建议至少验证：

| 场景 | Editor | 微信真机 | 抖音真机 |
|---|---:|---:|---:|
| 首次无缓存启动 | 模拟 | 必测 | 必测 |
| 有缓存启动 | 不适用 | 必测 | 必测 |
| 资源更新进度 | 模拟有限 | 必测 | 必测 |
| DLL 加载和主界面 | 必测 | 必测 | 必测 |
| 本地存档 | 必测 | 必测 | 必测 |
| 原生键盘 | 无完整模拟 | 必测 | 必测 |
| 前后台切换 | 有限 | 必测 | 必测 |
| 小游戏版本更新 | 无 | 必测 | 必测 |
| 激励视频完整/中断 | 直接回调成功 | 配置后必测 | 配置后必测 |
| 分享成功/失败/取消 | `GameLog.Info` | 必测 | 必测 |
| 安全区/横竖屏 | 有限 | 多机型 | 多机型 |
| CDN 异常/断网 | 可模拟 | 必测 | 必测 |

## 15. 构建产物与源码的对应关系

```text
HotUpdate 源码/生成配置
  -> HybridCLR HotUpdate.dll
  -> ConfigUtils.SaveSafeFile
  -> GameAssets/DLL/MiniGame/HotUpdate.dll.bin
  -> YooAsset Bundle
  -> CDN/{WeChat|DouYin}/yoo
  -> 客户端下载
  -> 解密
  -> Assembly.Load
  -> HotUpdate.StartGame.Play
```

任一环节仍使用过期产物，都会表现为“源码已修改但真机没有变化”。排查时应逐段确认时间戳、清单版本、下载日志和缓存。