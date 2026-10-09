// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    /// <summary>
    /// 集中提供分组数据访问、视图绑定和资源清理，可在同类页面中复用。
    /// 数据访问和尺寸查询应无副作用；业务状态、汇总与排序规则由业务维护。
    /// 控制器负责调用视图生命周期；Adapter 对象由调用方管理，控制器释放时仅解除引用。
    /// </summary>
    public abstract class ExpandableListAdapter<TGroup, TItem, TKey>
    {
        /// <summary>尚未测量时的主轴估计长度，必须是有限正数。</summary>
        public abstract float EstimatedItemSize { get; }
        /// <summary>返回稳定、非空且全局唯一的分组 Key。</summary>
        public abstract TKey GetGroupKey(TGroup group);
        /// <summary>返回已有子项集合；空组返回空集合，避免临时创建列表。</summary>
        public abstract IReadOnlyList<TItem> GetItems(TGroup group);
        /// <summary>返回稳定、非空且跨组唯一的子项 Key；与分组 Key 属于不同命名空间。</summary>
        public abstract TKey GetItemKey(TItem item);
        /// <summary>选择当前行的预制体，不在这里实例化对象。</summary>
        public abstract RectTransform GetItemPrefab(ExpandableListRow<TGroup, TItem, TKey> row);
        /// <summary>
        /// 完整覆盖本次绑定的显示状态。导航的屏外测量也可能调用此方法，不代表曝光或阅读。
        /// 组件引用和按钮监听应在 CreateView 中缓存，避免每次绑定时创建闭包或拼接字符串。
        /// </summary>
        public abstract void Bind(ScrollItemView view, ExpandableListRow<TGroup, TItem, TKey> row, ScrollItemLayoutContext context);

        /// <summary>每个物理实例创建一次视图包装；可以返回缓存组件引用的 ScrollItemView 子类。</summary>
        public virtual ScrollItemView CreateView(RectTransform root)
        {
            return new ScrollItemView(root, -1, -1);
        }

        /// <summary>提供已知主轴长度；返回 false 时，在绑定后调用 MeasureItem。</summary>
        public virtual bool TryGetItemSize(ExpandableListRow<TGroup, TItem, TKey> row, ScrollItemLayoutContext context, out float size)
        {
            size = 0f;
            return false;
        }

        /// <summary>测量已绑定视图的主轴长度；默认使用 Unity 布局测量。</summary>
        public virtual float MeasureItem(ScrollItemView view, ExpandableListRow<TGroup, TItem, TKey> row, ScrollItemLayoutContext context)
        {
            return ScrollItemMeasurement.Measure(view.Root, context.Orientation);
        }

        /// <summary>
        /// 结束旧绑定，包括保留实例的刷新重绑；row 来自旧快照，业务对象本身不深拷贝。
        /// 可取消旧订阅或异步任务，但不要销毁 root 或清空组件缓存。
        /// </summary>
        public virtual void Unbind(ScrollItemView view, ExpandableListRow<TGroup, TItem, TKey> row)
        {
        }

        /// <summary>物理实例销毁前释放按钮监听等实例级资源；root 由列表销毁。</summary>
        public virtual void DestroyView(ScrollItemView view)
        {
        }
    }
}
