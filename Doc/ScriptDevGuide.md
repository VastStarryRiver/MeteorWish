# 脚本开发与修改指南

## 1. 修改前先判断发布边界

### 1.1 应放入 `HotUpdate` 的代码

默认情况下，下列代码都应放入热更新程序集：

- 新玩法；
- 业务状态和业务数据；
- UI 页面逻辑；
- 数值计算；
- 活动、任务、背包等业务模块；
- 可在平台接口之上实现的逻辑；
- Excel 生成配置。

推荐目录：

```text
Assets/Scripts/HotUpdate/
├─ Config/       # 生成文件
├─ UI/           # UI 页面
├─ Utils/        # 热更新通用工具
└─ Workflow/     # 热更新业务入口/流程
```

### 1.2 应放入 `Invariable` 的代码

仅当满足以下条件之一时放入不可热更层：

- 必须在 `HotUpdate.dll` 加载前运行；
- 直接依赖微信或抖音 SDK 类型；
- 修改 HybridCLR/YooAsset 初始化；
- 属于所有业务都依赖的基础 Unity 组件；
- 必须挂在首包本地场景或加载面板；
- 热更新层无法安全实现。

修改 `Invariable` 后，应明确知晓：**需要重新发布小游戏基础包。**

### 1.3 应放入 `Editor/MyTools` 的代码

- Excel 导出；
- 资源导入；
- DLL 复制；
- 自动构建；
- 资源检查；
- 编辑器菜单和批处理。

Editor 代码不能被运行时程序集引用。

## 2. 命名和结构约定

代码约定：

| 内容 | 约定 |
|---|---|
| 不可热更命名空间 | `Invariable` |
| 热更新命名空间 | `HotUpdate` |
| 云服务命名空间 | `CloudService`（不可热更，云函数 + Model DTO） |
| 可写字段（私有/受保护/public 实例，含 static） | `m_` 前缀；例外见 §3.3 |
| `const` / `readonly` 字段 | PascalCase，禁止 `m_` |
| 生成配置表缓存字段 | `m_config{表名}`（由 CodeGenerator 生成，可写 static） |
| UI 页面类名 | 与 Prefab 文件名一致 |
| UI Prefab 地址 | `Prefabs_{Prefab名}` |
| DLL 地址 | `MiniGame_{DLL文件名}` |
| UI 挂载节点 | `UI_Root/Canvas_{layer}/Ts_Panel` |
| 事件名 | 字符串，建议使用 `模块_动作` |
| 计时器 key | 必须全局唯一，建议 `类名_对象ID_用途` |

新增类时建议保持：

- 一个主要类型一个文件；
- 文件名与主要类型名一致；
- 公共 API 写 XML 注释；
- 平台差异集中在 `SdkManager`，业务代码不要散落平台宏；
- 业务层尽量通过平台无关的方法调用 `SdkManager`。

## 3. 代码书写规范

以下规范适用于 `HotUpdate`、`Invariable`、`CloudService` 和 Editor 工具代码。自动生成的 `Config_*.cs` 以生成器输出格式为准（生成器模板本身遵循本章）。

脚本分两类：

- **非预制体挂载**：工具类、Manager、配置底座、云函数、Editor 管线等；
- **预制体挂载**：`Invariable/Component/*`、`Launcher`、`GameLoadingPanel`、`HotUpdate/UI/*` 等挂在 Prefab/场景上的 MonoBehaviour。两类共享本章通用规则；预制体脚本另见 **§3.14**。

基准参考：`ConfigUtils`、`YooAssetManager`、`CloudHelper`、`ConfigBinaryWriter`。

### 3.1 文件整体结构与成员顺序

脚本内容按以下顺序组织：

1. `using` 引用；
2. 命名空间（传统 `namespace X { }`，禁止 file-scoped）；
3. 类型声明；
4. 嵌套类型；
5. 字段（全部集中顶部）；
6. 属性；
7. 构造函数；
8. Unity 生命周期函数；
9. Unity 事件接口方法（`OnPointerClick` 等）；
10. 业务方法；
11. 自定义事件回调。

访问修饰符一律显式声明（含 `private`）；接口成员隐式 `public`，不写访问修饰符。特性（`[MenuItem]`、`[CloudFunc]` 等）独占一行。

### 3.2 `using` 引用

- 每个命名空间单独占一行；
- 只保留脚本实际使用的引用；
- **所有 using（含 System）统一按字母序排列**；
- `using` 区域结束后，与 `namespace` 之间保留 **3 个空行**；
- 不在文件中间声明 `using`；
- 除非类型重名或能明显提高可读性，否则不要使用完整限定类型名代替 `using`；
- 已 `using` 的命名空间禁止再写完全限定名；仅真实类型重名（如 `Object`）才保留限定名或别名；
- 条件编译块（`#if`）内的平台 using 保持块内原位，不参与块外字母序重排；
- 生成文件（`Config_*.cs` / `ConfigManager.Preload.cs`）由 `CodeGenerator` 按字母序输出。

### 3.3 命名规范

| 对象 | 规范 | 示例 |
|---|---|---|
| 命名空间 | PascalCase | `HotUpdate` |
| 类、结构体、枚举 | PascalCase | `MainPanel` |
| 方法 | PascalCase | `PlayBtnAnim` |
| Unity 生命周期函数 | 使用 Unity 原始名称 | `Awake`、`Start`、`OnEnable` |
| 可写字段（含 public 实例、static） | `m_` + camelCase；MonoBehaviour 单例字段私有，对外用 `Instance`/`HasInstance` | `m_position`、`m_package`、`GameManager.Instance` |
| `const` / `readonly` 字段 | PascalCase，禁止 `m_`；与公共属性同名时加 `Value` 后缀；字段与类型同名不算冲突，已 `using` 即写短名 | `Key`、`ConfigExcelPath`、`ConfigNameValue`、`MemoryStream MemoryStream` |
| Inspector 绑定 public 字段 | `m_` + 类型语义前缀 + 业务名 | `m_btnPlay`、`m_tsPlay`、`m_objItem`、`m_tsTrans`、`m_tsHandle` |
| 纯数据/DTO public 字段 | PascalCase，不加 `m_` | `Id`、`GameId`；`Config_*` 行字段（Excel 列契约）、JSON Model（服务器字段契约）保持原名 |
| 序列化字段改名 | 仅当存在已落盘且需保留的序列化数据（.asset/场景/Prefab）时，加 `[FormerlySerializedAs("旧名")]`；导入期代码赋值生成的对象（如 `BinAsset`）不需要 | `[FormerlySerializedAs("oldName")] public string m_name;` |
| 局部变量、参数 | camelCase | `itemIndex`、`callBack` |
| 回调参数 | 统一 `callBack` | `Action callBack` |
| 布尔字段 | `m_is`、`m_has`、`m_can` 等语义前缀 | `m_isPlaying` |
| `catch` 异常变量 | 统一 `error` | `catch (Exception error)` |
| 事件处理方法 | `On` + 对象/行为 + 事件 | `OnPlayGameClick` |

