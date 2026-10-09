# ZRList 使用说明

安装方式见[包说明](../README.md)，更新方式见[1.0.2 升级说明](Upgrading.md)。默认安装地址跟踪 `main` 分支，需要固定版本时使用发布标签。以下示例路径均相对于 Package Manager 导入的 **ZRList Demos** 目录。

## Unity 与输入兼容性

最低支持 Unity 2021.3 LTS。示例 EventSystem 上的 `DemoInputModule` 会在首帧前选择输入模块：启用 Input System 时使用 `InputSystemUIInputModule`，否则使用 `StandaloneInputModule`。Both 模式优先使用 Input System，避免两个模块同时处理输入；未安装 Input System 时也能编译和运行。

无需更改宿主工程的 Active Input Handling。场景生成器保存相同配置，输入模块由示例自行初始化。示例中的 `link.xml` 保留可选输入模块，构建播放器时请与示例脚本一起保留。运行库仅依赖 uGUI，不强制安装 Input System。

包升级不会自动修改已导入的旧场景。请重新导入新版 Samples，或在自定义旧场景的 EventSystem 上将 `StandaloneInputModule` 替换成 `DemoInputModule`。

## 示例场景与预制体

| 场景（`Scenes/`） | 内容与交互 |
| --- | --- |
| `VerticalList.unity` | 1,000 条不同高度卡片；竖向滚动、首尾动画跳转、修改可见卡片高度 |
| `HorizontalList.unity` | 1,000 条不同宽度卡片；横向滚动、首尾动画跳转、修改可见卡片宽度 |
| `ItemDrag.unity` | 503 个物品；拖动左侧 MOVE 手柄到另一格交换，拖动其余区域滑动列表；空白处释放取消 |
| `InventoryGrid.unity` | 503 个直接挂在 Content 下的背包格子，无行节点；自动列数、点击选择、使用物品和滚动复用 |
| `CurvedScroll.unity` | 1,000 张卡片沿指定半径圆弧或自定义 AnimationCurve 移动，切线旋转、翻转、调整半径、首尾跳转 |
| `RewardReveal.unity` | 获得奖励时，24 个道具依次淡入、上弹、缩放回弹；支持重新领取播放和立即显示全部 |
| `DirectRewardReveal.unity` | 24 个道具逐个弹出；Content 下只有格子 item，无行节点，支持重播和跳过 |
| `Chat.unity` | 400 条初始聊天消息；他人在左、自己在右，输入发送、添加回复、按文字换行测量高度 |
| `VerticalNestedHorizontal.unity` | 120 个垂直分组，每组 80–108 个水平卡片；上下拖动外层，左右拖动内层 |
| `HorizontalNestedVertical.unity` | 120 个水平分组，每组 80–108 个垂直卡片；左右拖动外层，上下拖动内层 |
| `ExpandableList.unity` | 一级分组折叠、业务状态排序、全部展开／收起与隐藏子项定位 |
| `TreeList.unity` | 活动／章节／小节／任务四层折叠、状态汇总、祖先链展开定位 |
| `BusinessSortedTree.unity` | 红点／解锁／未读三个独立开关、同级业务排序与分组排序前后对照 |
| `CallbackTree.unity` | OnItemRender 回调接入三层折叠树、未读排序、视图清理和隐藏课程定位 |

`Prefabs/Showcase/` 包含可直接编辑的 `VerticalCard`、`HorizontalCard`、`InventorySlot`、`InventoryRow`，以及 `ChatIncoming`、`ChatOutgoing`、`ChatIncomingInfo`、`ChatOutgoingWarm`、`ChatIncomingAlert` 等真实 prefab。聊天通过数组配置任意数量的模板，场景默认展示五种样式；背包 `InventoryGrid` 和逐个弹出的 `DirectRewardReveal` 都直接在 Content 下复用格子；`InventoryRow` 供原有行网格示例使用。按钮、输入框、EventSystem 和所有组件引用都已保存到场景。

嵌套示例另提供 `NestedVerticalGroup`、`NestedHorizontalGroup`、`NestedHorizontalItem`、`NestedVerticalItem` 四个真实 prefab。每个分组缓存普通 C# 视图对象，绑定独立的内层 `VirtualScrollView`；回收时保存内层首个可见索引、清空内层数据并停止动画和惯性，再绑定时恢复该组的位置。

按住内层时会停止祖先列表的惯性。`VirtualScrollRect` 按开始拖拽时的主方向选择内层或最近的可用父层，并将父层手势的 `pointerDrag` 交给父对象，使发起手势的分组回收后仍能继续拖动。滚轮也按主轴分配：垂直滚轮操作垂直层，水平滚轮操作水平层；没有对应父层时保留普通单列表的滚轮行为。嵌套的两个方向分别锁定，内层 Viewport 使用 `RectMask2D` 裁剪。

Unity 菜单 **Tools → ZRList → Rebuild Showcase Assets** 可以重新生成十四个场景及其预制体，并添加到 Build Settings；**Rebuild Nested Assets** 只生成两个嵌套场景与四个嵌套 prefab。**Rebuild Item Drag Assets** 只生成物品拖拽场景，**Rebuild Reward Reveal Assets** 只生成原奖励弹出场景及 `RewardSlot` prefab；**Rebuild Direct Reward Reveal Assets** 单独生成无行节点的逐个弹出场景。四个折叠场景分别对应 **Rebuild Expandable List Assets**、**Rebuild Tree List Assets**、**Rebuild Business Sorted Tree Assets** 和 **Rebuild Callback Tree Assets**。生成前会提示保存当前修改过的场景。

