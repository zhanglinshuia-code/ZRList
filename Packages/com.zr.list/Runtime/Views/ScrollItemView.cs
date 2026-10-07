// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList
{
    public class ScrollItemView
    {
        public RectTransform Root;
        // Business view cache belongs to this instance and survives recycling.
        public object CachedComponent;
        protected ContentSizeFitter SizeFitter { get; set; }
        public virtual int ItemIndex { get; set; }

        public int ViewIndex;
        public float ItemSize;
        internal long BindingVersion;
        internal IScrollItemAdapter OwnerAdapter;
        internal bool IsBound;
        internal RectTransform SourcePrefab;
        public RectTransform Prefab
        {
            get
            {
                return SourcePrefab;
            }
        }
        public ScrollItemView()
        {
        }

        public ScrollItemView(RectTransform root, int itemIndex, int viewIndex)
        {
            Root = root;
            ItemIndex = itemIndex;
            ViewIndex = viewIndex;
            SizeFitter = root.GetComponent<ContentSizeFitter>();
            if (SizeFitter != null) {
                SizeFitter.enabled = false;
            }
        }

        public virtual void CollectViews()
        {
        }

        public virtual void MarkForRebuild()
        {
            if (SizeFitter != null) {
                SizeFitter.enabled = true;
            }

            if (Root != null) {
                LayoutRebuilder.MarkLayoutForRebuild(Root);
            }
        }

        public virtual void UnmarkForRebuild()
        {
            if (SizeFitter != null) {
                SizeFitter.enabled = false;
            }
        }

        public virtual void ResetState()
        {
            ItemIndex = ViewIndex = -1;
            ItemSize = 0f;
            Root = null;
            CachedComponent = null;
            SizeFitter = null;
            BindingVersion = 0;
            OwnerAdapter = null;
            IsBound = false;
            SourcePrefab = null;
        }
    }
}