常用 Unity/UI 字段缩写沿用工程风格：

| 前缀 | 类型/用途 | 对应节点名 | 示例 |
|---|---|---|---|
| `m_btn` | `UIButton` 或按钮 | `Btn_{业务名}` | `m_btnPlay` → `Btn_Play` |
| `m_ts` | `Transform` / `RectTransform` | `Ts_{业务名}` | `m_tsPlay` → `Ts_Play` |
| `m_text` | TextMeshPro 文本 | `Text_{业务名}` | `m_textTitle` → `Text_Title` |
| `m_img` | `Image` | `Img_{业务名}` | `m_imgIcon` → `Img_Icon` |
| `m_raw` | `RawImage` | `Raw_{业务名}` | `m_rawPreview` → `Raw_Preview` |
| `m_obj` | `GameObject` | `Obj_{业务名}` | `m_objContent` → `Obj_Content` |
| `m_sli` | `Slider` | `Sli_{业务名}` | `m_sliProgress` → `Sli_Progress` |
| `m_scr` | `ScrollRect` | `Scr_{业务名}` | `m_scrList` → `Scr_List` |

命名必须表达业务含义，不使用 `a`、`b`、`temp1`、`obj2` 等无法判断用途的名称。

### 3.4 字段声明与分组

- 字段统一声明在类的顶部、方法之前；不允许 backing field 与属性穿插；
- 字段区内按用途分组；非预制体脚本不强制 public/private 排序；预制体脚本见 §3.14（public 绑定字段在前）；
- 字段区与属性区之间保留 **1 个空行**；属性区与方法区之间保留 **3 个空行**；
- 构造函数作为独立分组，前后各保留 **3 个空行**；
- 可写私有引用类型字段显式初始化为 `= null`；Inspector 绑定的 public 字段**不写** `= null`；
- public 可写实例字段同样 `m_` + camelCase（如 `BinReader.m_position`）；DTO/JSON/`Config_*` 行字段例外见 §3.3；
- 运行期只读字段加 `readonly`，编译期常量用 `const`；二者命名均为 **PascalCase、不加 `m_`**；
- `readonly` 字段在构造函数中赋值时，声明处**不写** `= null`；
- 修饰符顺序：`public static readonly`（不用 `readonly static`）；
- 字段声明时仅设置明确且安全的默认值，不在字段初始化器中执行复杂逻辑。

### 3.5 组件引用与预制体绑定

所有能够在 Unity Inspector 中配置的组件引用，必须声明为 `public` 成员字段，并在场景或预制体上通过拖拽完成赋值。

#### 命名对应规则

脚本字段、Prefab 节点名与挂载组件必须一一对应：

| 脚本字段 | Prefab 节点名 | 挂载组件 |
|---|---|---|
| `m_textPlay` | `Text_Play` | `TextMeshProUGUI` |
| `m_btnPlay` | `Btn_Play` | `UIButton` |
| `m_tsPlay` | `Ts_Play` | `RectTransform` / `Transform` |
| `m_imgPlay` | `Img_Play` | `Image` |
| `m_rawPlay` | `Raw_Play` | `RawImage` |

通用换算：

- 脚本字段 = `m_` + 小写语义前缀 + PascalCase 业务名；
- Prefab 节点名 = 语义前缀首字母大写 + `_` + 同一业务名；
- 节点必须挂载与字段类型匹配的组件，再绑定到脚本字段。

其余类型同理，例如：`m_objContent` ↔ `Obj_Content` ↔ `GameObject`；`m_sliProgress` ↔ `Sli_Progress` ↔ `Slider`；`m_scrList` ↔ `Scr_List` ↔ `ScrollRect`。

例外（适用于本节全部命名对应要求，含下文「必须遵守」清单）：同一节点可同时被 Inspector 事件绑定（如 UIButton 的 UnityEvent 点击）与代码字段引用（如该节点的 RectTransform）；节点名只需与其中一种引用方式满足命名对应即可（如 `m_tsTest` 引用 `Btn_Test1` 节点的 RectTransform 做动画）。

推荐写法：

```csharp
public UIButton m_btnPlay;
public RectTransform m_tsPlay;
public TextMeshProUGUI m_textTitle;
```

不推荐在运行时查找已经固定存在于预制体中的组件：

```csharp
// 不推荐：固定组件不应在运行时查找
private void Awake()
{
    m_btnPlay = transform.Find("Btn_Play").GetComponent<UIButton>();
    m_tsPlay = GameObject.Find("UI_Root/Canvas_0/Ts_Panel").GetComponent<RectTransform>();
}
```

必须遵守以下规则：

- UI 控件、动画节点、文本、图片、列表、按钮及其他固定组件引用，统一使用 `public` 字段；
- `public` 组件字段必须使用 `m_` 前缀和对应的类型语义前缀；
- Prefab 节点名必须与字段语义前缀及业务名对应，并挂载匹配组件；
- 字段必须在对应场景或 Prefab 的 Inspector 中拖拽赋值；
- 新增或修改字段后，必须打开对应场景或 Prefab 检查引用是否完整；
- 不允许为了减少 Inspector 字段而使用 `GameObject.Find`、`Transform.Find` 或 `GetComponent` 查找固定组件；
- 不允许依赖节点名称和层级路径获取本来可以直接绑定的对象；
- 不允许在 `Update`、循环或高频方法中执行任何 `Find` 查找；
- Prefab 变更时应同时提交脚本和对应 Prefab，避免代码字段与资源绑定不一致。

只有以下少数情况可以使用运行时查找：