`DirectRewardReveal.unity` 是独立的无行节点道具弹出示例：进入 Play Mode 后，24 个格子按 0.12 秒间隔依次淡入、上弹、缩放回弹。层级为 `ScrollView/Viewport/Content/RewardSlot/Visual`，Content 的每个直接子节点都是一个道具格子，Visual 仅负责该格子的动画。`DirectRewardRevealDemo` 使用 `DirectGridView`，无需 `InventoryRow`、`RowScrollView` 或 `VirtualGridView`。**Receive rewards** 重播，**Show all now** 立即显示全部；Inspector 可调整数量、初始延迟、出现间隔、动画时长、上弹距离和回弹幅度。大量道具仍按格子复用，刷新和回收按数据索引恢复动画进度。菜单 **Tools → ZRList → Rebuild Direct Reward Reveal Assets** 可单独重建。 默认内容超过一屏，可从道具区域或空白处拖动，也可用滚轮滚动；内容高度不超过视口时没有可滚动距离。

奖励示例进入 Play Mode 后自动播放，点击 **Receive rewards** 可以重新播放，**Show all now** 跳过动画。`RewardRevealDemo` 的 `RewardCount`、`InitialDelay`、`ItemInterval`、`PopDuration`、`RiseDistance` 和 `ScaleOvershoot` 分别控制奖励数量、开始延迟、道具间隔、单项动画时长、上弹距离与缩放回弹幅度。默认间隔为 0.12 秒。所有格子先保留布局位置，只在 `RewardSlot/Visual` 子节点上改变透明度、位置和缩放；按照奖励数据索引计算进度，格子刷新和复用不会重启动画。使用不受 `Time.timeScale` 影响的时间，游戏暂停时也能播放；禁用示例时结束动画并恢复显示。

物品拖拽示例使用独立的 `ItemDragSlot` prefab，仅在左侧 `DragHandle` 子对象上挂 `ItemDragHandle` 接收物品拖拽事件；拖动图标、文字和背景区域会滑动列表，目标格子的任意区域都可接受物品，业务视图仍为普通 C# 对象。拖起时创建不拦截射线的预览并停止列表惯性和跳转；释放时通过网格查询当前格子索引，交换数据并刷新两个格子。原格子保持在布局中；拖拽取消、格子回收或组件禁用时清理预览并恢复滚动状态。

## 曲线滚动示例

打开 `Scenes/CurvedScroll.unity` 并进入 Play Mode。1,000 张卡片继续由 `VirtualScrollView` 按可见范围复用，`CurvedScrollDemo` 在列表与惯性更新后的 LateUpdate 中修改 `CurvedCard/Visual` 子节点。支持 Arc、Custom curve、半径增减、翻转、切线旋转开关和首尾动画跳转；**Tools → ZRList → Rebuild Curved Scroll Assets** 可单独生成场景与 prefab。

Inspector 中的 `Curve` 配置：

| 参数 | 含义 |
| --- | --- |
| `Mode` | `Arc` 为指定半径圆弧，`Custom` 使用 AnimationCurve |
| `ArcRadius` | 视口局部 UI 单位中的半径；场景默认 1,100 |
| `CustomCurve` | 时间 0–1 对应视口沿滚动方向的起点到终点，值乘以 Amplitude；可编辑波浪、S 形等曲线 |
| `Amplitude` | 自定义曲线的副轴幅度；场景默认 65 |
| `Invert` | 翻转副轴偏移与切线方向 |
| `RotateAlongCurve` | 控制器上的开关，沿切线旋转或保持卡片正向 |
| `CrossAxisOffset` | 相对视口副轴中心的额外偏移 |

以 item 布局中心在视口主轴上的位置为参数，横向滚动改变 y，竖向滚动改变 x。主轴投影保持线性，所以原有虚拟化、拖拽和跳转可以直接复用；沿圆弧的实际弧长速度随位置变化。此方式适合能沿主轴表达的圆弧、波浪和 S 形，不表示闭合圆或回折路径。圆弧中心位于 `(0, radius)`，偏移采用稳定的圆弧弓高公式；超过圆弧两端 ±80° 的区域沿切线延伸，较小半径和弹性越界也不会产生 NaN 或把 item 堆在同一点。注意为所选半径／幅度留足视口高度或宽度。

`ScrollCurve` 位于运行库，可在其他视图中复用 `Evaluate(distance, halfExtent, out offset, out angle)`；distance 沿滚动顺序（向右／向下）为正，angle 是相对默认方向的角度。自定义曲线使用 257 个缓存采样点与导数插值，约 2 KB，每次查询为 O(1)。替换曲线对象时自动重建；通过代码原地修改 keys、切线或 wrapMode 后，调用 `Curve.RebuildCache()`，再调用 `demo.ApplyCurve()`。Inspector 编辑自动重建缓存。运行时修改其他参数后调用 `ApplyCurve()` 即可立即应用。

