# 分组折叠列表

`ExpandableListController<TGroup, TItem, TKey>` 在 `VirtualScrollView` 上提供一级分组折叠。标题和展开的子项都是独立的虚拟行，共用现有布局、模板池和动态尺寸能力。

业务通过回调或 Adapter 提供分组关系、稳定 Key 和显示逻辑，并注入可选比较器；控制器管理展开状态、执行排序并生成可见行，`VirtualScrollView` 负责布局、虚拟化和视图复用。红点、解锁、已读、权限以及标题上的汇总内容由业务计算。参见可运行的 `ExpandableList.unity`：所有这些业务字段都位于示例 `ExpandableListDemo` 中。

## 接入

先配置一个未初始化的 `VirtualScrollView`，包括 Content、Viewport 和 VirtualScrollRect。`FirstDataIndex` 必须为 0。一个控制器独占一个列表；不要同时使用旧的 Initialize、ReloadData 或修改 ItemsCount。

简单页面可以通过回调接入，在构造函数中提供三个数据访问函数：

需要可运行的回调示例，可参考 [CallbackTree](TreeLists.md#onitemrender-回调示例)。它使用多层树控制器演示 `OnItemRender`、视图工厂和清理事件，分组控制器采用相同的回调生命周期。

| 成员 | 职责 |
| --- | --- |
| `GetGroupKey(group)` | 返回稳定、非空且全局唯一的分组 Key |
| `GetItems(group)` | 返回组内子项；空组返回空集合 |
| `GetItemKey(item)` | 返回稳定、非空且跨组全局唯一的子项 Key |

数据访问函数在构造后固定，返回当前业务模型中的数据；结构变化后调用 `Submit` 重新读取。显示与尺寸使用以下配置及事件：

| 成员 | 职责 |
| --- | --- |
| `EstimatedItemSize` | 未测量行的主轴估计长度，默认 100，必须有限且大于 0 |
| `ItemPrefabSelector(row)` | 根据行类型和业务数据选择标题／子项模板 |
| `ViewFactory(root)` | 可选：创建持有组件引用的强类型 `ScrollItemView`；默认使用普通视图包装 |
| `ItemSizeProvider(row, context, out size)` | 可选：返回 bool，直接计算主轴长度；返回 false 时使用视图测量 |
| `ItemMeasurer(view, row, context)` | 可选：返回测量长度；默认使用现有 Unity 布局测量 |
| `OnItemRender(view, row)` | 完整覆盖本次绑定的显示状态 |
| `OnItemRecycle(view, oldRow)` | 可选：旧绑定结束，取消异步工作和旧业务订阅，保留视图缓存 |
| `OnViewDestroyed(view)` | 可选：释放物理视图持有的资源，root 由列表销毁 |

`ExpandableListRow` 提供 `IsHeader`、`IsExpanded`、`GroupKey`、`ItemKey`、`Group`、`Item`。标题行的 Item／ItemKey 为默认值，不能据此访问子项。只有标题使用 IsExpanded。

分组与子项的 Key 属于不同命名空间，可以使用相同的数字。子项跨组移动时保持原 Key，就能保持其定位身份。同一业务对象同时出现在多处时，需要为每个展示实例提供不同的 Key。不要使用行下标或可变字段作为 Key；Key 的相等与哈希采用 `EqualityComparer<TKey>.Default`。

```csharp
// Group、Entry 和 EntryView 由业务定义；这些方法只在初始化时绑定委托。
var list = new ExpandableListController<Group, Entry, int>(
    scrollView, GetGroupKey, GetItems, GetItemKey)
{
    EstimatedItemSize = 72f,
    ItemPrefabSelector = SelectItemPrefab,
    DefaultExpanded = true,
    GroupComparison = CompareGroups, // 可省略
    ItemComparison = CompareItems   // (group, left, right) => int，可省略
};
list.OnItemRender += OnItemRender;
list.Submit(groups);

void OnItemRender(ScrollItemView view, ExpandableListRow<Group, Entry, int> row)
{
    if (view.CachedComponent == null) {
        view.CachedComponent = new EntryView(view.Root);
    }
    ((EntryView)view.CachedComponent).Render(row);
}

// 业务完成一次或多次数据修改后，只提交一次。
UpdateBusinessData();
list.Submit(groups);

list.Toggle(groupId);
list.SetExpanded(groupId, true);
list.SetAllExpanded(false);
list.ScrollToItem(itemId, expandIfNeeded: true, duration: 0.25f);

// 在 UI 的销毁流程中调用。
list.Dispose();
```

回调模式需要强类型包装时设置 `ViewFactory = CreateItemView`，让它返回自定义 `ScrollItemView` 子类，再在 `OnItemRender` 中强转。通过 `OnViewDestroyed` 移除创建视图时注册的按钮监听。不要每次渲染都查找组件、添加监听或创建捕获行下标的闭包；绑定时更新稳定 Key，点击时读取当前 Key。

## 通过 Adapter 复用绑定逻辑

需要在同类页面复用整套接入逻辑时，继承 `ExpandableListAdapter<TGroup, TItem, TKey>`。可运行示例使用此入口，完整实现见 [ExpandableListDemo.cs](../Samples~/Basic/Scripts/Showcase/ExpandableListDemo.cs) 中的 `ChapterListAdapter`：

```csharp
// 页面初始化时创建一次 Adapter；数据、状态汇总和比较器仍由业务提供。
var list = new ExpandableListController<Group, Item, int>(scrollView, new ChapterListAdapter(this))
{
    DefaultExpanded = true,
    GroupComparison = CompareGroups,
    ItemComparison = CompareItems
};
list.Submit(groups);
```

Adapter 集中实现三个数据访问方法、`EstimatedItemSize`、`GetItemPrefab` 和 `Bind`。可选重写 `CreateView` 缓存组件、`TryGetItemSize` 提供已知尺寸、`MeasureItem` 测量动态尺寸、`Unbind` 结束旧绑定、`DestroyView` 清理物理视图。`Bind` 是 Adapter 模式的渲染入口；`Unbind` 也会在保留实例的重绑前执行，接收旧快照行；导航的屏外测量也可能调用 `Bind`，不代表阅读或曝光。

接入模式在构造时确定。Adapter 模式不再设置控制器的显示策略或订阅 `OnItemRender`、`OnItemRecycle`、`OnViewDestroyed`，混用会抛出异常；排序、提交、展开、定位 API 在两种模式下相同。渲染器初始化后保持 Adapter 的视图工厂和清理策略一致；修改模板、尺寸策略或业务数据后，通过 `Submit` 应用。Adapter 方法内同样禁止重入修改控制器。

控制器负责展开状态、行投影和视图生命周期，Adapter 不需要保存行下标或复制数据。示例 Adapter 持有页面引用，因此每个页面创建一个实例以复用其实现；没有页面状态、仅提供共享模板的 Adapter 也可以共享实例。控制器销毁时先完成视图清理，再解除 Adapter 引用，不负责销毁调用方的 Adapter 对象。

数据访问委托只在控制器构造时缓存，渲染直接调用 Adapter 方法；没有额外 Bridge，也不为每个节点创建 Adapter。返回已有集合并在视图创建时缓存组件和监听，可避免滚动过程中创建临时集合、闭包或重复查询组件。

## 回调生命周期与配置

可以先 `Submit` 再订阅 `OnItemRender`；没有渲染订阅时不会创建空白 item。列表启用时，增加或删除渲染订阅会立即刷新已初始化的渲染器；删除最后一个订阅会结束可见项绑定并回收它们，保留池供重新订阅时复用。列表禁用时，这些刷新和回收延至恢复启用后处理。渲染入口属于控制器，不要另外订阅 `scrollView.OnItemRender` 来渲染折叠行。

导航可能临时绑定屏外项以测量尺寸，这些绑定也会调用 `OnItemRender`。它不是曝光或阅读通知，不能仅因渲染回调执行就将业务项标为已读。

`OnItemRecycle` 表示旧绑定结束，也会在刷新重绑、提交重排和视图复用前触发，不代表该实例一定进入池。回调提供旧快照中的行信息，随后才切换快照并绑定新数据。业务对象本身没有深拷贝；如果原地修改了对象，旧行仍引用这个对象。不要在这里销毁 root 或清空组件缓存。`OnViewDestroyed` 表示物理实例结束，可移除按钮监听或释放实例级资源。

`ViewFactory`、`OnItemRecycle` 和 `OnViewDestroyed` 必须在渲染器初始化前配置；初始化后更改会抛出异常，避免池中的旧视图被交给另一套工厂或清理逻辑。渲染器因错误释放后，可以修正这些配置并重新 `Submit`。`ItemPrefabSelector`、`ItemSizeProvider`、`ItemMeasurer` 和 `EstimatedItemSize` 可在回调之外修改，修改后调用 `Submit` 统一应用；渲染订阅的增删按上述启用状态处理。

`Dispose` 先完成解绑和销毁，再清空控制器保存的委托。直接调用 `Dispose` 即可，不要提前取消清理事件订阅。所有数据访问、排序、渲染、测量和生命周期回调都不允许重入修改控制器；后续业务变化应排队到回调之外。

控制器直接显式实现底层列表接口，将行下标转换为行上下文。回调与 Adapter 两种入口共用这条渲染路径，业务不需要调用底层接口。

## 提交与排序

`Submit(IReadOnlyList<TGroup>, ScrollUpdatePosition)` 是统一的数据更新入口：读取完整结构、验证所有 Key、应用可选排序、生成展开行，再提交一次布局更新。

- 没有比较器时使用数据源顺序；有比较器时只排序内部快照，不修改输入集合。
- 子项比较器接收所属分组以及两个子项。比较结果相等时按输入顺序排列。
- 比较器、数据访问器和 Key 获取函数应无副作用，且排序条件在整个 Submit 期间保持一致。修改比较器后，下次 Submit 才应用。
- 折叠组的子项也会读取、验证和排序；其状态可以影响业务分组比较器。
- 展开／收起只使用已经提交的结构，不重新调用数据访问器或比较器。业务对象变化后必须 Submit，组件不订阅属性变化，也不会在滚动中自动排序。
- 快照固定 Key、关系和顺序，保留业务对象引用，不深拷贝数据。即使引用和 Key 没变，每次 Submit 仍重新绑定可见项，并将未确认有效的长度恢复为估算；不会创建全部子项来测量。
- 重复 Key、读取或排序异常发生在提交前，不改变已提交列表。渲染、模板或生命周期回调在提交过程中抛异常时，底层释放所拥有的视图并退出初始化状态；修复业务错误后可重新 Submit。

一次提交自然合并了内容、结构和排序变化。业务不用连续调用标题刷新、子项刷新、结构刷新与排序刷新。高频业务事件应先合并，再在主线程提交一次。

## 展开状态与定位

| API | 行为 |
| --- | --- |
| `SetExpanded(key, value)` / `Toggle(key)` | 返回是否改变；未知组返回 false |
| `IsExpanded(key)` | 查询提交后的状态；未知组返回 false |
| `SetAllExpanded(value)` | 一次更新所有组，触发一次 ExpansionChanged |
| `ExpansionChanged` | 显式展开操作完成后触发，可读取状态用于保存 |
| `TryGetHeaderIndex` / `TryGetItemIndex` | 获取本次展示行下标，隐藏子项返回 false；不要长期保存下标 |
| `TryGetRow` | 将展示行下标转换成分组／子项上下文 |
| `ScrollToGroup(key, duration)` | 定位分组标题 |
| `ScrollToItem(key, expandIfNeeded, duration)` | 定位子项；按参数决定是否展开所属组 |

再次 Submit 时，仍存在的分组按 Key 保留展开状态。新组使用 DefaultExpanded；从提交数据中移除的组会丢弃其展开状态。过滤后重新引入的组视为新组；如需跨过滤保存，可由业务在外部保存并恢复状态。

Submit 默认使用 `ScrollUpdatePosition.KeepVisibleItem`，保持首个实际可见行及其视口内偏移。也可以选择 `KeepOffset` 或 `Start`。点击可见标题展开／收起时优先保持标题位置；锚点子项被隐藏时回退到所属标题；分组也被删除时选择邻近保留行。首尾边界和尚未测量的屏外尺寸仍会限制精度。

所有接口在 Unity 主线程使用。Submit、展开操作、Dispose 和配置修改不允许从渲染、回收、测量、提交通知或更新批次内重入。回调需要触发新的结构变化时，由业务排队到回调之外。异步内容仍需校验业务 Key 或 `ScrollItemBinding`；一次提交会使旧绑定快照失效。

## 底层集合更新

普通线性列表也能使用新增的 `VirtualScrollView.ApplyData`：

```csharp
// 每个元素对应新列表的一行。旧索引不可重复；-1 表示新增。
// PreserveSize 只能在调用方确认内容、模板与尺寸仍有效时设为 true。
updates.Add(new ScrollItemUpdate(previousIndex, preserveSize: false));

// 缓存此委托。回调只切换已经准备好的数据，不调用列表 API。
scrollView.ApplyData(updates, commitPreparedData, options);
```

先完成旧绑定的 Unbind，再调用 commitPreparedData，之后更新索引和新绑定。保留下来的可见实例可以继续使用；模板变化时自动更换实例。数据更新取消旧跳转，正常保持滚动速度；位置补偿同步调整拖拽起点。Start 策略同时停止惯性。

`ScrollDataUpdateOptions` 可指定位置策略、旧列表的优先锚点下标和新列表的回退下标。ApplyData 要求已初始化、FirstDataIndex 为 0，在回调与 BeginUpdate/EndUpdate 之外调用。映射在返回前不得修改。展开控制器已封装映射与提交回调，使用它时不要直接调用 ApplyData。

## 性能和范围

滚动时只访问已生成的行描述，再复用现有虚拟列表逻辑；不遍历分组、不排序、不为每次绑定创建闭包。控制器使用两个可复用的紧凑数据快照、展示行缓冲及索引字典，不为每个分组创建内部子列表。旧快照的业务引用在操作结束时清除，容量保留用于后续更新。

Submit 的结构读取和行匹配为 O(n)，排序另计。展开／收起为 O(g + v)，其中 g 为组数，v 为展开后的展示行数；不会重新读取折叠组的全部子项。底层尺寸索引线性重建，并复用树数组作为重排暂存，避免额外尺寸数组。

容量增长、首次实例化、业务回调和 Unity UI 自身可能分配内存；不承诺整个业务界面绝对零 GC。预热后的核心滚动、固定容量提交和展开操作由 Tests~ 中的分配回归覆盖。此接口面向一级分组；多层嵌套使用 [TreeListController](TreeLists.md)，原有接口保持不变。吸顶标题、分组网格、异步加载和展开高度动画没有内置。