1. 对象由代码动态实例化，编辑阶段无法拖拽绑定；
2. 目标对象来自运行时加载的场景或 Prefab，编译时不存在引用关系；
3. 框架级全局根节点需要跨场景定位，例如 `UI_Root`；
4. 第三方 SDK 或框架 API 只能通过名称、路径或类型获取对象；
5. 为兼容已有资源临时补偿缺失引用，并且需求明确要求兼容；
6. 例外：`UIPopup.OnEnable` 对同物体 `CanvasGroup` 使用 `GetComponent`（`m_tsTrans` 所在物体必须同时挂 `CanvasGroup`）。

确需运行时查找时，必须同时满足：

- 在代码旁写注释说明不能拖拽绑定的原因；
- 尽量只查找一次，并将结果缓存到成员字段；
- 使用前检查查找结果是否为 `null`；
- 查找失败时输出包含节点名称或路径的明确错误日志；
- 不使用容易变化的深层路径；
- 后续具备 Inspector 绑定条件时，应改回 `public` 字段拖拽赋值。

允许的动态对象示例：

```csharp
public RectTransform m_tsItemParent;
public GameObject m_itemPrefab;

private RectTransform CreateItem()
{
    GameObject item = GameObject.Instantiate(m_itemPrefab, m_tsItemParent);
    return item.GetComponent<RectTransform>(); // 动态实例对象，无法提前拖拽其组件
}
```

即使对象是动态创建的，其**模板 Prefab**和**父节点**仍然必须优先通过 `public` 字段拖拽绑定。

### 3.6 方法排列顺序

类中的方法按以下顺序排列：

1. Unity 生命周期函数（按执行顺序）：`Awake` → `OnEnable` → `Start` → `Update` → `OnDisable` → `OnDestroy`（未列出的 Unity 消息按官方执行序插入）；
2. Unity 事件接口方法（如 `OnPointerClick`，紧跟生命周期之后）；
3. 公共业务方法；
4. 私有业务方法；
5. 自定义按钮/事件/异步回调方法。

同一组内按照实际调用流程排列。入口方法调用的业务方法应尽量放在其后方。

空行规则：

- **同组同级别方法之间保留 1 个空行**（生命周期组内、业务方法组内、菜单函数组内、私有辅助方法组内均适用）；
- 连续的一行式成员（如一组 `=>` 方法）之间可紧凑不留空行；
- **3 个空行仅用于大组边界**：
  - `using` 区 → `namespace`；
  - 字段/属性区 → 方法区；
  - 构造函数前后；
  - 生命周期/Unity 事件接口组 → 业务方法组；
  - 入口方法组（菜单函数/`[ContextMenu]` 等）→ 私有辅助方法组；
- 多行方法体的 `return` 前空 **1 行**；方法体内逻辑块之间允许 1 空行，不出现连续空行；
- **文件末尾不保留空行**：以最后一个 `}` 结束，不写末尾换行符；生成文件同样以 `}` 结束、无尾部换行。

### 3.7 缩进、花括号与空格

- 使用 **4 个空格**缩进，不使用 Tab；
- 命名空间、类、方法、条件和循环的左花括号独占下一行（Allman）；
- `if` / `for` / `foreach` / `while` / `using` / `switch` **一律带花括号并换行**，即使只有一行；
- 空方法用单行空括号：`private StateMachine() { }`；
- `switch` 的 `case` 之间空 1 行；`default` 不强制；`case` 内不加额外花括号（需局部作用域时例外）；
- 简单 get 属性多行展开，不允许 `get { return xxx; }` 单行；
- 方法参数列表保持一行，不换行；短泛型约束同行：`where T : new()`；
- 二元运算符、赋值符号和逗号后保留一个空格；方法调用的左括号前不加空格；
- `if`、`for`、`while`、`switch` 等关键字与左括号之间保留一个空格；
- 不在行尾保留多余空格；连续空行数量按照本章分组规则执行。

### 3.8 注释规范

- public/业务入口方法使用中文 XML `<summary>`，说明“做什么”；
- 私有辅助方法自解释时可省略 XML 注释；
- Unity 生命周期与 Unity 事件接口方法名称自解释时，可不写 XML 注释；
- 类与 public 成员注释可选；
- 补充 `<param>`、`<returns>` 时**必须填写内容**，不允许空标签；
- `//` 后空一格；行尾注释与代码之间空一格：`代码; // 注释`；
- 行尾注释仅用于解释当前语句中不直观的目的；
- 注释不使用全角句号，句尾不加标点，句中用 `，`；
- 注释与代码保持同步；不保留被注释掉的无用代码。

示例：

```csharp
/// <summary>
/// 播放开始游戏按钮的动画
/// </summary>
private void PlayBtnAnim()
{
    // ...
}
```

允许的简短行尾注释：

```csharp
GameManager.Instance.InvokeEventCallBack<object>(InvariableConst.Event_Launcher_StartGame, null); // 销毁热更新面板
```

### 3.9 方法体与职责

- 一个方法只承担一个明确职责；
- 生命周期函数只负责组织调用，不堆积大段业务实现；
- 可独立描述的逻辑提取为私有方法；
- 按钮点击方法使用 `OnXxxClick` 命名；
- 方法较短时不为了形式继续拆分；
- 避免超过 3 层的深层嵌套，优先使用提前返回；
- 对外部输入、资源加载结果和可能为空的对象进行必要校验；
- 注册的事件、计时器和 SDK 回调应在对应生命周期中解除；按钮监听：Inspector UnityEvent 持久化绑定和代码 `AddClickListener` 等等五个注册的监听都会在GameObject销毁的时候自动失效所以无需代码清理。

`MainPanel` 的生命周期组织方式是推荐写法（生命周期只组织调用）。MainPanel 按钮监听走 Prefab 上 `UIButton` 的 UnityEvent 字段绑定；代码 `AddClickListener` 方式仍合法（见 §5.1 模板）：

```csharp
private void OnEnable()
{
    ShowAuthButton();
}

private void Start()
{
    PlayBGM();
    PlayBtnAnim();
}

private void OnDisable()
{
    HideAuthButton();
}
```

### 3.10 链式调用与 Lambda

- 简短且语义连续的链式调用可以写在同一行；
- `=>` 表达式成员仅限简单一行属性/方法；复杂逻辑改回传统方法体；
- Lambda 参数一律带括号：`(operation) =>`、`() =>`；
- Lambda 花括号换行：`=>` 后 `{` 独占一行；
- Lambda 内逻辑超过少量语句时，提取为命名方法；
- 不在复杂 Lambda 中混合资源加载、状态修改和 UI 刷新等多个职责。

示例：

