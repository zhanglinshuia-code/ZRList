# 性能优化

ZRList 按可见范围维护实例：同范围滚动只移动 Content，稳定跨界只处理进入和离开的 item；拖拽、惯性、回弹与动画继续按当前位置更新。

## 优化清单

以下路径相对于包目录。

| 位置 | 技术 | 减少的工作 |
| --- | --- | --- |
| `Runtime/Core/VirtualScrollView.Layout.cs` | 首尾索引缓存与同范围提前返回 | 跳过可见集合重建、prefab 查询和 item 遍历 |
| `Runtime/Core/VirtualScrollView.Incremental.cs` | 范围求交与差集更新；头部新增一次性插入 | 只回收离开项、绑定和摆放进入项，保留项无需重复检查模板或几何 |
| `Runtime/Core/ItemSizeIndex.cs` | Fenwick 树 O(log N) 查询；预计算查找起始位；尺寸版本号 | 避免线性查找和重复准备工作，尺寸变化及时使范围缓存失效 |
| `Runtime/Core/VirtualScrollView.Layout.cs` | 按尺寸版本复用尾部校正 | 减少尾部连续拖拽、回弹时的重复测量 |
| `Runtime/Core/VirtualScrollView.Navigation.cs` | 即时跳转复用首次目标解析；回调提交更新后重新解析 | 避免无变化时重复查询尺寸或测量 |
| `Runtime/Core/VirtualScrollView.Pooling.cs`、`Runtime/Views/ScrollItemView.cs` | 按 prefab 分桶；保存桶内与总表索引；交换末项删除 | 通常以 O(1) 获取匹配实例，避免全池扫描和中间删除的引用移动 |
| `Runtime/Core/VirtualScrollView.Layout.cs` | 单次获取复用已选 prefab；统一 `LayoutHolder` 并复用局部几何读取 | 减少创建、替换、测量中的重复模板选择，以及 RectTransform 读取 |
| `Runtime/Core/VirtualScrollView.cs`、`VirtualScrollView.Updates.cs` | 无业务回调介入的相邻步骤复用布局检查结果 | 减少空闲更新、显式刷新和按索引提交尺寸的重复配置比较 |
| `Runtime/Grid/VirtualGridView.cs` | 稳定状态直接读取字典数量；按值写入布局 | 可见格子计数为 O(1)，减少无变化的 Unity 属性写入 |
| `Samples~/Basic/Scripts/Showcase/DirectGridView.cs` | 固定尺寸范围缓存，重建或重载时失效 | 跳过同范围内的格子遍历、池维护和重复状态通知 |
| `Samples~/Basic/Scripts/Showcase/RewardItemView.cs` | 按值更新动画属性；固定三次幂改为乘法 | 减少等待和完成阶段的 UI 写入及缓动计算 |
| 核心滚动调用链 | 分段 `ProfilerMarker` | 区分配置检查、范围计算、实例更新与业务回调耗时 |
| `Runtime/Expandable/` | 紧凑双缓冲、稳定 Key 映射和缓存比较委托 | 一次提交处理内容与排序，滚动不访问业务分组；单个共享排序缓冲避免逐组分配 |
| `Runtime/Core/VirtualScrollView.Data.cs`、`ItemSizeIndex.cs` | 可见实例索引重映射；利用树数组暂存重排尺寸 | 保留可复用的物理实例，以 O(N) 更新几何，不额外分配尺寸副本 |

`N` 为数据项数，尺寸索引占用 O(N) 内存。增量更新减少了业务回调和 Unity 属性访问，但公开有序列表的头部移动仍需 O(可见项数) 的引用复制。

## 缓存与回退

