# 多层折叠列表

`TreeListController<TNode, TKey>` 将任意深度的树投影到一个 `VirtualScrollView`。每个可见节点是一行，所有层级共用虚拟化、模板池、动态尺寸和滚动定位。界面上的嵌套由行的 `Depth` 表达，不需要给每个分组创建 ScrollRect 或子列表。

只有标题和一层子项、且两者数据类型不同的业务，可以继续使用 [ExpandableListController](ExpandableLists.md)。两套接口独立，不改变已有一级分组调用方式。

## 可运行示例

导入 **ZRList Demos** 后打开 `Scenes/TreeList.unity`，点击 Play。仓库本地开发工程对应 `Assets/Demos/Basic/Scenes/TreeList.unity`。

示例包含 6 个活动、24 个章节、72 个小节和 360 个任务，共 462 个节点、4 层。点击任意分支标题可展开／收起；点击任务可解锁、阅读或增加通知。底部按钮支持全部展开／收起、通知任务 6435、全部已读、定位隐藏的任务 6435。首次运行展开一条路径，四个层级均可直接看到。

红点、解锁、未读汇总及排序都在 `TreeListDemo.cs` 中。示例沿父链重新计算汇总后提交一次；组件内部没有这些业务字段。`Notify task 6435` 会使有通知的活动、章节、小节和任务在各自同级中前移。`Find task 6435` 一次展开整条祖先链并滚动到任务。

### OnItemRender 回调示例

打开 `Scenes/CallbackTree.unity`，本地工程对应 `Assets/Demos/Basic/Scenes/CallbackTree.unity`。示例包含 8 个章节、32 个小节和 256 门课程，共 296 个节点、3 层；首次展开一条路径。实现位于 [CallbackTreeDemo.cs](../Samples~/Basic/Scripts/Showcase/CallbackTreeDemo.cs)，附中文注释。

`InitializeDemo` 通过数据访问函数构造控制器，配置 `ViewFactory`、模板和尺寸策略，再订阅三个回调：`OnItemRender` 直接更新文字、展开箭头、未读提示和深度缩进；`OnItemRecycle` 结束旧点击绑定；`OnViewDestroyed` 移除实例级监听。组件与监听随物理视图创建一次，重绑更新稳定 Key。`Dispose` 完成清理后统一释放回调，无需提前退订清理事件。

点击章节／小节可以折叠；点击课程切换阅读状态。**Toggle unread priority** 切换 ID 排序与“未读优先、再按 ID”排序，**Expand all / Collapse all** 操作全部分支，**Find lesson 8408** 自动展开隐藏课程的祖先链并定位。阅读状态和比较规则都在业务脚本内，一次 `Submit` 同时刷新内容与顺序；渲染本身不会把课程标为已读。

示例复用 `TreeBranch` 和 `TreeLeaf` 预制体。菜单 **Tools → ZRList → Rebuild Callback Tree Assets** 可单独重建场景。

### 业务排序交互示例

打开独立场景 `Scenes/BusinessSortedTree.unity`（本地为 `Assets/Demos/Basic/Scenes/BusinessSortedTree.unity`）。四层树包含 4 个活动、8 个章节、16 个小节和 64 个任务，共 92 个节点。右侧显示规则、选中任务的三个独立状态开关，以及根分组的排序前后对照。

规则按优先级依次比较：**有红点优先 → 已解锁优先 → 未读优先 → ID 升序**。只比较同级节点，高优先级存在差异时不再比较后续字段。行上的 `DOT / OPEN / UNREAD` 用 1、0 展示参与排序的实际值。

初始选中隐藏的任务 1224，根分组顺序为 **04 → 03 → 02 → 01**：04 有红点，03 已解锁且有未读，02 已解锁且全已读，01 未解锁且有未读。可依次验证：