```csharp
m_tsPlay.DOAnchorPos(Vector2.zero, 1f).SetEase(Ease.InSine).OnComplete(() =>
{
    m_tsPlay.DOAnchorPos(new Vector2(0, -500), 1f).SetEase(Ease.OutSine);
});
```

### 3.11 字面量与类型使用

- `float` 字面量使用 `f` 后缀，例如 `1f`、`0.2f`；
- 空字符串统一 `""`，不用 `string.Empty`；
- 含多个变量的字符串优先插值 `$""`；单变量前/后缀的简单拼接可保留 `+`；
- 优先使用明确类型；`var` 仅当右侧类型明显时使用（`new`、`as`、强制转换）；
- 不使用目标类型推断 `new()`，显式写类型；
- 短集合初始化可单行，长初始化每元素一行；
- `#region` 允许保留；
- 字符串事件名、资源地址等应沿用项目格式；
- 同一业务字符串重复使用时，应提取为常量；
- 不直接在业务代码中写入密码、正式广告位 ID 或其他敏感配置；
- 文档只规定书写格式；没有实际用途的字段、生命周期方法和空方法不得保留。

### 3.12 错误处理

- 项目代码（`Assets/Scripts`、`Assets/Editor`）禁止 `throw new`；统一 `GameLog.Error(具体信息)` + 安全返回（fail-soft：返回 null/默认值/空数组，调用方判空）；
- 第三方库 `Assets/ToolPackage` 不改、不受此约束。

### 3.13 日志输出

- `Invariable` / `HotUpdate` / `MyTools` 禁止直接调用 `UnityEngine.Debug.Log` / `LogWarning` / `LogError` 输出业务日志；
- 必须使用 `GameLog.Info`（仅编辑器环境输出）或 `GameLog.Error`（始终输出）；
- 映射：`Debug.Log` / `Debug.LogWarning` → `GameLog.Info`；`Debug.LogError` → `GameLog.Error`；
- 排除 `CloudService`：云函数约束要求只能用 `UnityEngine.Debug`，且 `GameLog` 位于 `Invariable`，引用它会与 `Invariable` → `CloudService` 的依赖方向形成循环引用；
- `GameLog` 命名空间为 `Invariable`：同程序集直接用；跨程序集（`MyTools`）需 `using Invariable;`；
- 例外：`GameLog.cs` 自身实现；第三方库 `ToolPackage` 不在此约束。

### 3.14 预制体挂载脚本附加规范

适用于挂在 Prefab/场景上的脚本（如 `Invariable/Component/*`、`Launcher`、`GameLoadingPanel`、`HotUpdate/UI/*`）。运行时 `AddComponent` 创建的 Manager（如 `GameManager`、`AudioManager`）按非预制体规则。

- 所有预制体节点引用：分类型、对应具体节点、全部声明为 `public` 字段，并在 Inspector 拖拽赋值（细则见 §3.5）；
- 字段区 **public 绑定字段在前**，private 状态字段在后；
- Inspector 绑定字段**不补** `= null`；
- 生命周期函数必须按执行顺序排列；生命周期作为一组：组前/组后 **3 空行**，组内 **1 空行**；
- Unity 事件接口方法紧跟生命周期之后，注释可选；
- 事件注册/注销对称：`OnEnable` 注册 ↔ `OnDisable` 注销；`OnDestroy` 释放监听与资源；
- 不用 `Find`/`GetComponent` 查找固定组件（例外见 §3.5）。

### 3.15 代码提交前检查

- [ ] `using` 字母序、无冗余，且与 `namespace` 之间保留 3 个空行；已 `using` 无冗余完全限定名；
- [ ] 成员顺序：嵌套类型 → 字段 → 属性 → 构造 → 生命周期 → Unity 事件接口 → 业务 → 回调；
- [ ] 可写实例字段（含 public）`m_`；`const`/`readonly` PascalCase 无 `m_`；序列化改名且存在落盘数据时带 `FormerlySerializedAs`；回调参数 `callBack`；`catch` 变量 `error`；
- [ ] 可写私有引用字段显式 `= null`；Inspector 绑定字段无 `= null`；
- [ ] 无 `throw new`（统一 `GameLog.Error` + 安全返回）；
- [ ] 业务日志走 `GameLog.Info` / `GameLog.Error`；未直接使用 `UnityEngine.Debug`（`GameLog.cs` 与 `CloudService` 除外）；
- [ ] 方法参数列表保持一行，不换行；
- [ ] `if`/`for`/`switch` 等一律花括号；`return` 前空行；`//` 前后空格；
- [ ] Lambda 参数带括号且花括号换行；空字符串 `""`；`public static readonly` 顺序；
- [ ] 同组同级别方法之间 1 空行，3 空行仅用于大组边界；文件末尾无空行（不以换行符结束）；
- [ ] 固定组件引用均为 `public` 并已拖拽赋值（预制体脚本）；
- [ ] 生命周期按执行序；预制体脚本 public 绑定字段在前；
- [ ] 事件、计时器和 SDK 回调已对称清理；按钮监听：Inspector UnityEvent 持久化绑定和代码 `AddClickListener` 等等五个注册的监听都会在GameObject销毁的时候自动失效所以无需代码清理；
- [ ] 没有无用注释代码、调试残留或敏感信息。

## 4. 新增普通热更新业务脚本

示例目录：

```text
Assets/Scripts/HotUpdate/Gameplay/ExampleFeature.cs
```

示例：

```csharp
using Invariable;

namespace HotUpdate
{
    public class ExampleFeature
    {
        public void Start()
        {
            GameManager.Instance.InvokeEventCallBack<object>(HotUpdateConst.Event_ExampleFeature_Started, null);
        }
    }
}
```

注意：

1. `HotUpdate` 可以 `using Invariable`；asmdef 对项目内程序集统一写名称引用，第三方包用 GUID；
2. 不要让 `Invariable` 直接引用此类型；
3. `HotUpdate` 可消费 `CloudService` 的 Model DTO（如 `PlayerCloudData`），仅限数据模型，禁止依赖云函数内部实现；DTO 契约变更需重发基础包并同步重导热更 DLL；
4. 如需从不可热更层调用，应定义稳定接口、事件，或在唯一入口处反射；
5. 如果使用新泛型组合，真机构建前应验证 HybridCLR AOT 泛型支持。
6. 事件 key 必须先在 `HotUpdateConst` 的 `#region 事件` 定义为常量后再引用（定义示例见 §7.1），禁止在调用处散落字面量。