- 尺寸版本变化、视口或布局配置变化、强制刷新及待处理更新都会阻止范围快路径。
- 新进入项的测量改变尺寸、回调提交更新或布局改变时，自动转入完整渲染；不相交的远距离跳转也使用完整渲染。
- 绑定、布局和回收成功后才发布范围缓存；异常路径保留实例归属，失败的新项标记为待刷新，后续更新可以重试。
- 几何数据只在本次摆放内复用，布局检查结果只在没有业务代码介入的相邻步骤间复用。
- 池内空桶在滚动时复用，`TrimPool` 清理空桶与模板引用，`Dispose` 清空池；已销毁的闲置实例在获取时剔除。

## 接入要求

| 变化 | 调用 |
| --- | --- |
| 单项内容或 prefab 改变 | `RefreshItem(index)` |
| 可见内容批量改变 | `RefreshVisibleItems()` |
| 动态尺寸改变 | `SetItemSize`；异步结果使用绑定快照重载 |
| 数据重载 / 尾部追加 / 布局变化 | `ReloadData` / `AppendItems` / `RefreshLayout` |
| 普通列表序列增删／重排 | `ApplyData`，提供新旧行映射与数据切换回调 |
| 折叠列表业务数据变化 | `ExpandableListController.Submit`，内部统一处理刷新、顺序与位置 |
| 多层树业务数据变化 | `TreeListController.Submit`，内部处理各层排序、父子关系、展开与位置 |

普通滚动沿用保留项的绑定和几何信息，内容变化需要显式通知。按行网格刷新单个格子仍会重绑整行。完整用法见[使用说明](Usage.md)。

可见集合与回收集合由列表维护，不直接增删其成员。生命周期回调中集合可能处于更新过程，需要完整、有序的结果时在 `ViewStateChanged` 后读取。

## 性能分析

| Profiler 标记 | 内容 |
| --- | --- |
| `ZRList.CheckLayout` | 配置检查及其触发的布局刷新 |
| `ZRList.RangeLookup` | 范围查找与缓存判断 |
| `ZRList.RebuildViewport` | 完整可见集合更新 |
| `ZRList.UpdateRange` | 有交集范围的增量更新 |
| `ZRList.SelectPrefab` | 业务模板选择 |
| `ZRList.Bind` | 业务绑定 |
| `ZRList.QuerySize` | 业务尺寸查询 |
| `ZRList.Measure` | 实际尺寸测量 |

父标记包含子标记耗时，不能直接相加。首次创建、集合扩容、显式批次作用域及业务代码仍可能产生内存分配；实际性能需结合目标设备、Canvas 和业务内容采样。

已在 Unity 2022.3.5f1 验证包导入、EditMode 与 PlayMode 回归，覆盖横竖方向、动态尺寸、刷新、跳转、异常恢复、混合模板池，以及十个示例的交互。随机差分验证以完整渲染为对照，核对增量更新后的范围、实例、尺寸和位置。

## 折叠列表分配验证

新增的 `Tests~/Editor/ExpandableListRegression.cs` 使用 80 组、每组 24 个子项（全部展开共 2,000 行）的合成视图。先预热容量、模板池和排序委托，再使用当前线程的 `GC.Alloc` 记录器采样；测试先验证记录器确实能观测到一次主动分配。

Unity 2022.3.5f1、Windows Editor 同一轮 Play Mode 中分别通过回调和 Adapter 接入的记录如下。两种入口执行同一份用例；耗时仅用于本机诊断，不用于判定两种入口的速度优劣，业务绑定、编辑器负载和目标设备会影响结果。

| 操作 | 次数 | GC 分配事件（两种入口均） | 回调总耗时 | Adapter 总耗时 |
| --- | ---: | ---: | ---: | ---: |
| 同容量提交，含组间及组内排序 | 100 | 0 | 123.70 ms | 120.07 ms |
| 单组展开／收起 | 200 | 0 | 159.42 ms | 154.35 ms |
| 跨可见范围滚动 | 1,000 | 0 | 36.78 ms | 36.09 ms |
| 同范围滚动 | 1,000 | 0 | 1.02 ms | 1.02 ms |