每帧处理 O(可见项数)，静止时跳过更新；组件引用、业务视图和标题均预先缓存，滚动热路径没有 LINQ、协程、临时集合、格式化字符串或逐项 AnimationCurve.Evaluate。首次创建实例、标题初始化和首次曲线缓存会分配，后续重建复用采样数组。回收／禁用恢复 Visual，Root 始终由列表管理。prefab 的 224×224 布局槽覆盖 148×160 卡片旋转后的最大投影，减少边缘提前回收；修改卡片大小时也应为旋转留出布局空间。列表的 `IsItemVisible`／`GetVisibleItems` 查询逻辑布局槽，业务需要视觉矩形的精确相交时使用缓存的 `CurvedItemView.Visual`。

## 尺寸与布局的职责

- `IScrollItemAdapter` 提供估算尺寸、当前尺寸计算和数据绑定，不保存测量缓存。
- item 视图决定如何测量内容。可选的 `ScrollItemMeasurement` 支持通过预制体的 Unity 自动布局进行测量。
- `VirtualScrollView` 接收滚动方向上的长度，维护尺寸索引、位置、可见范围、复用与滚动补偿。item 长度不包含列表的间距和 Padding。
- `ItemSizeIndex` 只保存当前布局长度，用于树状数组的位置和总长度计算，不决定是否跳过测量。单项更新、前缀位置和位置查找为 `O(log n)`，总长度读取为 `O(1)`。初始化为 `O(n)`，尾部追加 k 项为 `O(k log(n+k))`，容量增长时另有复制成本。两个数组约占 `12 × capacity` 字节，容量不足时成倍扩展；重载和清空数据保留历史容量，`Dispose` 释放。视图数量取决于可见范围和池的历史峰值。

核心按初始化与配置、更新事务、布局与回收、定位、回调和事件组织。运行库使用 `ZRList` 命名空间和独立的 `ZRList.Runtime` 程序集。示例使用 `ZRList.Samples` 命名空间。

未测量项使用 `EstimatedItemSize`。因此未掌握全部尺寸时，总长度与远距离位置是估算值；跳转时只获取目标附近的实际尺寸，并完成局部对齐。列表目前有首尾边界，数据索引支持循环映射。

## 接入方式

先添加 `using ZRList;`，再添加 `UI/ZRList` 组件，设置 `ItemPrefabs`（单模板也放入数组）、`Content`、`Viewport` 和 `ScrollRect`。滚动组件必须使用 `VirtualScrollRect`，它同步补偿拖拽起点和惯性采样位置。Content 放在视口下，使用左上 pivot `(0, 1)`，其左上角在 anchoredPosition 为零时与视口左上角对齐；不要再挂自动排列根 item 的 LayoutGroup 或 ContentSizeFitter。常用接入只需要初始化和一个渲染回调：

```csharp
scrollView.Initialize(data.Count);
scrollView.OnItemRender += OnItemRender;

void OnItemRender(ScrollItemView item, int dataIndex)
{
    if (item.CachedComponent == null) {
        item.CachedComponent = new ItemView(item.Root);
    }

    ((ItemView)item.CachedComponent).SetData(data[dataIndex]);
}
```

`ItemView` 是普通 C# 视图类，不继承 `MonoBehaviour`，也不挂在 prefab 上；构造函数通过 `RectTransform` 绑定 UI 组件，`SetData`／`SetInfo` 按业务模型定义。回调参数 `ScrollItemView` 提供 `Root` 和一个 `object CachedComponent`；首次判空创建视图对象，之后强转调用，组件查找仅在构造函数中执行一次。缓存属于实际 item 实例，回收、重绑和同一适配器的重新加载均保留；实例销毁时清空。需要 GameObject 时使用 `item.Root.gameObject`。背包示例的按钮监听也只在视图构造时绑定一次，复用时更新当前数据索引，回收时清理业务回调。

未绑定渲染函数时不会创建占位 item，绑定后立即填充首屏；先绑定再初始化也支持。滑动时，新建或复用的 item 进入显示范围会触发 `OnItemRender`，仍在原范围内的已绑定 item 不会每帧重绘。退出视口后回收，再次进入时重新填充数据。`RefreshItem`、重新加载数据和改变测量布局条件也会触发需要更新的 item。

回调必须覆盖文字、图片、颜色、选中状态等全部业务显示。尺寸可由 item prefab 的 Unity 布局测量；业务直接计算高度或宽度时，在回调里调用 `scrollView.SetItemSize(dataIndex, size)`。变尺寸的远距离跳转也可能使用临时复用视图测量目标附近的数据，因此回调应只填充已有数据，不发起网络请求。

可选的 `OnItemRecycle(ScrollItemView, int)` 用于取消异步任务或清理绑定事件，可以直接使用已有的 `CachedComponent`，不要在回收时清空组件缓存。示例初始化时订阅一次，在控制器销毁时解除。列表禁用时保留已建立的滚动监听，处理函数立即忽略禁用对象；重新配置或 Dispose 时解除，避免嵌套分组启停时重复分配 UnityEvent 监听。重新绑定渲染函数会立即刷新当前视图。十个示例均使用此回调方式，业务不需要编写适配器或 `BindView`。原有 `Init(Action<GameObject, int>)` 接口保留。

`ViewStateChanged` 在绑定、布局和回收完成后通知可见视图、池或布局状态的变化，可用于更新统计文字；相同范围内的小幅滚动不会持续发送通知。示例直接在这个事件中更新状态栏，没有刷新状态栏的 `Update`。