## 5. 新增 UI 页面

假设新增 `InventoryPanel`。

### 5.1 创建脚本

位置：

```text
Assets/Scripts/HotUpdate/UI/InventoryPanel/InventoryPanel.cs
```

示例：

```csharp
using Invariable;
using UnityEngine;



namespace HotUpdate
{
    public class InventoryPanel : UIPanel
    {
        public UIButton m_btnClose;



        private void Start()
        {
            m_btnClose.AddClickListener(OnCloseClick);
        }



        private void OnCloseClick()
        {
            Close();
        }
    }
}
```

### 5.2 创建 Prefab

位置建议：

```text
Assets/GameAssets/Prefabs/UI/InventoryPanel/InventoryPanel.prefab
```

要求：

- Prefab 文件名必须为 `InventoryPanel`；
- 脚本类名必须为 `InventoryPanel`；
- 命名空间必须为 `HotUpdate`；
- 类必须继承 `UIPanel`；
- Inspector 字段必须完整绑定；
- 若需要弹窗动画，根对象附加 `UIPopup` 并绑定 `m_tsTrans`（对应节点 `Ts_Trans`）；`m_tsTrans` 所在物体须同时挂 `CanvasGroup`；
- Prefab 必须位于 YooAsset `Prefabs` 收集目录下。

### 5.3 打开页面

```csharp
Utils.OpenUIPrefabPanel(
    "InventoryPanel",
    0,
    panelObject =>
    {
        var panel = panelObject.GetComponent<InventoryPanel>();
        // 初始化参数
    }
);
```

### 5.4 关闭页面

页面内部：

```csharp
Close();
```

外部按名称：

```csharp
UIManager.Instance.CloseUIPanel("InventoryPanel");
```

### 5.5 UI 页面检查清单

- [ ] 脚本名、类名、Prefab 名一致；
- [ ] 使用 `Utils.OpenUIPrefabPanel` 打开页面；Tips/FloatText 可用 `HotUpdateUtils` 业务封装；
- [ ] layer 对应节点存在；
- [ ] Inspector 引用完整，所有固定组件均通过 `public` 字段拖拽赋值；
- [ ] 没有使用 `Find` 查找本可直接绑定的 UI 组件；
- [ ] 页面关闭后事件和计时器已取消；
- [ ] 异步资源回调时页面可能已销毁，已处理空引用；
- [ ] 快速重复点击不会实例化重复页面；
- [ ] Prefab 已进入 YooAsset 构建结果；
- [ ] 真机 HybridCLR 能找到脚本类型。

## 6. 新增弹窗或顶层提示

### 6.1 标准提示弹窗

```csharp
HotUpdateUtils.OpenTipsPanel(
    content: "是否继续？",
    btn1: "确定",
    callBack1: OnConfirm,
    btn2: "取消",
    callBack2: OnCancel,
    title: "提示"
);
```

只有一个按钮时将 `btn2` 留空。

### 6.2 飘字

```csharp
HotUpdateUtils.ShowFloatText("操作成功");
```

`FloatTextPanel` 重复打开经 UIManager 单例去重激活，内部 item 对象池复用，播完自动隐藏；不在池化名单（池化仅 TipsPanel），主动 CloseUIPanel 时 Destroy。

## 7. 使用事件系统

### 7.1 注册与移除

推荐生命周期对称：

常量定义在 `HotUpdateConst` 的 `#region 事件`：

```csharp
// HotUpdateConst.cs
#region 事件
public const string Event_Inventory_DataChanged = "Inventory_DataChanged";
public const string Event_ExampleFeature_Started = "ExampleFeature_Started";
#endregion
```

```csharp
private void OnEnable()
{
    GameManager.Instance.AddEventListener<int>(
        HotUpdateConst.Event_Inventory_DataChanged,
        OnDataChanged
    );
}

private void OnDisable()
{
    GameManager.Instance.RemoveEventListener<int>(
        HotUpdateConst.Event_Inventory_DataChanged,
        OnDataChanged
    );
}

private void OnDataChanged(int itemId)
{
    // 使用强类型参数
}
```

触发：

```csharp
GameManager.Instance.InvokeEventCallBack(
    HotUpdateConst.Event_Inventory_DataChanged,
    itemId
);
```

无参纯通知：

```csharp
GameManager.Instance.InvokeEventCallBack<object>(HotUpdateConst.Event_ExampleFeature_Started, null);
```

### 7.2 使用注意

- 事件 API 仅有泛型：`AddEventListener<T>` / `RemoveEventListener<T>` / `InvokeEventCallBack<T>`；
- 参数类型不匹配会在运行时失败；
- 不要用含义相近但拼写不同的事件名；
- 不要在匿名 lambda 注册后尝试用另一个 lambda 移除；
- 页面销毁前必须移除监听；
- 回调中避免直接增删同一个事件的监听集合；
- 高频数据同步不宜全部经过事件总线，可考虑明确接口。

事件与延迟调用 key 集中在常量类（用 `#region` 分区管理）：

```text
Assets/Scripts/Invariable/Utils/InvariableConst.cs   # 跨层契约（事件/计时器/UI 路径/AOT 列表/广告与分享/音频本地 key 等）
Assets/Scripts/HotUpdate/Utils/HotUpdateConst.cs     # HotUpdate 业务 key（业务计时器前缀、对象池 key 等）
```

禁止在调用处散落魔法字符串。

## 8. 使用计时器

### 8.1 一次性延迟

常量定义在 `HotUpdateConst` 的 `#region 计时器`：

```csharp
GameManager.Instance.DelayCallSeconds(
    HotUpdateConst.Timer_InventoryPanel_RefreshDelay,
    RefreshView,
    0.5f
);
```

清理：

```csharp
private void OnDisable()
{
    GameManager.Instance.CancelInvokeByKey(HotUpdateConst.Timer_InventoryPanel_RefreshDelay);
}
```

### 8.2 重复调用

```csharp
GameManager.Instance.RepeatingCallSeconds(
    HotUpdateConst.Timer_Battle_Countdown,
    TickCountdown,
    1f
);
```

停止：

```csharp
GameManager.Instance.CancelInvokeByKey(HotUpdateConst.Timer_Battle_Countdown);
```

### 8.3 必须注意的行为