Unity 的 `List.Sort(index, count, IComparer)` 在该运行时会为每次调用分配比较委托。折叠控制器采用缓存的 `Comparison`，并复用一个容量按最大组增长的排序缓冲，不为每个分组保存额外的排序集合。展开／收起不重新读取业务数据或重新排序。上述零分配结果不包含首次初始化、容量增长、业务产生的字符串及真实 Canvas 的更新。

同一功能与分配回归也通过 Unity 2021.3.45f1 的独立消费者工程验证，两种入口的四条预热路径均为 0 次分配。Unity 2022.3.5f1 的 Adapter 示例另外通过 Play Mode 点击、业务排序、隐藏项定位、拖拽期间提交数据及位移补偿验证。

## 多层树分配验证

`Tests~/Editor/TreeListRegression.cs` 使用 40 个根、每根 6 个分支、每分支 8 个叶子，共 2,200 个节点。预热和采样边界与上述合成视图测试相同。Unity 2022.3.5f1、Windows Editor 同一轮 Play Mode 的两种入口记录如下：

| 操作 | 次数 | GC 分配事件（两种入口均） | 回调总耗时 | Adapter 总耗时 |
| --- | ---: | ---: | ---: | ---: |
| 同容量提交，含各层同级排序 | 100 | 0 | 149.30 ms | 162.85 ms |
| 根分支展开／收起 | 200 | 0 | 169.57 ms | 162.12 ms |
| 跨可见范围滚动 | 1,000 | 0 | 35.40 ms | 35.78 ms |
| 同范围滚动 | 1,000 | 0 | 1.01 ms | 1.02 ms |
| 全部收起后展开祖先链并定位 | 100 | 0 | 36.80 ms | 37.46 ms |
| 隐藏分支切换 | 200 | 0 | 0.07 ms | 0.08 ms |

Unity 2021.3.45f1 的独立消费者工程也通过两种入口的六条分配路径。另覆盖一万层链式数据与随机树结构／展开变更，读取和投影均不依赖递归。四层 `TreeList` 场景通过真实 Button 点击、层级缩进、逐级业务排序、462 个节点虚拟化和隐藏任务定位的 Play Mode 检查。

树控制器复用两个紧凑快照、两个投影及一个同级排序缓冲，收起的子树不进入投影；隐藏分支单独切换只修改展开状态，不重建可见视图。容量按历史峰值保留，不为每个分组创建列表或独立 ScrollRect。上述结果限于已预热的核心路径，不含业务字符串分配、首次实例化或 Canvas 重建，耗时仅供本机诊断。

两类控制器直接实现底层接口，回调或 Adapter 方法传递值类型行上下文。Adapter 每个页面创建一次，访问委托在构造时缓存，渲染直接调用 Adapter，不为每个节点创建适配对象；滚动和提交内部不创建委托或闭包。业务应在初始化时缓存访问函数、比较器和渲染委托。注册／取消事件订阅属于配置操作，可能分配，不包含在上述滚动与数据更新采样中。

## OnItemRender 示例验证

`CallbackTree` 包含 296 个节点，使用真实 Text、Button 和行预制体。Unity 2022.3.5f1 Play Mode 中，在预热组件缓存与视图池后连续执行 1,000 次跨范围滚动，当前线程记录到 0 次 `GC.Alloc`，可见与池内实例总数没有增长。该采样覆盖同步滚动及 `OnItemRender` 绑定，不包含随后的 Canvas 重建、首次实例化、数据创建或编辑器其他帧。

组件查询和按钮监听在 `ViewFactory` 中执行一次；`OnItemRender` 只读取缓存组件和已有字符串，更新稳定 Key。`OnItemRecycle` 结束旧绑定，`OnViewDestroyed` 在实例销毁前移除监听。实际测试入口为 `Tests~/SampleEditor/CallbackTreeSampleRegression.cs`，同时验证排序后点击、回收后的旧绑定失效和隐藏课程定位。
