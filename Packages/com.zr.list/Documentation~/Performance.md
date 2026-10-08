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