- 相同 key 已存在时不会启动新计时；
- 一次性延迟完成后会移除对应 key；
- 页面禁用/销毁必须主动调用取消；
- 延迟与循环计时均由 `Update` 驱动秒/帧双最小堆；
- `DelayCallSeconds` 与 `RepeatingCallSeconds` 均受 `Time.timeScale` 影响；
- `CancelInvokeByKey` 仅当 key 存在时输出 `GameLog.Info(key + "取消调用")`，key 不存在直接返回；
- 循环调用支持 `immediately`（默认 true）：注册后是否立即执行一次；
- 不要把短生命周期对象直接永久捕获在回调中。

## 9. 加载资源

### 9.1 挂载资源

Animation、Atlas、Audios、Png 四个分组为 Depend 模式，不按名加载。资源通过 Prefab 挂载引用进包：

- 单张图片：`public Sprite m_iconSprite;` → `m_imgIcon.sprite = m_iconSprite;`
- 图集：`public SpriteAtlas m_uiAtlas;` → `m_imgIcon.sprite = m_uiAtlas.GetSprite("icon_score");`
- 音频：`public AudioClip m_audioClip;` → `AudioManager.Instance.PlaySFX(m_audioClip);`
- 动画：挂载到 Animation 组件，代码 `animation.Play("animName")`

### 9.2 设置图片

图集一律在脚本声明 `public SpriteAtlas` 字段并在 Inspector 挂载，按名取图后直接赋值（随 Prefab 依赖加载，零异步）。禁止按地址异步加载图集。同一图集内图片仍合批为一个 DrawCall。

```csharp
public SpriteAtlas m_uiAtlas;

m_imgIcon.sprite = m_uiAtlas.GetSprite("icon_score");
```

远程 URL 走 `Utils.SetRemoteImage`，无法挂载。单张图片挂载 `public Sprite` 字段，图集挂载 `public SpriteAtlas` 字段。

### 9.3 设置灰度

```csharp
Utils.SetGray(gameObject, "Icon", true);
Utils.SetGray(gameObject, "Icon", false);
```

### 9.4 场景

```csharp
YooAssetManager.Instance.AsyncLoadScene(
    "Scenes_Battle",
    UnityEngine.SceneManagement.LoadSceneMode.Additive,
    scene =>
    {
        // 场景加载完成
    }
);
```

卸载：

```csharp
YooAssetManager.Instance.UnLoadScene("Scenes_Battle");
```

注意：`UnLoadScene` 仅释放对应场景句柄，不连带释放普通资源，支持可选回调（成功失败均触发）。`Single` 模式加载会先卸载其他已缓存场景，完成后调用 `PoolUtils.ClearAllGameObjectPools()` 清空全部 GameObject 池（池中对象随之销毁，业务需自行重建）。按地址释放资源使用 `ReleaseAsset`；批量卸载未使用资源使用 `UnloadUnusedAssets`。

### 9.5 资源加载检查清单

- [ ] 地址与 Collector 地址规则一致；Depend 挂载资源（Animation / Atlas / Audios / Png）无独立地址，不走此项；
- [ ] 文件位于 `Assets/GameAssets` 对应目录；
- [ ] 加载失败路径有日志或用户提示；
- [ ] 回调触发时宿主对象仍存活；
- [ ] 不依赖未定义的回调顺序；
- [ ] 场景卸载不会意外释放仍使用的资源；
- [ ] 发布前重新构建并上传 YooAsset 清单和 Bundle。

## 10. 使用音频

### 10.1 Inspector 挂载

音频一律在脚本声明 `public AudioClip` 字段并在 Inspector 挂载，经 `AudioManager` 播放（随 Prefab 依赖加载，零异步）。禁止按名加载。

```csharp
AudioManager.Instance.PlaySFX(m_audioClip);
AudioManager.Instance.PlayBGM(m_audioBgm);
```

`PlaySFX(clip)` 写死单次，每 clip 独立通道，同 clip 打断重播，不同 clip 叠加；通道上限 30，超出回收最久未用空闲通道，全忙打错误日志并跳过。`PlayBGM(clip)` 写死循环，单通道，切歌自动停上一首。音量/静音走 `SetMasterVolume` / `SetBGMVolume` / `SetSFXVolume` / `SetMute`。

UIButton 的 `m_audioClip` 即此路径：单击触发时自动 `PlaySFX(m_audioClip)`。`m_audioClip` 留空时 Inspector 自动挂载默认点击音效（`Audios/Sfx/defaultBtn.mp3`），手动修改后不自动覆盖。

### 10.2 停止、暂停与恢复

```csharp
AudioManager.Instance.StopBGM();
AudioManager.Instance.PauseBGM();
AudioManager.Instance.ResumeBGM();
AudioManager.Instance.StopSFX(m_audioClip);
AudioManager.Instance.PauseSFX(m_audioClip);
AudioManager.Instance.ResumeSFX(m_audioClip);
AudioManager.Instance.StopAllAudio();
AudioManager.Instance.PauseAllAudio();
AudioManager.Instance.ResumeAllAudio();
```

`StopBGM` / `PauseBGM` / `ResumeBGM` 控制当前 BGM。`StopSFX(clip)` / `PauseSFX(clip)` / `ResumeSFX(clip)` 按 clip 控制音效。`StopAllAudio` / `PauseAllAudio` / `ResumeAllAudio` 控制全部。

### 10.3 文件位置与导入

```text
Assets/GameAssets/Audios/Bgm/bgm.*   # 背景音乐
Assets/GameAssets/Audios/Sfx/xxx.*   # 音效
```

子目录是导入设置依据（`VastStarryRiver/资源处理/设置音频资源`：Bgm 用 CompressedInMemory，Sfx 用 DecompressOnLoad 且强制单声道）。音量设置经 `SdkManager` 本地存储持久化。

## 11. 新增或修改 Excel 配置

### 11.1 Excel 源表格式

源表位于项目根目录 `Excel/`，仅支持 `.xlsx` / `.xls`。

- `.xlsx` / `.xls` 只读第一个 sheet；
- 文件名须为合法 C# 标识符。

表头固定 3 行，第 4 行起为数据（全表至少 4 行）：

| 行 | 含义 |
|---:|---|
| 1 | 字段名；首列强制视为 `Id` |
| 2 | 类型：`int` / `float` / `string`（不区分大小写） |
| 3 | 注释（可空） |
| 4+ | 数据行 |

约束：

