# Changelog

## 1.0.1 — 2026-10-07

- 最低支持版本调整为 Unity 2021.3 LTS，兼容 Unity 6。
- 十个示例在启动时根据输入后端配置 EventSystem，支持旧 Input Manager、新 Input System 和 Both；修复仅启用 Input System 时读取 UnityEngine.Input 的异常。
- Input System 保持可选依赖，提供示例用的链接保留配置，避免播放器构建裁剪动态加载的输入模块。
- 场景生成器同步使用自动输入配置，并兼容旧版 Unity 的 Arial 和新版 Unity 的 LegacyRuntime 内置字体。
- 补充安装升级与重新导入示例的说明。

## 1.0.0 — 2026-10-07

首次公开发布 ZRList。

- 提供独立的 `com.zr.list` UPM 包和 `ZRList.Runtime` 程序集。
- 支持横竖虚拟列表、多预制体复用、动态尺寸、批量更新、尾部追加和动画定位。
- 提供按行虚拟化网格、可见项查询、异步绑定校验与资源生命周期接口。
- 支持双向嵌套滚动、主轴手势分发、惯性和弹性边界。
- 附带十个可导入的示例、编辑器生成器与完整使用说明。
- 使用 Apache-2.0 许可证。
