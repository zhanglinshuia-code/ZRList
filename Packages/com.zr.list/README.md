# ZRList

[English](README.en.md) | 简体中文

Unity uGUI 虚拟列表与网格。只创建和复用可见项，支持横向、竖向、动态尺寸、多种预制体、分组与多层折叠、嵌套滚动和动画定位。

- Unity **2021.3 LTS 及以上**，支持 Unity 6。
- UPM 包：`com.zr.list`，版本 **1.0.2**。
- 运行库仅依赖 `com.unity.ugui`。
- 许可证：[Apache-2.0](LICENSE.md)。

## 安装

默认跟踪 `main` 分支，方便后续通过 Package Manager 更新。Unity 菜单 **Window → Package Manager → + → Add package from git URL**，输入：

```text
https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#main
```

也可以下载仓库，在 Package Manager 中选择 **Add package from disk**，打开 `Packages/com.zr.list/package.json`。

安装后展开包的 **Samples**，导入 **ZRList Demos**。打开导入目录中的 `Scenes` 场景即可运行；仓库提供 UPM 包，示例由 Unity 导入你的工程。

需要固定到某个发布版本时，可以使用标签地址，例如：

```text
https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#v1.0.2
```

## 升级

本次版本为 **1.0.2**。完整步骤、安装来源差异与迁移事项见[升级指南](Documentation~/Upgrading.md)。若业务直接引用过公开工具类 `ZRList.GameObjectPool`，请按指南迁移；列表内部视图池不受影响。

- **通过 `#main` 安装**：在 Package Manager 中选中 **ZRList**，点击 **Update** 获取主分支最新提交。若当前 Unity 版本没有该按钮，再通过 **Add package from git URL** 输入同一个 `#main` 地址即可刷新。远端发布新提交后，需要主动更新，包不会自动变化。
- **通过 `#v1.0.0` 等标签安装**：版本已固定，**Update 不会自动切换到新标签**。通过 **Add package from git URL** 输入新的标签地址即可升级；也可以改用上面的 `#main` 地址，之后跟随主分支更新。重新添加 URL 会更新已有的同名包，无需先卸载。
- **更新示例**：包升级后，在 **Samples → ZRList Demos** 中重新导入，并打开新版导入目录中的场景。已复制到 `Assets/Samples` 的旧示例不会随包升级自动更新。

