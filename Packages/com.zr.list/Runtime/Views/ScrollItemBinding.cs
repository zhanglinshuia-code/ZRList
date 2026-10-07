// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    /// <summary>A binding snapshot for rejecting results from recycled views or old layouts.</summary>
    public readonly struct ScrollItemBinding
    {
        public readonly int DataIndex;
        public readonly int LayoutRevision;
        internal readonly int ViewIndex;
        internal readonly long Version;
        internal readonly RectTransform Root;
        internal ScrollItemBinding(ScrollItemView holder, int layoutRevision)
        {
            DataIndex = holder.ItemIndex;
            ViewIndex = holder.ViewIndex;
            Version = holder.BindingVersion;
            Root = holder.Root;
            LayoutRevision = layoutRevision;
        }
    }
}