- `Id` 必须是标量 `int`，不能是数组；
- 字段按 baseName 字典序、同前缀按数字后缀排序后写入 bytes 与生成代码，`Id` 强制首位；布局顺序不等于 Excel 列序；
- 定长数组：列名使用 `字段名+序号`（如 `Reward1`/`Reward2`/`Reward3`），同前缀连续列且类型一致，生成 `字段名` 数组；
- 空单元格：`int`/`float` 按 0，`string` 按空串。

支持类型：

```text
int, float, string,
定长 int[] / float[] / string[]
```

标量示例：

| Id | StartLv | Speed | Name |
|---|---|---|---|
| int | int | float | string |
| 编号 | 开启等级 | 速度 | 名称 |
| 1 | 10 | 3.5 | 新手 |

定长数组示例：

| Id | Reward1 | Reward2 | Pos1 | Pos2 | Tag1 | Tag2 |
|---|---|---|---|---|---|---|
| int | int | int | float | float | string | string |
| 编号 | 奖励1 | 奖励2 | X | Y | 标签1 | 标签2 |
| 1 | 100 | 200 | 1.5 | 2.5 | 近战 | 物理 |

生成字段：`int[] Reward`、`float[] Pos`、`string[] Tag`。

### 11.2 修改数据

1. 按 §11.1 修改 `Excel` 目录中的源表；
2. 不要直接修改 `HotUpdate/Config/Generated/Config_*.cs` 或 `GameAssets/Config/*.bytes`；
3. 执行菜单：`VastStarryRiver/Config/导出Excel配置`；导表同时产出 `GameAssets/Config/{表}.bytes`（YooAsset Config 组，地址 `Config_{表名}`，可热更）与 `Generated/Config_{表}.cs`；
4. 可选：`VastStarryRiver/Config/校验配置数据`；
5. **纯数值改动**：只需再构建 AssetBundle（bytes 已在 YooAsset，无需重导 DLL）；
6. **表结构变更**：等待脚本重新编译后，按完整 HybridCLR + YooAsset 流水线导出。

### 11.3 使用配置

每表由生成代码提供 `Get{表}ByID` / `Get{表}ByIDs` / `Get{表}` / `GetAll{表}` / `Clear{表}`；`PreloadAll` / `ClearAll` 由 `ConfigManager.Preload.cs` 提供：

```csharp
ConfigManager.GetPlayerByID(1, row =>
{
    if (row != null)
    {
        int level = row.StartLv;
    }
});

ConfigManager.GetPlayerByIDs(new[] { 1, 2 }, list =>
{
    // 任一 id 无效则整体回调 null 并 GameLog.Error
});

ConfigManager.GetRoleRune(dic =>
{
    if (dic == null) return;
    foreach (var id in dic.Keys)
    {
        if (dic.TryGetValue(id, out var rune))
        {
            // 按需惰性反序列化
        }
    }
});

ConfigManager.GetAllRoleRune(
    list =>
    {
        // 完成才交付完整 IReadOnlyList；小表同帧完成，>500 行分帧物化
    },
    (loaded, total) =>
    {
        // 可选进度回调
    });

ConfigManager.ClearRoleRune(); // 单表清理
ConfigManager.PreloadAll(() => { });
ConfigManager.ClearAll();
```

每表首次访问异步加载 bytes（YooAsset 地址 `Config_{表名}`，`TextAsset`）；行对象按 ID 惰性创建。可选 `ConfigManager.PreloadAll` 预热，`ConfigManager.ClearAll` 清理全部表缓存。

补充约定：

- bytes 头部含 magic(CFGT)+schemaHash；加载时与生成代码中的 `SchemaHash` 校验，不匹配即报错，需重新导表；
- 闲置超过 180s 会逐出解析层（Reader/行对象/字符串缓存），YooAsset handle 与 bytes 常驻，再次访问同帧秒回；
- 不要长期缓存 `DictionaryForConfig`：逐出后字典失效，继续访问会 `GameLog.Error`，应重新走 `ConfigManager.Get{表}`；
- `PreloadAll` 单表加载失败会 `GameLog.Error` 提示具体表名并继续，不阻塞整体完成回调。

### 11.4 修改导表规则

如果需求是支持新字段类型或新代码结构，应修改：

```text
Assets/Editor/MyTools/Config/
```

这是 Editor 工具修改，不属于热更新业务脚本。修改后应使用小型测试表验证：

- 空值；
- 中文 string；
- 定长数组（Name1/Name2）；
- 重复 Id；
- 非法文件名；
- int / int[] / float / float[] / string / string[]。

## 12. 使用平台存储

### 12.1 本地存储

统一接口：

```csharp
SdkManager.Instance.SetLocalData("PlayerName", name);

string name = SdkManager.Instance.GetLocalData(
    "PlayerName",
    "Default"
);
```

对应实现：

| 环境 | 实现 |
|---|---|
| Editor | `PlayerPrefs` |
| 微信 | `WX.Storage*Sync` |
| 抖音 | `TT.Save` / `TT.LoadSaving` |

### 12.2 云存档读写

业务侧统一走 `SdkManager`（与本地存储同属数据存储模块）：

```csharp
SdkManager.Instance.SetCloudData("Score", "100");

string score = SdkManager.Instance.GetCloudData("Score", "0");
```

对应实现：

| 环境 | 实现 |
|---|---|
| Editor | 转发 `SetLocalData` / `GetLocalData` |
| 微信/抖音 | 转发 `CloudManager` 云缓存；写后异步上传 |

云初始化失败后 Set 静默丢弃、Get 返回默认值。排行榜上报：`CloudManager.Instance.ReportRankScore(rankKey, score)`。排行榜拉取：`CloudManager.Instance.GetRankList(rankKey, rankType, callBack)`，`rankType` 必填，世界榜传 `CloudRankTypes.World`，日榜传 `CloudRankTypes.Day`。编辑器桩：`GetRankList` 回空列表、`ReportRankScore` 回 true。资料键用 `CloudDataKeys.UserId` / `NickName` / `AvatarUrl`。昵称直接赋 `TextMeshProUGUI.text`；头像 URL 用 `Utils.SetRemoteImage`，不要用挂载图集。云存档与排行榜契约见 FrameworkAndProcess §16。云函数/密钥约束见 NewProjectSetup §7。

存档修改应考虑：

- key 不要随意改名；
- 新字段提供默认值；
- JSON 数据要做版本兼容；
- 不要把服务端可信数据只存本地；
- 大数据量不宜继续只使用字符串接口。

## 13. 新增平台能力

推荐做法：