Git 包按 URL 指定的分支或标签解析，并通过锁文件记录提交。重新提交 Git URL 会重新解析该引用，具体规则见 [Unity 官方文档](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html#git-locks)。

## Unity 版本与输入系统

示例根据宿主工程的 **Active Input Handling** 自动选择输入模块：

| 工程设置 | 示例使用的输入模块 |
| --- | --- |
| Input Manager (Old)，未安装 Input System 包 | `StandaloneInputModule` |
| Input System Package (New) | `InputSystemUIInputModule` |
| Both，已启用 Input System | `InputSystemUIInputModule` |

无需切换工程输入设置。Input System 是可选依赖，运行库仍仅依赖 uGUI。所有示例的 EventSystem 都由 `DemoInputModule` 在首次更新前配置，场景生成器也采用相同方式。

原有十个示例已实际验证以下组合，均运行场景并检查 EventSystem 的点击、拖拽、滚轮和聊天输入框聚焦：

| Unity 版本 | 已验证的输入模式 |
| --- | --- |
| 2021.3.45f1 | Input Manager、Input System 1.11.2 |
| 2022.3.5f1 | Input Manager |
| 6000.3.11f1 | Input Manager、Input System 1.19.0、Both |

另外通过 Unity 6 的 Windows IL2CPP 播放器验证（High 级别代码裁剪、Input System），以及 Unity 2021.3 的场景重新生成验证。

新增四个折叠场景已通过 Unity 2022.3.5f1 Play Mode 交互验证；分组和树运行库的回调／Adapter 双入口还通过 Unity 2021.3.45f1 功能及预热分配回归。新增场景的记录与上述原有输入模式矩阵分别列示。

从 **1.0.0 升级**：按上面的升级步骤安装 `main` 或 `v1.0.1` 及以上版本，并重新导入 **ZRList Demos** 以取得输入兼容修复。自定义过的旧场景，可将 EventSystem 上的 `StandaloneInputModule` 替换为 `DemoInputModule`。

## 快速接入

1. 在滚动对象上添加 `VirtualScrollRect` 和 `VirtualScrollView`。
2. 设置 `Content`、`Viewport`、`ScrollRect` 和 `ItemPrefabs`，单模板也放入数组。
3. Content 使用左上角 pivot `(0, 1)`，不要添加排列根 item 的 LayoutGroup 或 ContentSizeFitter。
4. 业务代码使用 `using ZRList;`；自定义 asmdef 引用 `ZRList.Runtime`，使用 uGUI 类型时另引用 `UnityEngine.UI`。

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

`ItemView` 是由业务实现的普通 C# 视图类，构造函数缓存组件引用，`SetData` 填充数据。绑定回调后立即渲染首屏；item 离开视口后复用，`CachedComponent` 随实例保留。业务销毁时解除事件订阅。

常用操作：`RefreshItem(index)` 更新内容，`SetItemSize(index, size)` 更新主轴尺寸，`AppendItems(count)` 尾部追加，`TryGetVisibleItem` 查询可见项。详细参数、动态尺寸和资源生命周期见[使用说明](Documentation~/Usage.md)。

## 折叠列表接入

| 场景 | 控制器 | 接入示例 |
| --- | --- | --- |
| 一级分组，标题与子项类型不同 | `ExpandableListController<TGroup, TItem, TKey>` | `ExpandableList` |
| 多层树，节点通过统一类型表达 | `TreeListController<TNode, TKey>` | `TreeList`、`BusinessSortedTree`、`CallbackTree` |

简单页面可使用数据访问函数和控制器的 `OnItemRender`；需要复用整套数据访问、显示和清理逻辑时使用 Adapter。两种入口在构造时二选一，排序、展开和定位接口相同。红点、解锁、阅读状态及祖先汇总由业务维护，修改后统一 `Submit`。

详见[分组折叠](Documentation~/ExpandableLists.md)、[多层折叠](Documentation~/TreeLists.md)、[回调示例源码](Samples~/Basic/Scripts/Showcase/CallbackTreeDemo.cs)与[性能及 GC 边界](Documentation~/Performance.md)。

## 示例

| 场景 | 内容 |
| --- | --- |
| `VerticalList` | 1,000 条不同高度卡片、动画定位和尺寸修改 |
| `HorizontalList` | 1,000 条不同宽度卡片、横向滚动 |
| `InventoryGrid` | 503 个直接挂在 Content 下的格子、自动列数和选择 |
| `ItemDrag` | 独立拖拽手柄、交换物品和取消拖拽 |
| `CurvedScroll` | 圆弧、自定义曲线、切线旋转和虚拟化 |
| `RewardReveal` | 按行虚拟化的奖励道具逐个弹出 |
| `DirectRewardReveal` | 无行节点的奖励逐个弹出、重播和跳过 |
| `Chat` | 多种消息模板、换行测量、发送和回复 |
| `ExpandableList` | 分组折叠、业务排序、状态更新与阅读位置保持 |
| `TreeList` | 活动／章节／小节／任务四层折叠、同级排序与祖先链自动展开定位 |
| `BusinessSortedTree` | 红点／解锁／未读独立开关、ID 排序、祖先状态汇总与排序前后对照 |
| `CallbackTree` | OnItemRender 回调接入三层折叠树、未读排序、视图清理与隐藏课程定位 |
| `VerticalNestedHorizontal` | 垂直列表嵌套水平列表，两层均虚拟化 |
| `HorizontalNestedVertical` | 水平列表嵌套垂直列表，两层均虚拟化 |

全部场景保留可编辑的预制体和组件引用，支持拖拽、滚轮、惯性与边界回弹。导入示例后，菜单 **Tools → ZRList** 可以重新生成场景及预制体。

`VirtualGridView` 按行虚拟化；示例中的 `DirectGridView` 按格子直接复用。两者的布局和接入方式见[网格说明](Documentation~/Usage.md#背包网格)。

### 聊天列表（Chat）

多种消息模板、动态气泡高度，以及消息发送和回复。

![聊天列表：动态气泡高度、发送和回复](Documentation~/Images/chat.gif)

### 曲线列表（CurvedScroll）

圆弧与自定义曲线、切线旋转，配合虚拟化复用展示卡片。

![曲线列表：圆弧、自定义曲线和切线旋转](Documentation~/Images/curved.gif)

### 奖励逐个展示（DirectRewardReveal）

无行节点的奖励网格，支持逐个弹出、重播和跳过动画。

![奖励展示：逐个弹出、重播和跳过动画](Documentation~/Images/reveal.gif)

## 包结构

```text
Packages/com.zr.list/
  Runtime/          # 列表、网格、适配器、视图与复用池
  Samples~/Basic/   # 十四个可导入的示例及编辑器生成器
  Documentation~/  # API、接入方式与能力边界
  package.json
  README.md
  CHANGELOG.md
  LICENSE.md
  NOTICE
```

示例默认不会参与编译，导入后才进入用户工程。

## 文档与反馈

- [完整使用说明](Documentation~/Usage.md)
- [安装与升级](Documentation~/Upgrading.md)
- [分组折叠](Documentation~/ExpandableLists.md)
- [多层树与业务排序](Documentation~/TreeLists.md)
- [性能与 GC](Documentation~/Performance.md)
- [版本记录](CHANGELOG.md)
- [提交问题](https://github.com/zhanglinshuia-code/ZRList/issues)

## 许可证

Copyright 2026 zhanglinshuia-code。代码、文档和包内原创几何示例图标采用 [Apache License 2.0](LICENSE.md)。
示例字体使用 Unity 内置资源，uGUI 通过 Unity Package Manager 解析，各自遵循其原有许可。
