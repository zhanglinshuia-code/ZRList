# 安装与升级到 1.0.3

ZRList 使用 Unity Package Manager 分发，包名为 `com.zr.list`，最低支持 Unity 2021.3 LTS。升级沿用原包名和运行库程序集 `ZRList.Runtime`，现有列表无需重新安装组件。

## 已安装项目通过 Update 更新

推荐使用跟踪主分支的安装地址：

```text
https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#main
```

1. 打开 **Window → Package Manager**，选择 **In Project** 中的 **ZRList**。
2. 点击 **Update**，等待 Git 下载、包解析和脚本编译完成。
3. 确认包详情中的版本为 **1.0.3**。项目的 `Packages/packages-lock.json` 会记录新提交；把该文件与 `Packages/manifest.json` 一起提交给团队。
4. 需要新示例时，再按下节导入 **ZRList Demos**。

如果当前 Unity 界面没有 Update 按钮，使用 **+ → Add package from git URL** 再次输入上述地址，会更新现有同名包，无需先卸载。发布新版本不会自动修改消费项目；项目需要主动执行更新。Unity 的 Git 更新行为见[官方更新说明](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-update.html)。

## 不同安装来源

| 当前来源 | 升级方式 |
| --- | --- |
| Git URL 以 `#main` 结尾 | 使用 Update，仍跟踪同一分支 |
| Git URL 没有 `#` 引用 | 更新跟踪仓库默认分支；本仓库默认分支为 main，也可重新添加明确的 `#main` 地址 |
| 固定 `#v1.0.0`、`#v1.0.1`、`#v1.0.2` 或提交 SHA | Update 仍解析原引用，不会选择新标签；重新添加 `#v1.0.3` 或 `#main` 地址 |
| Add package from disk / `file:` 本地目录 | 更新该本地目录的文件，或改用 Git URL；Package Manager 不会从 GitHub 更新本地副本 |
| 工程 `Packages/com.zr.list` 中的嵌入包 | 嵌入包优先于 Git 依赖；备份自定义内容并移出该目录后，再添加 Git URL |

固定本次发布使用：

```text
https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#v1.0.3
```

也可以在项目 `Packages/manifest.json` 的 `dependencies` 中配置：

```json
"com.zr.list": "https://github.com/zhanglinshuia-code/ZRList.git?path=/Packages/com.zr.list#main"
```

修改 manifest 后由 Unity 解析依赖；同一个分支的新提交需要通过 Update 或重新添加相同 URL 重新解析。不要把包的显示版本号与 Git 引用混为一谈：`package.json` 显示 1.0.3，实际下载哪个提交由安装 URL 和锁文件决定。详见 [Git 依赖锁定规则](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html#git-locks)。

## 更新示例

在包详情的 **Samples → ZRList Demos** 中导入新版，默认目录为 `Assets/Samples/ZRList/1.0.3/ZRList Demos`。包更新不会替换已经导入或复制到其他目录的旧示例。

如修改过旧示例，先把修改保存到版本控制或备份到 `Assets` 之外，再处理导入时的旧版本提示。不要在同一工程同时保留多份示例源码及同名 asmdef，否则可能发生重复程序集或类型定义。业务项目只使用运行库时，无需导入示例。

1.0.2 新增的四个折叠场景：

| 场景 | 接入方式与用途 |
| --- | --- |
| `ExpandableList.unity` | Adapter：一级分组折叠，组和子项可以使用不同类型 |
| `TreeList.unity` | Adapter：四层树、状态汇总、祖先链展开与定位 |
| `BusinessSortedTree.unity` | Adapter：红点、解锁、未读、ID 的业务排序交互 |
| `CallbackTree.unity` | OnItemRender：三层树、组件缓存、回收／销毁与未读排序 |

完整接入见[使用说明](Usage.md)、[分组折叠](ExpandableLists.md)和[多层折叠](TreeLists.md)。回调与 Adapter 在控制器构造时二选一；业务状态和比较规则由业务层提供。

## 1.0.2 到 1.0.3 的迁移

- 修复新版 Unity 中示例生成器、截图工具和场景回归的 `FindObjectOfType` 弃用警告。Unity 2022.2 及以上使用 `FindFirstObjectByType`，旧版本保留原 API，保持查找首个活动对象的行为。
- 本次仅调整示例编辑器工具和测试，运行库 API 与业务接入方式保持不变。
- **必须重新导入示例才能更新已复制的编辑器脚本**。如果警告路径仍包含 `Assets/Samples/ZRList/1.0.2`，说明编译的仍是旧副本；先备份自定义修改，再移除旧副本并导入 1.0.3。

## 1.0.1 到 1.0.2 的迁移

- 原有普通列表、网格及其渲染入口保持可用。折叠控制器与 `ApplyData` 是新增 API；已有项目按需接入。
- **移除公开工具类 `ZRList.GameObjectPool`**。运行库和包内示例没有依赖它；如果业务直接引用该类，需要迁移到自己的对象池实现。列表内置的模板视图池仍由 `VirtualScrollView` 管理，无需额外配置。
- 折叠控制器独占未初始化的 `VirtualScrollView`，要求 `FirstDataIndex = 0`。使用控制器后，通过 `Submit` 更新数据，不同时调用底层列表的 Initialize、ReloadData 或修改 ItemsCount。
- 回调接入时，在首次 Submit 前配置视图工厂及清理事件。销毁时调用控制器 `Dispose`，由其完成清理并解除引用，不提前退订清理事件。`OnItemRender` 不代表曝光或已读；导航测量也可能触发它。
- Adapter 接入时使用 `Bind`、`Unbind`、`DestroyView`，不再混用控制器的显示回调；控制器不负责销毁调用方的 Adapter 对象。

从 1.0.0 升级还包含 1.0.1 的输入兼容修复。重新导入新版示例；自定义旧场景可使用 `DemoInputModule` 自动选择项目启用的输入系统。

## 常见更新问题

- **仍显示旧版本**：检查 manifest 是否固定旧标签、旧提交或本地路径，并检查是否存在嵌入包；再执行 Update。仅重新打开项目会沿用锁文件，并不保证拉取新提交。
- **包已是 1.0.3，示例仍有旧警告或找不到新场景**：重新导入 Samples，并打开 1.0.3 目录；旧的 `Assets/Samples` 副本不会自动同步。
- **出现重复类型或程序集**：检查是否同时导入了多个版本的 ZRList Demos；保留一份源码，先备份自定义修改。
- **Git 下载失败**：检查 Unity 能否找到 Git，以及 GitHub 的网络访问；按 Package Manager 或 Editor 日志处理具体错误，无需删除整个 Library 或锁文件。