| 操作（从重置状态开始，按顺序） | 根分组顺序 | 原因 |
| --- | --- | --- |
| 给任务 1224 加红点 | 01 → 04 → 03 → 02 | 01 与 04 都有红点且未解锁，01 的未读优先 |
| 清除该红点 | 04 → 03 → 02 → 01 | 恢复初始顺序 |
| 解锁任务 1224 | 04 → 01 → 03 → 02 | 01 与 03 都已解锁且有未读，ID 决定先后 |
| 将任务 1224 标为已读 | 04 → 03 → 01 → 02 | 03 的未读优先；01 与 02 再按 ID 排序 |

各开关只修改自己的字段，标为已读不会自动清除红点。这是为便于单独观察规则而选择的示例业务行为。分支按“任意后代满足即为 true”汇总；取消一个子项的红点时，其他子项仍有红点就继续保留分支红点。

点击 **Find selected task** 展开选中任务的祖先链。点击其他任务可以换选，再通过右侧修改它的状态。**Root overview** 收起所有分支并回到顶部，后续业务修改在总览中从顶部显示新顺序；详情模式则保持阅读锚点与已有展开状态。**Expand all** 展开全部节点；**Reset scenario** 恢复初始状态、选中任务与折叠总览。

业务实现位于 `BusinessSortedTreeDemo.cs`：`CompareBusinessPriority` 定义规则，`UpdateSummary` 汇总状态，`SubmitChangedPath` 更新受影响祖先后只提交一次。绑定使用已缓存的显示字符串；按钮监听随物理视图创建一次，重绑时更新稳定 Key。状态修改和说明文字更新允许少量业务分配，不在滚动绑定或比较器中拼接字符串。运行库接口无需为这些业务规则增加字段。

## 接入业务数据

可以通过回调或 Adapter 接入，均沿用当前业务模型，无需复制成组件提供的节点类。简单页面可在构造函数中提供 `GetKey` 和 `GetChildren`。两个数据访问函数构造后固定，业务修改模型后通过 `Submit` 重新读取。

| 成员 | 职责 |
| --- | --- |
| `GetKey(node)` | 稳定、非空、整棵森林全局唯一的展示身份 |
| `GetChildren(node)` | 返回已有子节点集合；叶子可返回 null 或空集合 |

回调模式通过属性提供显示策略，通过事件处理渲染与清理：

| 成员 | 职责 |
| --- | --- |
| `EstimatedItemSize` | 有限且大于 0 的主轴估计长度，默认 100 |
| `ItemPrefabSelector(row)` | 按节点类型、深度或业务数据选择模板 |
| `ViewFactory(root)` | 可选：返回缓存 Text、Button 等组件的 `ScrollItemView` 子类，默认返回普通视图包装 |
| `ItemSizeProvider(row, context, out size)` | 可选：返回 bool，直接提供主轴长度；返回 false 时使用视图测量 |
| `ItemMeasurer(view, row, context)` | 可选：返回主轴长度，默认使用现有 Unity 布局测量 |
| `OnItemRender(view, row)` | 完整覆盖内容、缩进、展开箭头和其他显示状态 |
| `OnItemRecycle(view, oldRow)` | 可选：结束旧绑定，取消异步任务或旧业务订阅，保留组件缓存 |
| `OnViewDestroyed(view)` | 可选：清理实例级资源；root 由列表销毁 |

`TreeListRow` 是值类型，提供 `Key`、`Node`、`HasParent`、`ParentKey`、`Depth`、`ChildCount`、`HasChildren`、`IsExpanded`。根节点深度为 0；只有 `HasParent` 为 true 时才能使用 `ParentKey`。叶子的 `IsExpanded` 始终为 false。深度是展示信息，组件不强制固定缩进或样式。

不同类型的业务节点可通过业务接口、基类或轻量包装统一为 TNode。同一业务对象在多个位置展示时，每个展示实例需要独立的 Key；不能复用同一节点身份。环、重复 Key 和重复出现的共享节点都会在提交前被拒绝，折叠的后代也参与校验。Key 使用 `EqualityComparer<TKey>.Default`，不要使用行下标或可变排序字段作为 Key。