切换 tab 时先替换业务数据，再调用 `scrollView.ReloadData(data.Count)`，即使数量相同也要刷新。空数据同样调用 `ReloadData(0)`：回收全部可见 item，保留对象池和视图缓存，不触发 `OnItemRender`。再次传入正数即可复用这些实例显示新数据。业务在切换方法中根据数量控制位于 Content 外的“暂无数据”提示。

## 自定义适配器（可选）

需要自定义尺寸计算、强类型视图工厂和资源销毁管理时，可使用保留的 `Initialize(adapter, count)` 接口。适配器实现：

| 成员 | 作用 |
| --- | --- |
| `EstimatedItemSize` | 未知 item 的有限、正数估算长度 |
| `CreateViewsHolder` | 为列表创建的 root 返回视图包装；可返回自己的 ScrollItemView 子类；不能替换或激活 root |
| `Bind` | 更新复用视图的完整显示状态 |
| `TryGetItemSize` | 按当前数据和布局条件直接计算尺寸；需要视图测量时返回 false。查询应无副作用 |
| `MeasureItem` | 每次绑定后，在没有本次尺寸提交且不能直接计算时测量视图；返回有限、正数长度 |

`ScrollItemLayoutContext` 提供方向、扣除横向 Padding 后的可用尺寸、实际 item 横向尺寸和布局版本。每次绑定、刷新和跳转测量都按当前条件重新取得尺寸。列表拥有 root 的最终位置和尺寸；视图测量后应停止 root 上持续改写尺寸的布局组件，子节点布局可以继续工作。

`StretchItems` 默认关闭，保留原预制体的横向尺寸；开启后，竖向列表铺满可用宽度，横向列表铺满可用高度。布局刷新重新取得可见项尺寸；仅改变滚动方向上的视口长度或列表间距时，保留屏外项的当前布局长度。

推荐继承 `ScrollItemAdapter<TView>`，使用强类型视图，减少类型转换和重复接口代码。下面演示业务直接提供竖向 item 高度：

```csharp
using System.Collections.Generic;
using ZRList;
using UnityEngine;
using UnityEngine.UI;

public sealed class RowModel
{
    public string Title;
    public float Height; // 业务计算的正数长度，不含列表间距
}

public sealed class RowView: ScrollItemView
{
    public readonly Text Title;
    public RowView(RectTransform root): base(root, -1, -1)
    {
        Title = root.GetComponentInChildren<Text>(true);
    }
}

public sealed class RowAdapter: ScrollItemAdapter<RowView>
{
    private readonly IReadOnlyList<RowModel> m_rows;
    public RowAdapter(IReadOnlyList<RowModel> rows)
    {
        m_rows = rows;
    }
    public override float EstimatedItemSize
    {
        get { return 80f; }
    }
    protected override RowView CreateView(RectTransform root)
    {
        return new RowView(root);
    }
    protected override void BindView(RowView view, int index, ScrollItemLayoutContext context)
    {
        view.Title.text = m_rows[index].Title;
    }
    public override bool TryGetItemSize(int index, ScrollItemLayoutContext context, out float size)
    {
        size = m_rows[index].Height;
        return true;
    }
}

// scrollView.Initialize(new RowAdapter(rows), rows.Count);
```

需要视图测量时返回 false，基类默认通过 Unity 布局测量；也可覆盖 `MeasureView` 使用业务测量结果。Root 没有 ContentSizeFitter 或 ILayoutController 时直接读取固定尺寸；有布局控制器时同步重建布局，缺失组件使用 TryGetComponent 查询。应避免在回调中进行昂贵的数据处理、I/O 或每次查找子节点。组件引用在创建包装时缓存，尺寸每次按当前内容和 `ScrollItemLayoutContext` 重新计算。

`Init(Action<GameObject, int>)`、`Init(IScrollItemAdapter)` 也可使用，数量来自 `ItemsCount`。

## 多个 item prefab

模板数量没有固定上限，可以使用 1、2、3、4、5 种或更多。prefab 数组配置在 `VirtualScrollView.ItemPrefabs` 的 Inspector 上；业务脚本只为每条数据提供模板索引，初始化和渲染仍各调用一次：

```csharp
scrollView.InitializeWithPrefabIndex(data.Count, GetItemPrefabIndex);
scrollView.OnItemRender += OnItemRender;

public int GetItemPrefabIndex(int dataIndex)
{
    return data[dataIndex].PrefabIndex;
}
```

`ItemPrefabs` 是 `RectTransform[]`，数量至少为 1，集合中不能有空模板。索引范围是 `0` 到 `ItemPrefabs.Length - 1`；多模板时必须提供业务选择函数，单模板直接 `Initialize(count)` 即可。数组为空时必须通过显式选择函数或 `IScrollItemPrefabProvider` 提供模板，否则初始化报错。显式调用 `Initialize(count, Func<int, RectTransform>)` 优先使用该选择函数。不再提供独立的 `ItemPrefab` 字段；模板索引错误会明确报错。普通适配器和旧 `Init(callback)` 入口使用长度为 1 的数组，异构适配器通过 `IScrollItemPrefabProvider` 选择模板。

列表按实际 prefab 引用匹配闲置实例，不同模板不会交换对象缓存。回调中的 `item.Prefab` 提供该实例的源模板，可用于决定首次创建哪一种普通 C# item view；随后继续通过 `object CachedComponent` 强转调用。数据类型变化后调用 `RefreshItem(dataIndex)`；列表会替换不匹配的模板并重新获取尺寸，原绑定快照失效。切换数据集仍使用 `ReloadData(count)`，空数据仍使用 `ReloadData(0)`。

