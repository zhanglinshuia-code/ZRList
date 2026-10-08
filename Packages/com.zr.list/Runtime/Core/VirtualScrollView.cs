// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ZRList
{
    [DisallowMultipleComponent, AddComponentMenu("UI/ZRList")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "VirtualScrollView")]
    public partial class VirtualScrollView: MonoBehaviour
    {
        [Tooltip("Item templates. One template uses index 0; multiple templates require a selector with InitializeWithPrefabIndex.")]
        public RectTransform[] ItemPrefabs = Array.Empty<RectTransform>();
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("_Content")]
        private RectTransform m_content;
        public RectTransform Content
        {
            get
            {
                return m_content;
            }

            set
            {
                m_content = value;
            }
        }

        [SerializeField, FormerlySerializedAs("viewport")]
        [UnityEngine.Serialization.FormerlySerializedAs("_Viewport")]
        private RectTransform m_viewport;
        public RectTransform Viewport
        {
            get
            {
                return m_viewport != null ? m_viewport : (ScrollRect != null && ScrollRect.viewport != null ? ScrollRect.viewport : transform as RectTransform);
            }

            set
            {
                m_viewport = value;
            }
        }

        [SerializeField, FormerlySerializedAs("mScrollRect")]
        [UnityEngine.Serialization.FormerlySerializedAs("_ScrollRect")]
        private VirtualScrollRect m_scrollRect;
        public VirtualScrollRect ScrollRect
        {
            get
            {
                return m_scrollRect;
            }

            set
            {
                m_scrollRect = value;
            }
        }

        [SerializeField, FormerlySerializedAs("contentPadding")]
        [UnityEngine.Serialization.FormerlySerializedAs("_ContentPadding")]
        private RectOffset m_contentPadding = new RectOffset();
        public RectOffset ContentPadding
        {
            get
            {
                return m_contentPadding;
            }

            set
            {
                m_contentPadding = value ?? new RectOffset();
            }
        }

        [SerializeField, FormerlySerializedAs("contentSpacing"), Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("_ContentSpacing")]
        private float m_contentSpacing;
        public float ContentSpacing
        {
            get
            {
                return m_contentSpacing;
            }

            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                m_contentSpacing = Mathf.Max(0f, value);
            }
        }

        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("_Orientation")]
        private ScrollOrientation m_orientation;
        public ScrollOrientation Orientation
        {
            get
            {
                return m_orientation;
            }

            set
            {
                m_orientation = value;
            }
        }

        public bool IsHorizontal
        {
            get
            {
                return Orientation == ScrollOrientation.Horizontal;
            }
        }

        [Tooltip("Fit the item across the viewport after subtracting cross-axis padding. Disabled preserves prefab size.")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("stretchItems")]
        private bool m_stretchItems;
        public bool StretchItems
        {
            get
            {
                return m_stretchItems;
            }

            set
            {
                m_stretchItems = value;
            }
        }

        public int ScrollAxisIndex { get; private set; }
        public int ScrollAxisSign { get; private set; }

        private int m_crossAxisIndex;
        private float m_viewportLength;
        private float m_halfContentSpacing;
        public float StartPadding { get; private set; }
        public double EndPadding { get; private set; }
        public float CrossAxisStartPadding { get; private set; }
        public double CrossAxisEndPadding { get; private set; }

        [UnityEngine.Serialization.FormerlySerializedAs("realIndexOfFirstItemInView")]
        public int FirstDataIndex;
        [UnityEngine.Serialization.FormerlySerializedAs("itemsCount")]
        public int ItemsCount;
        [UnityEngine.Serialization.FormerlySerializedAs("mInputField")]
        public InputField JumpIndexInput;
        [UnityEngine.Serialization.FormerlySerializedAs("mTargetValue")]
        public float JumpTargetOffset;
        [UnityEngine.Serialization.FormerlySerializedAs("mNeedMove")]
        public bool IsAnimatingJump;
        [UnityEngine.Serialization.FormerlySerializedAs("JumpItemIndexInView")]
        public int JumpViewIndex = -1;
        [UnityEngine.Serialization.FormerlySerializedAs("speed")]
        public float JumpElapsedTime = 10f;
        [UnityEngine.Serialization.FormerlySerializedAs("dur")]
        public float DefaultJumpDuration = 0.5f;
        public readonly List<ScrollItemView> VisibleItemViews = new List<ScrollItemView>();
        public readonly Dictionary<int, ScrollItemView> VisibleItemsByViewIndex = new Dictionary<int, ScrollItemView>();
        // Legacy inspection surface. Pool membership and ordering are owned by
        // the list; use TrimPool to release instances rather than editing it.
        protected internal readonly List<ScrollItemView> RecycledItemViews = new List<ScrollItemView>();
        private IReadOnlyList<ScrollItemView> m_readOnlyVisibleItems;
        /// <summary>Live, read-only collection of rendered views in layout order. Use GetVisibleItems for viewport clipping.</summary>
        public IReadOnlyList<ScrollItemView> VisibleItems
        {
            get
            {
                return m_readOnlyVisibleItems ?? (m_readOnlyVisibleItems = VisibleItemViews.AsReadOnly());
            }
        }

        public bool IsInitialized
        {
            get
            {
                return m_initialized;
            }
        }

        public int ItemCount
        {
            get
            {
                return m_sizeIndex.Count;
            }
        }

        public int PooledViewCount
        {
            get
            {
                return RecycledItemViews.Count;
            }
        }

        public bool IsJumping
        {
            get
            {
                return IsAnimatingJump || m_queuedJumpIndex >= 0;
            }
        }

        public float ScrollOffset
        {
            get
            {
                return m_initialized ? Offset : 0f;
            }
        }

        public int LayoutRevision
        {
            get
            {
                return m_layoutRevision;
            }
        }

        public ScrollItemLayoutContext LayoutContext
        {
            get
            {
                return m_layoutContext;
            }
        }

        private readonly ItemSizeIndex m_sizeIndex = new ItemSizeIndex();
        private readonly Dictionary<int, ScrollItemView> m_previousVisible = new Dictionary<int, ScrollItemView>();
        private readonly List<int> m_recycleIndices = new List<int>();
        private readonly struct SizeChange
        {
            public readonly float Size;
            public readonly ScrollItemBinding? Binding;
            public SizeChange(float size, ScrollItemBinding? binding = null)
            {
                Size = size;
                Binding = binding;
            }
        }

        private Dictionary<int, SizeChange> m_pendingSizes = new Dictionary<int, SizeChange>();
        private Dictionary<int, SizeChange> m_committingSizes = new Dictionary<int, SizeChange>();
        private readonly HashSet<int> m_dirtyItems = new HashSet<int>();
        private IScrollItemAdapter m_adapter;
        private bool m_initialized;
        private bool m_rebuilding;
        private bool m_flushing;
        private bool m_refreshAll;
        private bool m_deferredLayoutRefresh;
        private bool m_preparingLayout;
        private int m_updateDepth;
        private long m_updateGeneration;
        private long m_bindingVersion;
        private int m_layoutRevision;
        private ScrollItemLayoutContext m_layoutContext;
        private Vector2 m_layoutViewportSize;
        private ScrollOrientation m_layoutOrientation;
        private Vector4 m_layoutPadding;
        private float m_layoutSpacing;
        private bool m_layoutStretch;
        private int m_layoutDataOffset;
        private float m_previousContentPosition;
        private VirtualScrollRect m_listeningScrollRect;
        private InputField m_listeningInputField;
        private ScrollRect.ScrollRectEvent m_scrollChangedEvent;
        private InputField.OnChangeEvent m_indexChangedEvent;
        private int m_activeJumpIndex = -1;
        private bool m_jumpAtEnd;
        private float m_jumpDuration;
        private bool m_inertiaBeforeJump;
        private float m_decelerationRate;
        private int m_queuedJumpIndex = -1;
        private float m_queuedJumpDuration;
        private bool m_queuedJumpIsDataIndex;
        private bool m_runningQueuedJump;
        private int m_lifecycleCallbackDepth;
        private VirtualScrollRect m_jumpScrollRect;
        private RectTransform m_poolRoot;
        private RectTransform m_layoutFirstItemPrefab;
        private RectTransform m_layoutContent;
        private RectTransform m_layoutViewport;
        private VirtualScrollRect m_layoutScrollRect;
        private bool LayoutIsHorizontal
        {
            get
            {
                return m_layoutOrientation == ScrollOrientation.Horizontal;
            }
        }

        private RectTransform FirstItemPrefab
        {
            get
            {
                return ItemPrefabs != null && ItemPrefabs.Length > 0 ? ItemPrefabs[0] : null;
            }
        }

        private float Offset
        {
            get
            {
                return Content.anchoredPosition[ScrollAxisIndex] * -ScrollAxisSign;
            }
        }

        private float MaxOffset
        {
            get
            {
                return Mathf.Max(0f, Content.rect.size[ScrollAxisIndex] - m_viewportLength);
            }
        }

        private Vector4 PaddingSnapshot
        {
            get
            {
                return new Vector4(ContentPadding.left, ContentPadding.right, ContentPadding.top, ContentPadding.bottom);
            }
        }

        public void Init(Action<GameObject, int> callback)
        {
            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Initialize outside item callbacks.");
            }

            if (callback == null) {
                throw new ArgumentNullException(nameof(callback));
            }

            ScrollItemPrefabCollection.Validate(ItemPrefabs, null);
            RectTransform prefab = ItemPrefabs[0];
            float estimate = prefab.rect.size[IsHorizontal ? 0 : 1];
            if (!IsValidSize(estimate)) {
                estimate = ScrollItemMeasurement.Measure(prefab, Orientation);
            }

            Init(new LegacyScrollItemAdapter(callback, IsValidSize(estimate) ? estimate : 1f));
        }

        public void Init(IScrollItemAdapter itemAdapter)
        {
            InitializeCore(itemAdapter, Mathf.Max(0, ItemsCount));
        }

        private void InitializeCore(IScrollItemAdapter itemAdapter, int itemCount)
        {
            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Initialize after the current item callback returns.");
            }

            if (itemAdapter == null) {
                throw new ArgumentNullException(nameof(itemAdapter));
            }

            if (Content == null || ScrollRect == null || Viewport == null) {
                throw new InvalidOperationException("Assign the content and scroll rect before initializing.");
            }

            if (itemAdapter is not IScrollItemPrefabProvider) {
                ScrollItemPrefabCollection.Validate(ItemPrefabs, null);
            }

            if (!IsValidSize(itemAdapter.EstimatedItemSize)) {
                throw new ArgumentOutOfRangeException(nameof(itemAdapter), "Estimated size must be finite and positive.");
            }

            CancelJump();
            ScrollRect.StopMovement();
            if (!AreListenersCurrent) {
                DetachListeners();
            }
            m_initialized = false;
            bool canReuse = ReferenceEquals(m_adapter, itemAdapter) || (m_adapter is LegacyScrollItemAdapter oldLegacy && itemAdapter is LegacyScrollItemAdapter newLegacy && oldLegacy.CanReuseViews(newLegacy));
            if (m_adapter != null && (!canReuse || m_layoutFirstItemPrefab != FirstItemPrefab || m_layoutContent != Content)) {
                DestroyOwnedViews();
            }
            else {
                RecycleAllVisible();
            }

            if (!ReferenceEquals(m_adapter, itemAdapter) && ReferenceEquals(m_adapter, m_callbackAdapter)) {
                m_callbackAdapter?.ClearPrefabSelection();
                m_usesConfiguredPrefabs = false;
            }

            m_adapter = itemAdapter;
            m_layoutFirstItemPrefab = FirstItemPrefab;
            ResetDataState(itemCount);
            ConfigureLayout();
            ResetSizes();
            ResetScrollPosition();

            m_initialized = true;
            try {
                RenderAtOffset(0f, true);
                FlushPendingUpdates();
                SubscribeListeners();
            }
            catch {
                Dispose();
                throw;
            }
        }

        public void Initialize(IScrollItemAdapter itemAdapter, int itemCount)
        {
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Initialize outside item callbacks.");
            }

            InitializeCore(itemAdapter, itemCount);
        }

        public void ReloadData(int itemCount)
        {
            if (!m_initialized) {
                throw new InvalidOperationException("Initialize before reloading data.");
            }

            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (m_rebuilding || m_flushing || m_preparingLayout || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Reload data outside item callbacks.");
            }

            CancelJump();
            ScrollRect.StopMovement();
            bool layoutChanged = HasLayoutChanged();
            try {
                m_rebuilding = true;
                try {
                    if (m_layoutFirstItemPrefab != FirstItemPrefab || m_layoutContent != Content) {
                        DestroyOwnedViews();
                    }
                    else {
                        RecycleAllVisible();
                    }

                    m_layoutFirstItemPrefab = FirstItemPrefab;
                    ResetDataState(itemCount);
                    if (layoutChanged) {
                        ConfigureLayout();
                    }
                    else {
                        RefreshCallbackPrefab();
                    }

                    ResetSizes();
                    ResetScrollPosition();
                }
                finally {
                    m_rebuilding = false;
                }

                RenderAtOffset(0f, true);
                FlushPendingUpdates();
                if (isActiveAndEnabled && !AreListenersCurrent) {
                    SubscribeListeners();
                }
            }
            catch {
                Dispose();
                throw;
            }
        }

        private bool HasLayoutChanged()
        {
            return m_layoutFirstItemPrefab != FirstItemPrefab || m_layoutContent != Content || m_layoutViewport != Viewport || m_layoutScrollRect != ScrollRect || Viewport.rect.size != m_layoutViewportSize || Orientation != m_layoutOrientation || PaddingSnapshot != m_layoutPadding || ContentSpacing != m_layoutSpacing || StretchItems != m_layoutStretch || FirstDataIndex != m_layoutDataOffset;
        }

        private void ResetScrollPosition()
        {
            Vector2 position = Content.anchoredPosition;
            ScrollRect.CompensateContentPosition(0, -position.x);
            ScrollRect.CompensateContentPosition(1, -position.y);
            Content.anchoredPosition = Vector2.zero;
            m_previousContentPosition = 0f;
        }

        // Only a check with no reconfiguration can be reused by the immediately
        // following internal operation. Reconfiguration may invoke business code.
        internal bool EnsureLayoutCurrent()
        {
            using var profileScope = s_layoutCheckMarker.Auto();
            if (!m_initialized || m_rebuilding || m_flushing || m_preparingLayout || m_updateDepth > 0) {
                return false;
            }

            if (ItemsCount != m_sizeIndex.Count || m_layoutContent != Content || m_layoutScrollRect != ScrollRect) {
                Init(m_adapter);
                return false;
            }
            else if (HasLayoutChanged() || ConfiguredPrefabsChanged) {
                RefreshLayout();
                return false;
            }

            return true;
        }

        private void ConfigureLayout(bool forceRevision = true)
        {
            InvalidateRenderCache();
            if (m_adapter is not IScrollItemPrefabProvider) {
                ScrollItemPrefabCollection.Validate(ItemPrefabs, null);
            }

            bool prefabsChanged = ConfiguredPrefabsChanged || m_layoutFirstItemPrefab != FirstItemPrefab;
            RefreshCallbackPrefab();
            m_contentPadding ??= new RectOffset();
            if (float.IsNaN(m_contentSpacing) || float.IsInfinity(m_contentSpacing)) {
                throw new InvalidOperationException("Spacing must be finite.");
            }

            m_contentSpacing = Mathf.Max(0f, m_contentSpacing);
            ScrollRect.content = Content;
            if (m_viewport != null) {
                ScrollRect.viewport = m_viewport;
            }

            ScrollRect.horizontal = IsHorizontal;
            ScrollRect.vertical = !IsHorizontal;
            ScrollAxisIndex = IsHorizontal ? 0 : 1;
            m_crossAxisIndex = 1 - ScrollAxisIndex;
            ScrollAxisSign = IsHorizontal ? 1 : -1;
            m_viewportLength = Viewport.rect.size[ScrollAxisIndex];
            StartPadding = IsHorizontal ? ContentPadding.left : ContentPadding.top;
            EndPadding = IsHorizontal ? ContentPadding.right : ContentPadding.bottom;
            CrossAxisStartPadding = IsHorizontal ? ContentPadding.top : ContentPadding.left;
            CrossAxisEndPadding = IsHorizontal ? ContentPadding.bottom : ContentPadding.right;
            m_halfContentSpacing = ContentSpacing / 2f;
            m_layoutViewportSize = Viewport.rect.size;
            m_layoutOrientation = Orientation;
            m_layoutPadding = PaddingSnapshot;
            m_layoutSpacing = ContentSpacing;
            m_layoutStretch = StretchItems;
            m_layoutDataOffset = FirstDataIndex;
            m_layoutContent = Content;
            m_layoutViewport = Viewport;
            m_layoutScrollRect = ScrollRect;
            m_layoutFirstItemPrefab = FirstItemPrefab;
            float available = Mathf.Max(1f, Viewport.rect.size[m_crossAxisIndex] - CrossAxisStartPadding - (float)CrossAxisEndPadding);
            float itemCrossSize = StretchItems || FirstItemPrefab == null ? available : Mathf.Max(1f, FirstItemPrefab.rect.size[m_crossAxisIndex]);
            if (forceRevision || prefabsChanged || m_layoutContext.Orientation != Orientation || m_layoutContext.AvailableCrossSize != available || m_layoutContext.ItemCrossSize != itemCrossSize) {
                ++m_layoutRevision;
            }

            m_layoutContext = new ScrollItemLayoutContext(Orientation, available, itemCrossSize, m_layoutRevision);
            if (ItemPrefabs != null) {
                foreach (RectTransform prefab in ItemPrefabs) {
                    if (prefab != null && prefab.gameObject.scene.IsValid()) {
                        prefab.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void ScrollRectValueChanged(Vector2 value)
        {
            if (!isActiveAndEnabled) {
                return;
            }

            EnsureLayoutCurrent();
            if (!m_initialized || !isActiveAndEnabled || m_rebuilding) {
                return;
            }

            if (m_previousContentPosition == Content.anchoredPosition[ScrollAxisIndex]) {
                return;
            }

            RenderAtOffset(Offset);
            FlushPendingUpdates();
        }

        public void RefreshVisibleItems()
        {
            bool layoutCurrent = EnsureLayoutCurrent();
            if (!m_initialized) {
                return;
            }

            foreach (ScrollItemView item in VisibleItemViews) {
                m_pendingSizes.Remove(item.ViewIndex);
                m_committingSizes.Remove(item.ViewIndex);
            }
            m_refreshAll = true;
            FlushPendingUpdates(layoutCurrent);
        }

        public void RefreshLayout()
        {
            if (!m_initialized || m_preparingLayout || m_rebuilding) {
                return;
            }

            if (m_updateDepth > 0) {
                m_deferredLayoutRefresh = true;
                return;
            }

            m_deferredLayoutRefresh = false;
            m_preparingLayout = true;
            try {
                FlushPendingUpdates();
                ViewAnchor anchor = CaptureAnchor();
                CancelActiveJump();
                m_dirtyItems.Clear();
                int previousRevision = m_layoutRevision;
                int previousDataOffset = m_layoutDataOffset;
                float previousSpacing = m_layoutSpacing;
                ConfigureLayout(false);
                if (m_layoutRevision != previousRevision || previousDataOffset != m_layoutDataOffset) {
                    ResetSizes();
                }
                else {
                    // Main-axis viewport changes and spacing do not change item measurements.
                    if (previousSpacing != ContentSpacing) {
                        m_sizeIndex.ChangeSpacing(ContentSpacing);
                    }

                    UpdateContentSize();
                }

                Vector2 pos = Content.anchoredPosition;
                pos[m_crossAxisIndex] = 0f;
                Content.anchoredPosition = pos;
                float offset = anchor.Valid ? GetEstimatedItemStart(anchor.Index) - anchor.Inset : 0f;
                RenderAtOffset(ConstrainUpdatedOffset(offset), true);
                FlushPendingUpdates();
            }
            finally {
                m_preparingLayout = false;
            }

            RunQueuedJump();
            NotifyViewStateChanged();
        }

        // Compatibility entry points; boundary layout now uses the same size index.
        public void UpdateBoundsForBottom()
        {
            if (m_initialized && !m_rebuilding && !IsAnimatingJump) {
                RenderAtOffset(Offset);
                NotifyViewStateChanged();
            }
        }

        public void UpdateTopPos()
        {
            if (m_initialized && !m_rebuilding && !IsAnimatingJump) {
                RenderAtOffset(Offset);
                NotifyViewStateChanged();
            }
        }

        private void OnIndexInputChanged(string value)
        {
            if (!m_initialized || !isActiveAndEnabled) {
                return;
            }

            JumpViewIndex = int.TryParse(value, out int index) ? index : -1;
        }

        private bool AreListenersCurrent
        {
            get
            {
                return m_listeningScrollRect == ScrollRect && m_listeningInputField == JumpIndexInput &&
                    ReferenceEquals(m_scrollChangedEvent, ScrollRect.onValueChanged) &&
                    ReferenceEquals(m_indexChangedEvent, JumpIndexInput != null ? JumpIndexInput.onValueChanged : null);
            }
        }

        private void SubscribeListeners()
        {
            if (AreListenersCurrent) {
                return;
            }

            DetachListeners();
            m_listeningScrollRect = ScrollRect;
            m_scrollChangedEvent = ScrollRect.onValueChanged;
            m_scrollChangedEvent.AddListener(ScrollRectValueChanged);
            m_listeningInputField = JumpIndexInput;
            if (m_listeningInputField != null) {
                m_indexChangedEvent = JumpIndexInput.onValueChanged;
                m_indexChangedEvent.AddListener(OnIndexInputChanged);
            }
        }

        private void DetachListeners()
        {
            if (m_scrollChangedEvent != null) {
                m_scrollChangedEvent.RemoveListener(ScrollRectValueChanged);
            }

            if (m_indexChangedEvent != null) {
                m_indexChangedEvent.RemoveListener(OnIndexInputChanged);
            }

            m_listeningScrollRect = null;
            m_listeningInputField = null;
            m_scrollChangedEvent = null;
            m_indexChangedEvent = null;
        }

        private void Update()
        {
            EnsureLayoutCurrent();
            if (m_initialized) {
                AdvanceJump(Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!m_initialized || m_rebuilding) {
                return;
            }

            FlushPendingUpdates(EnsureLayoutCurrent());
        }

        private void OnEnable()
        {
            if (!m_initialized) {
                return;
            }

            SubscribeListeners();
            RefreshVisibleItems();
        }

        private void OnDisable()
        {
            CancelJump();
            // Pooled nested lists are disabled on every recycle. Keep their event
            // subscription until reconfiguration/disposal; handlers ignore disabled views.
        }

        private void DestroyOwnedViews()
        {
            Exception firstError = null;
            try {
                RecycleAllVisible();
            }
            catch (Exception error) {
                firstError = error;
            }

            while (RecycledItemViews.Count > 0) {
                int last = RecycledItemViews.Count - 1;
                ScrollItemView holder = RemovePooledHolder(last);
                try {
                    DestroyHolder(holder);
                }
                catch (Exception error) {
                    if (firstError == null) {
                        firstError = error;
                    }
                }
            }

            m_poolsByPrefab.Clear();
            if (m_poolRoot != null) {
                ReleaseRoot(m_poolRoot);
            }

            m_poolRoot = null;
            if (firstError != null) {
                throw firstError;
            }
        }

        private void DestroyHolder(ScrollItemView holder)
        {
            RectTransform ownedRoot = holder.Root;
            var lifecycle = holder.OwnerAdapter as IScrollItemViewLifecycle;
            try {
                UnbindHolder(holder);
                if (lifecycle != null) {
                    ++m_lifecycleCallbackDepth;
                    try {
                        lifecycle.OnViewDestroyed(holder);
                    }
                    finally {
                        --m_lifecycleCallbackDepth;
                    }
                }
            }
            finally {
                // A business destroy callback may clear its wrapper references.
                if (ownedRoot != null) {
                    ReleaseRoot(ownedRoot);
                }

                holder.ResetState();
            }
        }

        private void ReleaseRoot(RectTransform root)
        {
            root.gameObject.SetActive(false);
            if (Application.isPlaying) {
                Destroy(root.gameObject);
            }
            else {
                DestroyImmediate(root.gameObject);
            }
        }

        public void TrimPool(int retainedCount = 0)
        {
            if (retainedCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(retainedCount));
            }

            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Trim the pool outside item callbacks.");
            }

            try {
                while (RecycledItemViews.Count > retainedCount) {
                    int last = RecycledItemViews.Count - 1;
                    ScrollItemView holder = RemovePooledHolder(last);
                    DestroyHolder(holder);
                }
            }
            finally {
                PruneEmptyPrefabPools();
            }

            NotifyViewStateChanged();
        }

        public void Dispose()
        {
            if (m_rebuilding || m_flushing || m_lifecycleCallbackDepth > 0) {
                throw new InvalidOperationException("Dispose outside item callbacks.");
            }

            CancelJump();
            m_initialized = false;
            DetachListeners();
            try {
                DestroyOwnedViews();
            }
            finally {
                m_pendingSizes.Clear();
                m_committingSizes.Clear();
                m_dirtyItems.Clear();
                m_sizeIndex.Clear();
                if (ReferenceEquals(m_adapter, m_callbackAdapter)) {
                    m_callbackAdapter?.ClearPrefabSelection();
                    m_usesConfiguredPrefabs = false;
                }
                m_adapter = null;
                m_updateDepth = 0;
                ++m_updateGeneration;
                m_refreshAll = m_deferredLayoutRefresh = false;
                NotifyViewStateChanged();
            }
        }

        private void OnDestroy()
        {
            try {
                Dispose();
            }
            catch (Exception error) {
                Debug.LogException(error, this);
            }
        }
    }
}