```csharp
// scrollView 必须未初始化，FirstDataIndex 为 0，且由 controller 独占。
var tree = new TreeListController<MyNode, int>(scrollView, GetNodeKey, GetChildren)
{
    EstimatedItemSize = 64f,
    ItemPrefabSelector = SelectItemPrefab,
    DefaultExpanded = false,
    NodeComparison = CompareNodes // 所有同级使用的比较器，可省略
};
tree.OnItemRender += OnItemRender;
tree.Submit(roots);

void OnItemRender(ScrollItemView view, TreeListRow<MyNode, int> row)
{
    if (view.CachedComponent == null) {
        view.CachedComponent = new NodeView(view.Root);
    }
    ((NodeView)view.CachedComponent).Render(row.Node, row.Depth, row.IsExpanded);
}

// 业务自己处理状态、汇总和结构修改，然后合并提交。
UpdateBusinessStateAndSummaries();
tree.Submit(roots);

tree.Toggle(nodeId);
tree.SetExpanded(nodeId, true);
tree.SetAllExpanded(false);
tree.ScrollToNode(targetId, expandIfNeeded: true, duration: 0.3f);

// 隐藏节点也可查询；行下标只对可见投影有效。
tree.TryGetNode(nodeId, out var node);
tree.TryGetRowIndex(nodeId, out int rowIndex);
tree.TryGetRow(rowIndex, out var row);

// UI 销毁时释放；不要同时直接操纵 view 的数据集合。
tree.Dispose();
```

`MyNode`、`NodeView` 和访问函数由业务提供。回调模式也可以设置 `ViewFactory = CreateItemView`，使用强类型 `ScrollItemView` 子类。不要在每次渲染中添加捕获节点／下标的按钮闭包。示例每个物理视图只注册一次监听，绑定时覆盖 Key，点击时使用当前 Key。

## 通过 Adapter 复用绑定逻辑

需要在同类页面复用整套接入逻辑时，继承 `TreeListAdapter<TNode, TKey>`。两个多层示例使用此入口，完整实现见 [TreeListDemo.cs](../Samples~/Basic/Scripts/Showcase/TreeListDemo.cs) 中的 `CampaignTreeAdapter` 和 [BusinessSortedTreeDemo.cs](../Samples~/Basic/Scripts/Showcase/BusinessSortedTreeDemo.cs) 中的 `BusinessPriorityTreeAdapter`，均附中文注释。

```csharp
// 页面初始化时创建一次；业务先更新状态及祖先汇总，再统一 Submit。
var tree = new TreeListController<Node, int>(scrollView, new BusinessPriorityTreeAdapter(this))
{
    NodeComparison = CompareBusinessPriority
};
tree.Submit(roots);
```

| Adapter 成员 | 职责 |
| --- | --- |
| `GetKey` / `GetChildren` | 返回稳定身份和已有子节点集合 |
| `EstimatedItemSize` / `GetItemPrefab` | 提供有限正估计值，选择现有模板 |
| `CreateView` | 可选：创建一次视图包装，缓存组件并注册实例级监听 |
| `Bind(view, row, context)` | 完整覆盖当前行显示，是 Adapter 模式的渲染入口 |
| `TryGetItemSize` / `MeasureItem` | 可选：优先提供已知尺寸，否则测量已绑定视图 |
| `Unbind(view, oldRow)` | 可选：结束旧绑定，包含保留实例的重绑；接收旧快照行 |
| `DestroyView(view)` | 可选：销毁实例前移除监听、释放资源，root 由列表销毁 |

接入模式在构造时确定。Adapter 模式不再设置控制器的显示策略或订阅 `OnItemRender`、`OnItemRecycle`、`OnViewDestroyed`，混用会抛出异常；排序、提交、展开、定位 API 在两种模式下相同。初始化后保持 Adapter 的视图工厂和清理策略一致；模板、尺寸策略或数据变化通过 `Submit` 应用。Adapter 方法内禁止重入修改控制器。`Bind` 也可能用于导航的屏外测量，不能据此标记已读。