1. 先确认 `SdkManager` 是否已有现成能力（如分享 `Share(string desc)`、环境判断 `IsWeChat()/IsDouYin()`、云读写 `SetCloudData/GetCloudData`、用户信息 `SyncPlatformUserInfo(authAnchor, authStateCallBack, userInfoCallBack)` / `TryGetPlatformUserInfo` / `RequestPlatformUserInfoAuth(authAnchor, authStateCallBack, userInfoCallBack)` / `DestroyPlatformUserInfoButton`，`authStateCallBack` 上报 `PlatformAuthStates`（`Invariable` 顶层枚举）、`userInfoCallBack` 仅资料结果，授权按钮显隐由业务层负责，`MainPanel` 仅 `NotAuthorized` 显示），避免重复实现；云存档/云函数与世界榜/日榜见 `CloudManager`、FrameworkAndProcess §16 与 NewProjectSetup §7；
2. 在 `SdkManager` 添加平台无关的公共方法；
3. 方法内部使用平台宏分支；
4. Editor 分支提供可预测的模拟结果；
5. 热更新业务只调用公共方法；
6. 真机分别验证微信和抖音；
7. 明确回调在成功、失败、取消时是否都能结束。

示例结构：

```csharp
public void DoPlatformAction(Action<bool> callBack)
{
#if UNITY_EDITOR
    callBack?.Invoke(true);

#elif MINIGAME_SUBPLATFORM_WEIXIN
    // 微信实现

#elif MINIGAME_SUBPLATFORM_DOUYIN
    // 抖音实现
#endif
}
```

注意：修改 `SdkManager` 属于不可热更修改。

## 14. 修改启动或热更新流程

若需要新增状态节点：

1. 在 `Invariable/Workflow` 新增实现 `IStateNode` 的类；
2. 在 `Launcher.Start` 调用 `stateMachine.AddNode<T>()`；
3. 在前置状态成功后调用 `ChangeState<T>()`；
4. 为失败、重试、取消设计明确状态；
5. 如需跨节点传参，使用黑板；
6. 确保异步回调不会在状态退出后错误切换；
7. 确保加载面板事件仍在 Launcher 销毁前有效。

状态机不会自动调用 `Update()`；启动节点都以协程/回调驱动，因此没有依赖 `OnUpdate()`。若新节点需要每帧更新，还必须让宿主持有状态机并在 Unity `Update` 中调用它。

## 15. Prefab 和脚本绑定注意事项

- 热更新 UI 可以通过 Prefab 序列化脚本或运行时按类名补加组件；
- 运行时补加依赖“Prefab 名 = 类名”；
- 字段序列化变化可能导致已有 Prefab 丢引用；
- 重命名脚本或移动命名空间时要检查 Prefab；
- 修改字段类型后要重新打开 Prefab 验证；
- 不要只改脚本而忽略对应 `.prefab`；
- 不要手工编辑 Unity YAML（资源文件文本内容），除非确有必要并能验证引用。

## 16. BUG 修复标准检查清单

### 16.1 通用

- [ ] 已确认复现路径和根因，而非只隐藏异常；
- [ ] 已搜索所有调用方；
- [ ] 未覆盖用户未提交修改；
- [ ] 修改范围符合热更新边界；
- [ ] 对空对象、失败回调、重复点击、快速开关做了处理；
- [ ] 没有新增字符串地址拼写不一致；
- [ ] 没有留下事件、计时器和 SDK 监听；
- [ ] 日志不包含密钥、用户隐私或认证信息。

### 16.2 UI

- [ ] Prefab 绑定完整；
- [ ] 页面名、脚本名一致；
- [ ] 层级正确；
- [ ] 动画完成前关闭/重复打开安全；
- [ ] 异步资源回调时页面仍有效。

### 16.3 热更新

- [ ] `HotUpdate.asmdef` 引用足够（项目内程序集统一名称引用 `Invariable` / `CloudService`）；
- [ ] 云资料键使用 `CloudDataKeys`，未散落 `UserId` / `NickName` / `AvatarUrl` 字面量；
- [ ] 新类型可被 HybridCLR 加载；
- [ ] AOT 泛型和反射类型已验证；
- [ ] DLL 已重新生成、加密并构建到 YooAsset；
- [ ] 真机未使用过期缓存清单。

### 16.4 平台

- [ ] Editor、微信、抖音分支均有明确行为；
- [ ] 正确平台宏处于激活状态；
- [ ] SDK 初始化完成后才调用；
- [ ] 成功、失败、关闭、取消均会回调（含用户信息授权）；
- [ ] 真机生命周期切后台/回前台已验证。

## 17. 使用对象池

实现：`PoolUtils`（`Assets/Scripts/Invariable/Utils/PoolUtils.cs`），`public static class`。

类型池：

```csharp
List<Delegate> snapshot = PoolUtils.Get<List<Delegate>>();
// 使用 snapshot
PoolUtils.Release(snapshot);
```

- 上限 `DefaultMaxSize` = 30
- 池空时 `new T()`
- `IList` 归还时自动 `Clear`
- `ClearPool<T>()` 清空指定类型池，下次取出时自动重建

GameObject 池：

```csharp
GameObject go = PoolUtils.GetGameObject(
    HotUpdateConst.Pool_FloatTextItem,
    m_objItem,
    transform
);
// 使用 go
PoolUtils.ReleaseGameObject(HotUpdateConst.Pool_FloatTextItem, go);
```

- 按 key 隔离，默认单 key 上限 `DefaultGameObjectMaxSize` = 50
- `SetGameObjectPoolMaxSize(key, maxSize)` 可按 key 自定义上限（≤0 回落默认）
- 池空时按预制体实例化；取出时激活并挂到业务 parent
- 归还统一挂入 `PoolParent`（`Launcher` 启动时调用 `SetPoolParent` 注入）并隐藏
- 超出上限则销毁并输出 `GameLog.Info` 提示
- `ClearGameObjectPool(key)` 销毁指定 key 全部实例；`ClearAllGameObjectPools()` 清空全部；均保留自定义上限
- `Single` 模式场景加载完成后自动调用 `ClearAllGameObjectPools()`

池 key 必须先在 `HotUpdateConst` 的 `#region 对象池` 定义为常量后再引用，禁止在调用处散落字面量：

```csharp
// HotUpdateConst.cs
#region 对象池
public const string Pool_FloatTextItem = "FloatTextItem";
#endregion
```

示例：`FloatTextPanel`。