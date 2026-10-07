# ZRList

Unity uGUI 虚拟列表与网格。只创建和复用可见项，支持横向、竖向、动态尺寸、多种预制体、嵌套滚动和动画定位。

- Unity **2022.3+**；已在 **2022.3.5f1** 验证。
- UPM 包：`com.zr.list`，版本 **1.0.0**。
- 运行库仅依赖 `com.unity.ugui`。
- 许可证：[Apache-2.0](LICENSE.md)。

## 安装

Unity 菜单 **Window → Package Manager → + → Add package from git URL**，输入：

```text
https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#v1.0.0
```

也可以下载仓库，在 Package Manager 中选择 **Add package from disk**，打开 `Packages/com.zr.list/package.json`。

安装后展开包的 **Samples**，导入 **ZRList Demos**。打开导入目录中的 `Scenes` 场景即可运行；仓库提供 UPM 包，示例由 Unity 导入你的工程。

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
| `VerticalNestedHorizontal` | 垂直列表嵌套水平列表，两层均虚拟化 |
| `HorizontalNestedVertical` | 水平列表嵌套垂直列表，两层均虚拟化 |

全部场景保留可编辑的预制体和组件引用，支持拖拽、滚轮、惯性与边界回弹。导入示例后，菜单 **Tools → ZRList** 可以重新生成场景及预制体。

`VirtualGridView` 按行虚拟化；示例中的 `DirectGridView` 按格子直接复用。两者的布局和接入方式见[网格说明](Documentation~/Usage.md#背包网格)。

## 包结构

```text
Packages/com.zr.list/
  Runtime/          # 列表、网格、适配器、视图与复用池
  Samples~/Basic/   # 十个可导入的示例及编辑器生成器
  Documentation~/  # API、接入方式与能力边界
  package.json
  README.md
  CHANGELOG.md
  LICENSE.md
  NOTICE
```

示例默认不会参与编译，导入后才进入用户工程。开发工程、内部回归工具、Library、IDE 配置和构建产物不属于发布内容。

## 文档与反馈

- [完整使用说明](Documentation~/Usage.md)
- [版本记录](CHANGELOG.md)
- [提交问题](https://github.com/zhanglinshuia-code/ZRList/issues)

## 许可证

Copyright 2026 zhanglinshuia-code。代码、文档和包内原创几何示例图标采用 [Apache License 2.0](LICENSE.md)，与 [ZRAsset](https://github.com/zhanglinshuia-code/ZRAsset/blob/main/LICENSE) 一致。
示例字体使用 Unity 内置资源，uGUI 通过 Unity Package Manager 解析，各自遵循其原有许可。