闲置实例按 prefab 分组管理；池内顺序不属于业务接口。稳定滚动只为进入的 item 选择模板、绑定和摆放，保留项沿用当前绑定。修改已显示项的数据或模板后必须调用刷新接口，不依赖跨界时重新检查所有项。新项测量改变尺寸或回调提交更新时，列表会自动转入完整布局流程。

聊天模板已移到场景中列表组件的 `ItemPrefabs`，`ChatScrollDemo` 不再保存模板字段。示例按消息的 `PrefabIndex` 选择模板，状态栏读取列表数组实际长度。示例中的偶数槽／奇数槽只是聊天样式的业务分类，运行库不限制模板类型或分类方式。单模板时同一视图根据消息数据更新发送者和左右对齐。

原有 `Initialize(count, IReadOnlyList<RectTransform>, Func<int, int>)` 和直接返回模板引用的入口继续兼容。选择函数应只读取数据，不修改列表状态。替换组件上的数组对象时自动更新布局；原地修改数组中的模板后调用 `ReloadData(count)`，重置布局长度并重新获取首屏尺寸。网格对应配置是 `VirtualGridView.CellPrefabs`，使用相同的 `InitializeWithPrefabIndex` 入口。

自定义适配器接入可通过 `IScrollItemPrefabProvider.GetItemPrefab(int dataIndex)` 提供相同的模板选择；其 `CreateViewsHolder`、`Bind` 和测量继续使用实际 prefab 的布局条件。

## 背包网格

全部示例的 ScrollRect 均使用 `Elastic` 边界模式（Elasticity 为 0.1，启用惯性），拖过首尾边界后松手会自动回弹；两个嵌套列表 prefab 的内层也采用相同设置。`RewardReveal` 和 `DirectRewardReveal` 默认各 24 个道具，内容超过一屏，可直接拖拽或用滚轮浏览。生成器与 UPM 示例保存相同配置。

`InventoryGrid.unity` 使用示例组件 `DirectGridView`，层级为 `ScrollView/Viewport/Content/InventorySlot`，每个格子都是 Content 的直接子节点。只需配置原生 `ScrollRect` 和 `InventorySlot.prefab`，不需要行 prefab 或行 item。滚动时按格子回收复用，列数随视口宽度变化；`ColumnCount` 大于零可固定列数。业务示例见 `InventoryGridDemo`，先订阅 `OnItemRender`／`OnItemRecycle`，再调用 `Initialize(count)`；`RefreshItem(index)` 只更新对应格子。菜单 **Tools → ZRList → Rebuild Inventory Grid Assets** 可单独重建场景。

包内的 `VirtualGridView` 仍用于 `ItemDrag` 和原 `RewardReveal` 示例，以下是它的按行布局接入方式：

`VirtualGridView` 将一行作为 `VirtualScrollView` 的虚拟项，格子同样通过 `Initialize(count)` 和 `OnItemRender` 填充，也支持可选的 prefab 选择。它只创建可见行及池中保留的行和格子。

1. 给列表设置空的行 prefab（参考 `InventoryRow.prefab`），并配置 `VirtualScrollRect`、Content、Viewport。
2. 添加 `VirtualGridView`，设置 `RowScrollView`、`CellPrefab`、`CellSize` 和 `CellSpacing`。
3. `ColumnCount = 0` 根据可用宽度自动计算列数，大于零使用固定列数。
4. 调用 `grid.Initialize(itemCount)`，绑定 `grid.OnItemRender += OnItemRender`，在回调中更新格子的图片、数量、选中状态等。网格统一使用固定的 `CellSize`。

```csharp
grid.Initialize(itemCount);
grid.OnItemRender += OnItemRender;

grid.JumpToItem(dataIndex, 0.3f);
grid.RefreshItem(dataIndex); // 重绑所在行，更新数量或选中状态
grid.TryGetVisibleItem(dataIndex, out ScrollItemView cell);
grid.ReloadData(newCount);
```

网格 API 使用格子的业务索引，内部按行定位；末行只显示真实数据。自动列数随视口变化更新，并将原首个可见数据项所在的新行作为定位锚点。支持竖向、固定尺寸的规则网格；不同高度格子和瀑布流需要另一种布局实现。参考 `ItemDragDemo` 和 `RewardRevealDemo` 的接入。

网格提供相同的 `OnItemRecycle` 与 `ViewStateChanged`，支持 `Initialize(itemCount, itemPrefabs, prefabIndexSelector)` 配置任意数量的格子模板，也保留直接返回模板的 `Initialize(itemCount, prefabSelector)`。自定义适配器接口 `Initialize(cellAdapter, itemCount)` 保留。

格子优先保留在所属行中，模板变化后按源 prefab 进入独立的闲置池，可在不同行之间复用，保留 `CachedComponent`；换模板不会持续销毁原格子。`PooledCellCount` 返回闲置格子总数，`grid.TrimPool()` 释放闲置行和格子，`grid.TrimPool(retainedCellCount)` 保留指定总数的闲置格子。`Dispose` 释放所有行、格子和对象缓存，包括生命周期回调抛错后的其余资源。

## 分组折叠列表

