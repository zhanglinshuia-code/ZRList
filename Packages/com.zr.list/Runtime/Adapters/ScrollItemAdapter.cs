// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    /// <summary>Convenience base for strongly typed item views; override only the behavior you need.</summary>
    public abstract class ScrollItemAdapter<TView>: IScrollItemAdapter, IScrollItemViewLifecycle where TView : ScrollItemView
    {
        public abstract float EstimatedItemSize { get; }

        protected abstract TView CreateView(RectTransform root);
        protected abstract void BindView(TView view, int dataIndex, ScrollItemLayoutContext context);
        public virtual bool TryGetItemSize(int dataIndex, ScrollItemLayoutContext context, out float size)
        {
            size = 0f;
            return false;
        }

        protected virtual float MeasureView(TView view, int dataIndex, ScrollItemLayoutContext context)
        {
            return ScrollItemMeasurement.Measure(view.Root, context.Orientation);
        }

        protected virtual void UnbindView(TView view, int dataIndex)
        {
        }

        protected virtual void DestroyView(TView view)
        {
        }

        ScrollItemView IScrollItemAdapter.CreateViewsHolder(RectTransform root)
        {
            return CreateView(root);
        }

        void IScrollItemAdapter.Bind(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context)
        {
            BindView((TView)holder, dataIndex, context);
        }

        float IScrollItemAdapter.MeasureItem(ScrollItemView holder, int dataIndex, ScrollItemLayoutContext context)
        {
            return MeasureView((TView)holder, dataIndex, context);
        }

        void IScrollItemViewLifecycle.OnViewUnbound(ScrollItemView holder, int dataIndex)
        {
            UnbindView((TView)holder, dataIndex);
        }

        void IScrollItemViewLifecycle.OnViewDestroyed(ScrollItemView holder)
        {
            DestroyView((TView)holder);
        }
    }
}