Adapter 集中封装业务接入，控制器管理每份列表的展开状态、行投影和视图生命周期。示例 Adapter 持有页面引用，因此每个页面创建一个实例以复用其实现；无页面状态的实现也可以共享实例。控制器销毁时先清理视图，再解除 Adapter 引用，不负责销毁调用方的 Adapter 对象。

控制器构造时缓存数据访问委托，渲染直接调用 Adapter 方法，不额外创建 Bridge 或每节点适配对象。数据访问返回已有集合，绑定使用缓存组件、稳定 Key 和业务预计算字符串，避免在滚动过程中创建临时集合、闭包或重复监听。

## 业务与列表职责

业务决定节点关系、稳定 Key、红点／解锁／阅读状态、祖先汇总规则、比较器及显示内容；控制器管理展开状态、执行同级排序、生成可见行、保持身份和定位；`VirtualScrollView` 负责虚拟化、视图池、布局、尺寸测量和滚动。组件不会扫描红点字段，也不会自动订阅业务属性变化。

例如，任务增加红点时，业务先修改任务，再沿父链更新汇总，最后调用一次 `Submit`。父节点红点如何汇总由业务决定；同级排序和折叠后隐藏后代由控制器执行。模板选择和缩进样式由业务回调确定，模板实例化及复用由列表完成。

## 回调生命周期与配置

`OnItemRender` 可以在 `Submit` 之后订阅。没有渲染订阅时不创建空白 item。列表启用时，新增或移除订阅会立即刷新已初始化的渲染器；最后一个渲染订阅被移除时，结束当前可见项绑定并回收实例，保留池。列表禁用时，这些刷新和回收延至恢复启用后处理。折叠行的渲染入口是 `tree.OnItemRender`，不要同时使用 `scrollView.OnItemRender`。

导航可能临时绑定屏外节点以测量尺寸，这些绑定也会调用 `OnItemRender`。它不是曝光或阅读通知，不能仅因渲染回调执行就将业务节点标为已读。

`OnItemRecycle` 表示旧绑定结束，包括刷新重绑、提交更新、进入池及复用，不表示一定进入池。回调传入旧快照中的行信息，随后才切换快照并绑定新数据；业务对象本身仍是引用，原地修改的字段不会还原。这里应取消旧业务订阅和异步工作，不能销毁 root 或清空组件缓存。`OnViewDestroyed` 在物理实例销毁时清理按钮监听和实例级资源。

渲染器初始化后，不能修改 `ViewFactory` 或增删 `OnItemRecycle`、`OnViewDestroyed` 订阅，避免已有池对象的创建和清理策略不一致。请在首次 `Submit` 前完成这些配置。渲染器因错误释放后可以修正配置，再次提交。`ItemPrefabSelector`、`ItemSizeProvider`、`ItemMeasurer`、`EstimatedItemSize` 可在回调之外修改，随后通过 `Submit` 统一应用；渲染订阅的变化按上述启用状态处理。

`Dispose` 在解绑和销毁完成后清空委托，不需要提前取消清理订阅。所有数据访问、比较、渲染、测量和生命周期回调内均不能重入变更控制器；需要继续修改业务时排队到回调之外。异步结果还需检查稳定 Key 或 `ScrollItemBinding` 是否仍有效。

控制器通过显式实现底层接口把下标转换为 `TreeListRow`，回调与 Adapter 共用此路径。使用者只调用控制器公开的业务接入接口，底层 `VirtualScrollView` 仍只处理线性行。

## 排序与提交

- `NodeComparison(left, right)` 只在同级内部排序，包含根节点。
- 可选 `ChildComparison(parent, left, right)` 覆盖非根层的比较规则，便于不同父节点使用不同规则；根节点仍使用 NodeComparison。
- 未提供比较器时保留数据源顺序；比较结果相等时保留本次输入顺序。排序只作用于内部快照，不修改业务集合。
- `Submit` 读取并验证全部结构，排序所有同级集合，按展开状态生成前序可见行，然后提交一次布局更新。隐藏节点的排序会在下次展开时体现。
- 收起／展开使用上次提交的结构，不重新读取或排序。业务内容、父子关系或比较器变化后，需要再次 Submit。
- 快照保存 Key、层级、顺序和对象引用，不深拷贝业务对象。访问器、Key 函数、比较器应无副作用，在一次提交期间保持一致。
- 每次 Submit 都重新绑定可见内容并使旧绑定失效。无法确认有效的尺寸恢复为估计，不会实例化整棵树来测量。