使用 `ExpandableListController<TGroup, TItem, TKey>` 接入一级分组，多层嵌套使用 `TreeListController<TNode, TKey>`。简单页面可通过数据访问函数、显示策略和 `OnItemRender` 接入；需要整套复用绑定逻辑时，继承 `ExpandableListAdapter` 或 `TreeListAdapter` 并通过 Adapter 构造入口接入。控制器独占一个未初始化的 `VirtualScrollView`，要求 `FirstDataIndex = 0`。下面先展示回调模式。

```csharp
// 数据访问方法在构造后固定；内部沿用业务模型，不复制业务状态。
var tree = new TreeListController<Node, int>(scrollView, GetNodeKey, GetChildren)
{
    EstimatedItemSize = 64f,
    ItemPrefabSelector = SelectItemPrefab,
    NodeComparison = CompareBusinessPriority
};
tree.OnItemRender += OnItemRender;
tree.Submit(roots);

void OnItemRender(ScrollItemView view, TreeListRow<Node, int> row)
{
    if (view.CachedComponent == null) {
        view.CachedComponent = new NodeView(view.Root);
    }
    ((NodeView)view.CachedComponent).Render(row.Node, row.Depth, row.IsExpanded);
}
```

业务提供稳定 Key、父子关系、状态汇总、排序规则和显示内容。控制器管理展开状态，执行排序，将树转成线性可见行；底层列表负责虚拟化、池、尺寸和滚动。业务修改红点、解锁、阅读或结构并更新汇总后，只需调用一次 `Submit`，同时完成内容、结构和顺序更新，并按 Key 保持展开状态和阅读锚点。滚动时不会重新读取整棵树或重新排序。

`OnItemRender` 的第二个参数是行上下文：一级分组提供 `IsHeader`、`Group`、`Item`，树提供 `Node`、`Depth`、`HasChildren` 和 `IsExpanded`。可在 `Submit` 后订阅；未订阅时不创建空白 item。列表启用时，订阅增删立即刷新，移除最后一个渲染订阅后回收可见项但保留池；禁用时延至重新启用后处理。导航可能临时绑定屏外项进行测量，因此该回调不代表曝光或阅读。不要同时使用底层 `scrollView.OnItemRender` 渲染折叠行。

需要强类型视图或自定义尺寸时配置 `ViewFactory`、`ItemSizeProvider` 和 `ItemMeasurer`。`EstimatedItemSize` 默认 100。通过 `OnItemRecycle(view, oldRow)` 结束旧绑定，包括重绑及回收；通过 `OnViewDestroyed(view)` 释放物理视图资源。`ViewFactory` 和这两个清理事件必须在渲染器初始化前配置，不能在初始化后改变；模板和尺寸策略修改后通过 `Submit` 应用。`Dispose` 完成清理后再清空委托，不应提前取消清理订阅。所有回调内禁止重入修改控制器。

