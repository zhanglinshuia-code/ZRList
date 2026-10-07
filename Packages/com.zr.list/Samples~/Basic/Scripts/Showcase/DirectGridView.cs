// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZRList.Samples
{
    /// <summary>
    /// A fixed-size vertical grid sample. Every pooled cell is a direct child of
    /// Content; row/column numbers are only used to calculate cell positions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DirectGridView: MonoBehaviour, IBeginDragHandler, IScrollHandler
    {
        public ScrollRect ScrollRect;
        public RectTransform CellPrefab;
        [Min(0)]
        public int ColumnCount;
        public Vector2 CellSize = new Vector2(144f, 144f);
        public Vector2 CellSpacing = new Vector2(12f, 12f);
        [SerializeField]
        private RectOffset m_contentPadding;
        public RectOffset ContentPadding
        {
            get { return m_contentPadding ??= new RectOffset(20, 20, 20, 20); }
            set { m_contentPadding = value; }
        }

        public event Action<ScrollItemView, int> OnItemRender;
        public event Action<ScrollItemView, int> OnItemRecycle;
        public event Action ViewStateChanged;

        private readonly Dictionary<int, ScrollItemView> m_visible = new Dictionary<int, ScrollItemView>();
        private readonly List<ScrollItemView> m_cells = new List<ScrollItemView>();
        private readonly Stack<ScrollItemView> m_pool = new Stack<ScrollItemView>();
        private RectTransform m_configuredPrefab;
        private Vector2 m_viewportSize;
        private Vector2 m_configuredCellSize;
        private Vector2 m_configuredSpacing;
        private Vector4 m_configuredPadding;
        private int m_configuredColumns;
        private float m_jumpFrom;
        private float m_jumpTo;
        private float m_jumpDuration;
        private float m_jumpElapsed;
        private bool m_refreshing;

        public bool IsInitialized { get; private set; }
        public int ItemCount { get; private set; }
        public int ResolvedColumnCount { get; private set; }
        public bool IsJumping { get; private set; }
        public RectTransform Content { get { return ScrollRect.content; } }
        public RectTransform Viewport { get { return ScrollRect.viewport; } }
        public int VisibleCellCount { get { return m_visible.Count; } }

        public void Initialize(int itemCount)
        {
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }
            ValidateConfiguration();
            ScrollRect.horizontal = false;
            ScrollRect.vertical = true;
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = Vector2.one;
            Content.pivot = new Vector2(0f, 1f);
            Content.sizeDelta = new Vector2(0f, Content.sizeDelta.y);
            ScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            ScrollRect.onValueChanged.AddListener(OnScrollChanged);
            IsInitialized = true;
            ReloadData(itemCount);
        }

        public void ReloadData(int itemCount)
        {
            if (!IsInitialized) {
                throw new InvalidOperationException("Initialize the grid first.");
            }
            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }
            ValidateConfiguration();
            ItemCount = itemCount;
            IsJumping = false;
            ScrollRect.StopMovement();
            Content.anchoredPosition = Vector2.zero;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (!IsInitialized || m_refreshing) {
                return;
            }
            ValidateConfiguration();
            int firstItem = ResolvedColumnCount == 0 ? 0 : Mathf.Max(0, Mathf.FloorToInt((Content.anchoredPosition.y - m_configuredPadding.z) / (m_configuredCellSize.y + m_configuredSpacing.y))) * ResolvedColumnCount;
            IsJumping = false;
            ScrollRect.StopMovement();
            RecycleAll();
            if (m_configuredPrefab != CellPrefab) {
                DestroyPool();
            }
            m_configuredPrefab = CellPrefab;
            m_viewportSize = Viewport.rect.size;
            m_configuredColumns = ColumnCount;
            m_configuredCellSize = CellSize;
            m_configuredSpacing = CellSpacing;
            m_configuredPadding = GetPadding();
            float width = Mathf.Max(0f, m_viewportSize.x - ContentPadding.horizontal);
            ResolvedColumnCount = ColumnCount > 0 ? ColumnCount : Mathf.Max(1, Mathf.FloorToInt((width + CellSpacing.x) / (CellSize.x + CellSpacing.x)));
            int bands = ItemCount == 0 ? 0 : (ItemCount - 1) / ResolvedColumnCount + 1;
            float height = ContentPadding.vertical + bands * CellSize.y + Mathf.Max(0, bands - 1) * CellSpacing.y;
            Content.sizeDelta = new Vector2(0f, Mathf.Max(m_viewportSize.y, height));
            float offset = ItemCount == 0 ? 0f : Mathf.Min(firstItem, ItemCount - 1) / ResolvedColumnCount * (CellSize.y + CellSpacing.y);
            SetOffset(offset);
        }

        public bool JumpToItem(int dataIndex, float duration = 0f)
        {
            EnsureLayoutCurrent();
            if (!IsInitialized || dataIndex < 0 || dataIndex >= ItemCount) {
                return false;
            }
            ScrollRect.StopMovement();
            m_jumpFrom = Content.anchoredPosition.y;
            m_jumpTo = Mathf.Clamp(dataIndex / ResolvedColumnCount * (CellSize.y + CellSpacing.y), 0f, MaxOffset);
            m_jumpDuration = duration;
            m_jumpElapsed = 0f;
            IsJumping = duration > 0f;
            if (!IsJumping) {
                SetOffset(m_jumpTo);
            }
            return true;
        }

        public bool TryGetVisibleItem(int dataIndex, out ScrollItemView cell)
        {
            EnsureLayoutCurrent();
            return m_visible.TryGetValue(dataIndex, out cell);
        }

        public bool RefreshItem(int dataIndex)
        {
            EnsureLayoutCurrent();
            if (!IsInitialized || dataIndex < 0 || dataIndex >= ItemCount) {
                return false;
            }
            if (m_visible.TryGetValue(dataIndex, out ScrollItemView cell)) {
                OnItemRender?.Invoke(cell, dataIndex);
            }
            return true;
        }

        private float MaxOffset { get { return Mathf.Max(0f, Content.rect.height - Viewport.rect.height); } }

        private Vector4 GetPadding()
        {
            return new Vector4(ContentPadding.left, ContentPadding.right, ContentPadding.top, ContentPadding.bottom);
        }

        private void ValidateConfiguration()
        {
            if (ScrollRect == null || ScrollRect.content == null || ScrollRect.viewport == null || CellPrefab == null) {
                throw new InvalidOperationException("Assign a ScrollRect with Content and Viewport, and a cell prefab.");
            }
            if (ColumnCount < 0 || !IsFinitePositive(CellSize.x) || !IsFinitePositive(CellSize.y) || !IsFinitePositive(CellSpacing.x + 1f) || !IsFinitePositive(CellSpacing.y + 1f) || CellSpacing.x < 0f || CellSpacing.y < 0f || ContentPadding == null || ContentPadding.left < 0 || ContentPadding.right < 0 || ContentPadding.top < 0 || ContentPadding.bottom < 0) {
                throw new ArgumentOutOfRangeException(nameof(CellSize), "Use finite positive cell sizes and nonnegative spacing, padding and columns.");
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void EnsureLayoutCurrent()
        {
            if (IsInitialized && (Viewport.rect.size != m_viewportSize || CellPrefab != m_configuredPrefab || ColumnCount != m_configuredColumns || CellSize != m_configuredCellSize || CellSpacing != m_configuredSpacing || GetPadding() != m_configuredPadding)) {
                RefreshLayout();
            }
        }

        private void SetOffset(float offset)
        {
            Content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, MaxOffset));
            RefreshVisibleCells();
        }

        private void RefreshVisibleCells()
        {
            if (!IsInitialized || m_refreshing) {
                return;
            }
            m_refreshing = true;
            try {
                float offset = Mathf.Clamp(Content.anchoredPosition.y, 0f, MaxOffset);
                float stride = CellSize.y + CellSpacing.y;
                int firstBand = Mathf.Max(0, Mathf.FloorToInt((offset - ContentPadding.top) / stride));
                int endBand = Mathf.Max(firstBand + 1, Mathf.CeilToInt((offset + Viewport.rect.height - ContentPadding.top) / stride));
                int first = (int)Math.Min(ItemCount, (long)firstBand * ResolvedColumnCount);
                int end = (int)Math.Min(ItemCount, (long)endBand * ResolvedColumnCount);

                // Release off-screen cells before acquiring their replacements.
                for (int index = m_cells.Count - 1; index >= 0; --index) {
                    ScrollItemView cell = m_cells[index];
                    if (cell.ItemIndex < first || cell.ItemIndex >= end) {
                        RecycleCell(cell);
                        m_cells.RemoveAt(index);
                    }
                }
                for (int dataIndex = first; dataIndex < end; ++dataIndex) {
                    if (m_visible.ContainsKey(dataIndex)) {
                        continue;
                    }
                    ScrollItemView cell;
                    if (m_pool.Count > 0) {
                        cell = m_pool.Pop();
                    }
                    else {
                        RectTransform root = Instantiate(CellPrefab, Content, false);
                        root.gameObject.SetActive(false);
                        cell = new ScrollItemView(root, -1, -1);
                    }
                    cell.ItemIndex = cell.ViewIndex = dataIndex;
                    cell.ItemSize = CellSize.y;
                    cell.Root.anchorMin = cell.Root.anchorMax = new Vector2(0f, 1f);
                    cell.Root.pivot = new Vector2(0f, 1f);
                    cell.Root.sizeDelta = CellSize;
                    cell.Root.anchoredPosition = new Vector2(ContentPadding.left + dataIndex % ResolvedColumnCount * (CellSize.x + CellSpacing.x), -ContentPadding.top - dataIndex / ResolvedColumnCount * stride);
                    m_visible.Add(dataIndex, cell);
                    m_cells.Add(cell);
                    OnItemRender?.Invoke(cell, dataIndex);
                    cell.Root.gameObject.SetActive(true);
                }
            }
            finally {
                m_refreshing = false;
            }
            ViewStateChanged?.Invoke();
        }

        private void RecycleCell(ScrollItemView cell)
        {
            m_visible.Remove(cell.ItemIndex);
            OnItemRecycle?.Invoke(cell, cell.ItemIndex);
            cell.Root.gameObject.SetActive(false);
            cell.ItemIndex = cell.ViewIndex = -1;
            m_pool.Push(cell);
        }

        private void RecycleAll()
        {
            for (int index = m_cells.Count - 1; index >= 0; --index) {
                RecycleCell(m_cells[index]);
            }
            m_cells.Clear();
        }

        private void DestroyPool()
        {
            while (m_pool.Count > 0) {
                ScrollItemView cell = m_pool.Pop();
                if (cell.Root != null) {
                    if (Application.isPlaying) {
                        Destroy(cell.Root.gameObject);
                    }
                    else {
                        DestroyImmediate(cell.Root.gameObject);
                    }
                }
                cell.ResetState();
            }
        }

        private void OnScrollChanged(Vector2 position)
        {
            EnsureLayoutCurrent();
            RefreshVisibleCells();
        }

        private void LateUpdate()
        {
            EnsureLayoutCurrent();
            AdvanceJump(Time.unscaledDeltaTime);
        }

        private void AdvanceJump(float deltaTime)
        {
            if (!IsJumping) {
                return;
            }
            m_jumpElapsed += deltaTime;
            float progress = Mathf.Clamp01(m_jumpElapsed / m_jumpDuration);
            SetOffset(Mathf.Lerp(m_jumpFrom, m_jumpTo, Mathf.SmoothStep(0f, 1f, progress)));
            IsJumping = progress < 1f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            IsJumping = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            IsJumping = false;
        }

        private void OnEnable()
        {
            if (IsInitialized) {
                ScrollRect.onValueChanged.AddListener(OnScrollChanged);
                EnsureLayoutCurrent();
                RefreshVisibleCells();
            }
        }

        private void OnDisable()
        {
            IsJumping = false;
            if (ScrollRect != null) {
                ScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
                ScrollRect.StopMovement();
            }
        }

        private void OnDestroy()
        {
            // All active and pooled roots belong directly to Content and are
            // destroyed with the scene. Do not call business UI during teardown.
            m_visible.Clear();
            m_cells.Clear();
            m_pool.Clear();
            IsInitialized = false;
        }
    }
}