数据读取、校验、比较器异常不会改变已提交快照。渲染、模板选择或生命周期回调在提交过程中抛异常时，底层释放视图并退出初始化状态；修复业务异常后可以再次 Submit。

## 展开与位置语义

| API | 行为 |
| --- | --- |
| `Toggle` / `SetExpanded` | 返回是否改变；叶子和未知 Key 返回 false |
| `IsExpanded` | 查询分支的展开状态，不受祖先可见性影响 |
| `SetAllExpanded` | 包括所有隐藏分支，一次提交、一次事件；无变化时不触发事件 |
| `ExpansionChanged` | 显式展开操作完成后触发，可在此读取状态，不允许重入修改 |
| `ScrollToNode` | 可选择自动展开全部祖先；目标分支自己的状态保持不变 |
| `TryGetNode` | 查询包含隐藏节点在内的已提交数据与层级信息 |
| `TryGetRowIndex` / `TryGetRow` | 在稳定 Key 与当前可见行下标之间转换 |
| `NodeCount` / `RootCount` / `RowCount` | 所有节点数、根节点数、展开后的行数 |

收起父节点保留后代展开状态。隐藏分支的单次 SetExpanded 只更新状态，不重建当前视图。按稳定 Key 重新提交、排序、跨父节点移动时保留展开偏好；首次出现的 Key 使用 DefaultExpanded。移除 Key 后再提交回来会被视为新节点。空分支按叶子处理，其展开偏好仍随 Key 保留，恢复子项后继续使用。

默认 Submit 保持实际可见锚点及视口偏移，也可传入 `KeepOffset` 或 `Start`。点击可见分支标题时优先保持标题位置；锚点被隐藏时沿新父链找到最近的可见祖先，被删除时沿旧父链寻找保留的可见祖先，再回退到邻近行。尺寸估计和首尾边界会限制精度。重排仍复用身份相同且模板兼容的可见实例。

全部接口在 Unity 主线程调用。控制器独占 view；不要同时 Initialize、ReloadData、ApplyData 或修改 ItemCount。不要在绑定、生命周期、布局批次或提交事件中重入修改；需要修改时由业务排队到回调之外。异步内容继续使用 Key 或 `ScrollItemBinding` 校验。

## 性能范围

完整读取采用迭代方式，内部快照将同级节点放入连续区间，使用父节点和同级索引进行前序遍历。没有递归调用，也不为每个节点创建内部列表。两份快照、两份可见行缓冲及索引字典按历史最大容量复用；只有一个按最大同级数量增长的排序暂存列表。操作结束后清空旧业务引用。

设 n 为全部节点，v 为投影行数，d 为目标深度：Submit 为 O(n)，排序另计为各同级集合 O(k log k) 之和；普通展开／收起为 O(旧 v + 新 v)，隐藏分支单独切换为 O(1)；全部展开／收起增加 O(n) 扫描；隐藏节点定位增加 O(d) 祖先处理。滚动只访问已经生成的行，不遍历业务树。内存为 O(n + v)，并受历史峰值容量影响。

`Tests~/Editor/TreeListRegression.cs` 覆盖一万层链、随机结构和展开操作、同级排序、隐藏重复／环、跨父节点移动、横竖轴动态尺寸、锚点回退及预热分配。真实四层场景另有 Play Mode 点击与定位回归。首次建表、扩容、实例化、业务字符串及 Unity Canvas 可能分配；不承诺整个界面绝对零 GC。

当前没有内置异步懒加载、吸顶标题、分组网格或展开高度动画。异步业务可在获取子项后自行修改模型并再次 Submit。