`ExpandableList`、`TreeList` 和 `BusinessSortedTree` 演示 Adapter 接入，分别使用 `ChapterListAdapter`、`CampaignTreeAdapter` 和 `BusinessPriorityTreeAdapter`。Adapter 的 `Bind` 负责渲染，`CreateView` 缓存组件，`Unbind` 结束旧绑定，`DestroyView` 清理实例资源。Adapter 模式不可再配置上述显示回调，排序、展开与提交接口则完全相同。控制器直接实现底层接口，两种入口无需额外 Bridge；数据访问委托只在构造时缓存。完整示例见[分组折叠](ExpandableLists.md#通过-adapter-复用绑定逻辑)和[多层折叠](TreeLists.md#通过-adapter-复用绑定逻辑)。

需要完整的 `OnItemRender` 示例时，打开 `Scenes/CallbackTree.unity`。脚本 [CallbackTreeDemo.cs](../Samples~/Basic/Scripts/Showcase/CallbackTreeDemo.cs) 直接配置函数构造入口，并订阅控制器的 `OnItemRender`、`OnItemRecycle`、`OnViewDestroyed`；组件和按钮监听只在 `ViewFactory` 中创建一次。完整接口与约定见[一级分组折叠](ExpandableLists.md)和[多层折叠列表](TreeLists.md)。

## 内容和尺寸更新

```csharp
// 更新内容、字体或布局后，重新绑定并重新取得尺寸。
scrollView.RefreshItem(dataIndex);

// 业务已确定新长度：直接提交结果。
scrollView.SetItemSize(dataIndex, newHeight);

```

不保存已测量尺寸缓存，也不保留跨绑定的显式尺寸覆盖。`SetItemSize` 更新当前布局；在绑定或测量回调里提交的尺寸用于本次更新，之后刷新、回收再显示或跳转时重新获取尺寸。非可见项刷新不会立即创建视图，进入视口时绑定并测量。`TryGetItemSize` 读取当前布局长度，尚未测量的项返回估算值，不表示尺寸已精确。业务修改文字后调用 `RefreshItem` 重新测量。

连续更新使用批次，支持嵌套；最后一次有效通知决定该项的结果：

```csharp
using (scrollView.BeginUpdateScope())
{
    scrollView.RefreshItem(dataIndex);
    scrollView.SetItemSize(otherDataIndex, knownHeight);
}
```

作用域在异常时也会结束批次，每次创建有一个小对象分配；高频路径可使用原有 `BeginUpdate`／`EndUpdate` 加 try/finally。提交批次后，列表统一更新总长度和布局，并保持一个实际可见 item 相对视口的位置。接近首尾时受内容边界约束；拖拽中的弹性偏移保留，拖拽起点和惯性采样同步补偿，动画目标随尺寸变化更新。

所有接口和业务回调在 Unity 主线程调用。绑定／测量期间的尺寸通知、刷新和跳转会延后处理；每次刷新最多同步处理四轮回调产生的新通知，其余延至下一帧。回调不能不断触发自身刷新，也不能调用初始化、销毁或池裁剪接口。

`RefreshItem`、`SetItemSize`、`JumpToDataItem`、`TryGetVisibleItem`、`TryGetItemSize` 使用业务数据索引。兼容接口 `JumpToItem` 使用显示顺序索引；当 `FirstDataIndex` 非零时，两者可能不同。新业务建议统一使用数据索引接口。

## 异步尺寸结果

复用视图开始异步工作前获取绑定快照，完成后在 Unity 主线程提交：

```csharp
if (scrollView.TryGetItemBinding(itemGameObject, out var binding))
{
    // 保存 binding；在异步内容准备完成后使用。
    bool accepted = scrollView.SetItemSize(binding, measuredHeight);
}
```

视图被复用、重新绑定、列表重新初始化、禁用或测量布局版本变化后，旧快照会被拒绝。内容变化时必须调用 `RefreshItem`。批次中已排队的刷新也会让旧快照失效；提交尺寸返回 true 表示接受提交，批次真正执行时还会验证一次，届时已失效的结果会丢弃。

## 视图生命周期与资源

首次创建和回收后显示的顺序是：创建／取出视图 → 绑定新数据 → 激活 → 取得尺寸 → 设置位置。实例在非激活父节点下创建，避免 OnEnable 先看到未绑定数据；不会改变 prefab 资产的激活状态。

适配器可实现 `IScrollItemViewLifecycle`，或覆盖强类型基类的 `UnbindView`／`DestroyView`：

- `UnbindView` 在复用、刷新重绑、回收时调用，仍可读取旧数据索引。取消异步任务、移除业务事件、释放与旧数据关联的资源；必须保留视图包装和 root，供下一次绑定使用。
- `DestroyView` 在池裁剪、切换适配器实例和最终销毁时调用。释放视图持有的资源；root 由列表负责销毁，回调可以清空自己的引用。
- 同一适配器实例重新初始化可以复用池；不同实例的视图会销毁，避免把旧业务持有的包装交给新业务。
- `TrimPool()` 释放闲置视图，`TrimPool(retainedCount)` 留下指定数量；`Dispose()` 解绑监听、清除缓存并释放全部实例，可随后再次初始化。生命周期回调抛错时，Dispose 仍会完成其他实例的清理，再报告异常。

Bind 应完整覆盖会变化的显示状态。异步绑定结果同样需要验证业务身份或绑定快照；尺寸提交接口只保护尺寸，不会替业务阻止旧图片等内容写入。

`VisibleItems` 是渲染实例的实时只读集合包装。`IsInitialized`、`ItemCount`、`PooledViewCount`、`IsJumping`、`ScrollOffset` 可用于读取状态。公开运行时字段和 `VisibleItemViews` 等集合已统一命名；新业务应通过列表 API 修改状态，通过 `VisibleItems` 读取渲染实例，通过 `GetVisibleItems` 获取实际位于视口内的 item。

不要直接增删可见集合或派生类可访问的回收集合；它们与内部索引共同维护实例归属。生命周期回调执行时增量更新可能尚未完成，需要完整、有序的最终集合时，在 `ViewStateChanged` 后读取。显式裁剪池也会清理空的模板分组，避免保留无用的模板引用。

## 定位与兼容参数

`JumpToDataItem(dataIndex, duration)` 按业务数据定位；`JumpToItem(viewIndex, duration)` 保留显示顺序定位。duration 为 0 时立即跳转，大于 0 时动画跳转；首项保留起始 Padding，其他项保留半个间距，尾部受内容边界约束。批次或绑定回调中只记录最后一次跳转，在安全时机执行；数据跳转按执行时的映射定位。可用 `CancelJump()` 取消，用户拖拽也会取消并恢复原滚动设置。

数据数量或整体数据集变化调用 `ReloadData(count)`，停止惯性和跳转、重置位置与尺寸索引，然后刷新首屏。它保留初始化状态、渲染回调和可复用实例，配置不变时保留滚动事件订阅。更换 prefab、Content 或 ScrollRect 时会同步相关配置，旧模板不会继续参与渲染。运行时直接改变数量的兼容方式仍会重新初始化；新业务应使用 `ReloadData`。修改方向、Padding、间距、视口尺寸或循环映射可调用 `RefreshLayout`，也会在 Update／LateUpdate 和下次列表 API 调用前检测。尺寸缓存、缓存状态与失效接口已删除。

## 性能与能力边界

普通滚动先通过尺寸索引查询首尾范围。同范围且没有待处理更新时，只同步 Content 位置，跳过可见集合重建、prefab 查询及 item 遍历。有交集且尺寸和布局稳定的跨界滚动只回收离开项、绑定进入项；尺寸、布局或刷新变化时自动回退到完整更新。

稳定尾部复用已完成的校正结果，避免连续拖拽和回弹时重复测量。每次新绑定、显式刷新和实际跳转测量仍重新获取尺寸；无动画跳转在没有回调提交新更新时复用首次目标解析结果。内容或 prefab 选择结果变化须调用 `RefreshItem` 等刷新接口，不依赖滚动发现变化。

网格跳过无变化的布局写入，单个格子刷新仍重绑整行；曲线与奖励动画继续逐帧更新。初次创建、集合扩容、显式批次作用域及业务代码仍可能产生分配。实现与分析入口见[性能优化说明](Performance.md)。

当前支持具有首尾边界的线性列表、任意数量的 item prefab 与固定尺寸竖向网格，以下能力尚未实现：

- 真正无首尾的无限循环、不同高度格子的网格和瀑布流。
- 吸顶分组和分组网格。线性序列增删、移动与重排可使用 `ApplyData`；一级分组可用 `ExpandableListController`，多层树可用 `TreeListController` 按稳定 Key 提交。`ReloadData` 仍会重置位置。
- 每帧测量预算、后台分页和取消任务调度；异步任务与数据加载由业务持有。
- 超长列表的坐标重基准和稀疏索引。当前索引占用 O(n) 内存，最终 Unity 坐标为 float；超大数据集需要分页或进一步设计。

仅在尾部增加数据时，先扩展业务集合，再调用 `scrollView.AppendItems(addedCount)`。已有布局长度、绑定快照和滚动位置保留，新增索引以估算长度初始化；追加取消当前跳转，保留滚动速度，不自动跟随到底部。该入口要求 `FirstDataIndex = 0`，并在 item 回调和更新批次之外调用；更新、删除、排序或替换旧数据仍使用相应刷新接口或 `ReloadData`。聊天示例使用 `AppendItems(1)` 后显式跳到最新消息，避免重建历史消息的尺寸索引。

item 尺寸必须有限且大于 0；隐藏数据应从数据集中移除，而非提交零尺寸。屏外内容尚未测量或已经改变时，总长度、滚动条和远距离位置具有估算性质；列表不再维护全量尺寸已知状态。

运行库已封装为 UPM 包，只依赖 `com.unity.ugui`。示例使用独立的 asmdef，安装运行库不会自动编译示例，也不需要 2D 或 TextMeshPro 包。

## 查询 item 与索引

列表和网格使用相同的查询入口，索引均为业务数据索引：

| 接口 | 语义 |
| --- | --- |
| `TryGetVisibleItem(dataIndex, out item)` | 获取当前渲染范围内已绑定的实例；屏外、越界或尚未绑定时返回 false，item 为 null |
| `TryGetItemIndex(ScrollItemView, out dataIndex)` | 从当前列表／网格拥有的实例反查索引；失败时为 -1 |
| `TryGetItemIndex(GameObject, out dataIndex)` | 从 item 根 GameObject 反查索引；子节点、其他列表的实例和已回收实例返回 false |
| `GetVisibleItems(results, fullyVisible: false)` | 清空并填充调用方的 List，返回实际与视口相交的活跃 item 数量；按显示顺序排列 |
| `GetVisibleItems(results, fullyVisible: true)` | 只返回矩形完全位于视口内的 item |
| `IsItemVisible(dataIndex, fullyVisible: false)` | 判断指定数据的实例是否实际与视口相交；可传 true 要求完全可见 |

`VisibleItems` 保留为列表当前渲染实例的实时只读集合，可能包含因首项保留、Padding 或弹性越界而位于视口外的实例；网格的 `VisibleCellCount` 同样统计渲染范围内已绑定的格子。需要获取“视口内正在显示的所有 item”时，使用 `GetVisibleItems`。它检查视口的两个轴，部分裁剪默认算可见，仅接触视口边缘不算；网格不会把溢出视口宽度的列算作可见。组件禁用、对象层级隐藏或视口没有面积时，可见结果为空。该判定基于根 RectTransform 与视口的几何关系，不检查透明度、其他遮挡物或更深层的 Mask。

结果集合由业务持有，可重复使用，内部只遍历已渲染的实例；反向查询通过索引／根对象字典校验归属，不扫描整个数据集。查询不会为了获取屏外数据而创建 item；需要操作屏外数据时直接修改业务模型，需要显示它时先跳转，再查询。

```csharp
private readonly List<ScrollItemView> m_visibleItems = new List<ScrollItemView>();

public void UpdateVisibleSelection(VirtualScrollView scrollView)
{
    scrollView.GetVisibleItems(m_visibleItems);
    foreach (ScrollItemView item in m_visibleItems) {
        if (scrollView.TryGetItemIndex(item, out int dataIndex)) {
            ((ItemView)item.CachedComponent).SetSelected(dataIndex == SelectedIndex);
        }
    }

    if (scrollView.TryGetVisibleItem(SelectedIndex, out ScrollItemView selected)) {
        // selected 是当前绑定 SelectedIndex 数据的实际实例。
    }
}
```

`GetVisibleItems` 会替换结果，而非追加；不要传入列表自己的 `VisibleItemViews`。返回值表示本次调用时的状态，保存的实例引用可能在后续滚动中复用为另一条数据；重新查询可获取当前索引，异步任务仍应使用绑定快照校验旧身份。

列表另提供 `TryGetDataIndex(viewIndex, out dataIndex)` 和 `TryGetViewIndex(dataIndex, out viewIndex)`，用于转换受 `FirstDataIndex` 影响的显示顺序索引，映射不要求 item 已创建，非法索引返回 false／-1。批次内查询反映当前已提交的绑定和布局；需要更新后的结果时，在 `EndUpdate` 后读取。完整视口查询在渲染／测量回调内部返回空或 false，业务应在回调结束后查询，并使用回调已提供的数据索引填充 item。
