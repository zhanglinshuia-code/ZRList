// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    /// <summary>
    /// Fixed-size, row-virtualized vertical grid. Uses ordinary item adapters for
    /// cells, including optional prefab selection and view lifecycle callbacks.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("UI/Virtual Grid View")]
    public sealed partial class VirtualGridView: MonoBehaviour
    {
        public VirtualScrollView RowScrollView;
        public RectTransform CellPrefab;
        [Tooltip("Optional cell templates. Business code selects an index with InitializeWithPrefabIndex.")]
        public RectTransform[] CellPrefabs = Array.Empty<RectTransform>();
        [Min(0)]
        public int ColumnCount;
        public Vector2 CellSize = new Vector2(128f, 128f);
        public Vector2 CellSpacing = new Vector2(12f, 12f);
        private IScrollItemAdapter m_adapter;
        private RowAdapter m_rowAdapter;
        private RectTransform m_creationRoot;
        private readonly Dictionary<RectTransform, Stack<ScrollItemView>> m_cellPools = new Dictionary<RectTransform, Stack<ScrollItemView>>();
        private readonly List<RowView> m_rows = new List<RowView>();
        private int m_pooledCellCount;
        private int m_retainedCellCount;
        private int m_itemCount;
        private int m_columns;
        private int m_configuredColumns;
        private Vector2 m_configuredCellSize;
        private Vector2 m_configuredSpacing;
        private float m_availableWidth;
        private RectTransform m_configuredCellPrefab;
        private bool m_initialized;
        private int m_callbackDepth;
        private bool m_rebuildingRows;
        private VirtualScrollView m_listeningRowScrollView;
        public event Action ViewStateChanged;
        public bool IsInitialized
        {
            get
            {
                return m_initialized && RowScrollView != null && RowScrollView.IsInitialized;
            }
        }

        public int ItemCount
        {
            get
            {
                return m_itemCount;
            }
        }

        public int ResolvedColumnCount
        {
            get
            {
                return m_columns;
            }
        }

        public int VisibleCellCount
        {
            get
            {
                if (!IsInitialized) {
                    return 0;
                }

                // The root registry already tracks every bound cell. During a
                // callback the row renderer can still be transferring old rows,
                // so preserve the visible collection's in-progress count there.
                if (m_callbackDepth == 0 && RowScrollView.CanQueryViewport) {
                    return m_boundCellsByRoot.Count;
                }

                var count = 0;
                IReadOnlyList<ScrollItemView> rows = RowScrollView.VisibleItems;
                for (int rowIndex = 0; rowIndex < rows.Count; ++rowIndex) {
                    foreach (ScrollItemView cell in ((RowView)rows[rowIndex]).Cells) {
                        if (cell != null && cell.IsBound) {
                            ++count;
                        }
                    }
                }

                return count;
            }
        }

        public int PooledCellCount
        {
            get
            {
                return m_pooledCellCount + m_retainedCellCount;
            }
        }

        public void Initialize(IScrollItemAdapter adapter, int itemCount)
        {
            EnsureOutsideCallback();
            if (adapter == null) {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            if (RowScrollView == null || RowScrollView.Viewport == null || (CellPrefab == null && adapter is not IScrollItemPrefabProvider)) {
                throw new InvalidOperationException("Assign the row scroll view, row prefab, viewport and cell prefab.");
            }

            ScrollItemPrefabCollection.Validate(RowScrollView.ItemPrefabs, null);
            ValidateGeometry();
            if (m_adapter != null && !ReferenceEquals(m_adapter, adapter)) {
                Dispose();
            }

            m_adapter = adapter;
            m_itemCount = itemCount;
            if (m_rowAdapter == null) {
                m_rowAdapter = new RowAdapter(this);
            }

            SubscribeRowViewChanges();
            RebuildRows(false);
        }

        public void ReloadData(int itemCount)
        {
            EnsureOutsideCallback();
            if (!IsInitialized) {
                throw new InvalidOperationException("Initialize the grid before reloading data.");
            }

            if (itemCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            ValidateGeometry();
            m_itemCount = itemCount;
            RebuildRows(false);
        }

        public bool JumpToItem(int dataIndex, float duration = 0f)
        {
            EnsureLayoutCurrent();
            return IsRendererReady && IsInitialized && dataIndex >= 0 && dataIndex < m_itemCount && RowScrollView.JumpToDataItem(dataIndex / m_columns, duration);
        }

        public bool RefreshItem(int dataIndex)
        {
            EnsureLayoutCurrent();
            if (!IsInitialized || dataIndex < 0 || dataIndex >= m_itemCount) {
                return false;
            }

            RowScrollView.RefreshItem(dataIndex / m_columns);
            return true;
        }

        public void RefreshLayout()
        {
            EnsureOutsideCallback();
            if (IsInitialized) {
                RebuildRows(true);
            }
        }

        public void Dispose()
        {
            EnsureOutsideCallback();
            m_initialized = false;
            DetachRowViewChanges();
            Exception firstError = null;
            try {
                if (RowScrollView != null) {
                    RowScrollView.Dispose();
                }
            }
            catch (Exception error) {
                firstError = error;
            }
            finally {
                try {
                    TrimCellPools(0);
                }
                catch (Exception error) {
                    firstError ??= error;
                }
                finally {
                    m_cellPools.Clear();
                    m_boundCellsByRoot.Clear();
                    m_rows.Clear();
                    m_pooledCellCount = m_retainedCellCount = 0;
                    if (ReferenceEquals(m_adapter, m_callbackAdapter)) {
                        m_callbackAdapter?.ClearPrefabSelection();
                        m_usesConfiguredPrefabs = false;
                    }
                    m_adapter = null;
                    m_itemCount = 0;
                    if (m_creationRoot != null) {
                        Release(m_creationRoot);
                    }

                    m_creationRoot = null;
                    ViewStateChanged?.Invoke();
                }
            }

            if (firstError != null) {
                throw firstError;
            }
        }

        /// <summary>Release idle rows and all but the requested total number of idle cells.</summary>
        public void TrimPool(int retainedCellCount = 0)
        {
            EnsureOutsideCallback();
            if (retainedCellCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(retainedCellCount));
            }

            bool wasRebuilding = m_rebuildingRows;
            m_rebuildingRows = true;
            try {
                TrimIdleViews(retainedCellCount);
            }
            finally {
                m_rebuildingRows = wasRebuilding;
                ViewStateChanged?.Invoke();
            }
        }

        private void TrimIdleViews(int retainedCellCount)
        {
            ++m_callbackDepth;
            try {
                foreach (RowView row in m_rows) {
                    for (int column = 0; column < row.Cells.Count; ++column) {
                        ScrollItemView cell = row.Cells[column];
                        if (cell != null && !cell.IsBound) {
                            row.Cells[column] = null;
                            --m_retainedCellCount;
                            RecycleCell(cell);
                        }
                    }
                }
            }
            finally {
                --m_callbackDepth;
            }

            Exception firstError = null;
            try {
                RowScrollView?.TrimPool();
            }
            catch (Exception error) {
                firstError = error;
            }

            try {
                TrimCellPools(retainedCellCount);
            }
            catch (Exception error) {
                firstError ??= error;
            }

            if (firstError != null) {
                throw firstError;
            }
        }

        private void TrimCellPools(int retainedCellCount)
        {
            Exception firstError = null;
            ++m_callbackDepth;
            try {
                foreach (Stack<ScrollItemView> pool in m_cellPools.Values) {
                    while (pool.Count > 0 && m_pooledCellCount > retainedCellCount) {
                        ScrollItemView cell = pool.Pop();
                        --m_pooledCellCount;
                        try {
                            DestroyCell(cell);
                        }
                        catch (Exception error) {
                            firstError ??= error;
                        }
                    }
                }
            }
            finally {
                --m_callbackDepth;
            }

            if (firstError != null) {
                throw firstError;
            }
        }

        private void EnsureOutsideCallback()
        {
            if (m_callbackDepth > 0) {
                throw new InvalidOperationException("Change grid ownership or geometry outside cell callbacks.");
            }
        }

        private void ValidateGeometry()
        {
            if (ColumnCount < 0 || !IsPositive(CellSize.x) || !IsPositive(CellSize.y) || !IsNonnegative(CellSpacing.x) || !IsNonnegative(CellSpacing.y)) {
                throw new ArgumentOutOfRangeException(nameof(CellSize), "Cell sizes must be finite and positive; spacing and columns must be nonnegative.");
            }
        }

        private static bool IsPositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsNonnegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private float GetAvailableWidth()
        {
            return Mathf.Max(1f, RowScrollView.Viewport.rect.width - RowScrollView.ContentPadding.horizontal);
        }

        private void RebuildRows(bool preserveItem)
        {
            ValidateGeometry();
            if (m_callbackAdapter != null && ReferenceEquals(m_adapter, m_callbackAdapter)) {
                if (m_usesConfiguredPrefabs) {
                    ScrollItemPrefabCollection.Validate(CellPrefabs, m_callbackAdapter.PrefabIndexSelector);
                    m_callbackAdapter.Prefabs = CellPrefabs;
                }
                m_callbackAdapter.DefaultPrefab = CellPrefab;
                m_callbackAdapter.EstimatedItemSize = CellSize.y;
            }
            var firstItem = 0;
            if (preserveItem && RowScrollView.VisibleItems.Count > 0) {
                firstItem = RowScrollView.VisibleItems[0].ItemIndex * m_columns;
            }

            m_availableWidth = GetAvailableWidth();
            m_columns = ColumnCount == 0 ? Mathf.Max(1, Mathf.FloorToInt((m_availableWidth + CellSpacing.x) / (CellSize.x + CellSpacing.x))) : ColumnCount;
            m_configuredColumns = ColumnCount;
            m_configuredCellSize = CellSize;
            m_configuredSpacing = CellSpacing;
            m_configuredCellPrefab = CellPrefab;
            RowScrollView.Orientation = ScrollOrientation.Vertical;
            RowScrollView.FirstDataIndex = 0;
            RowScrollView.StretchItems = true;
            RowScrollView.ContentSpacing = CellSpacing.y;
            m_rebuildingRows = true;
            try {
                bool reload = IsInitialized;
                m_initialized = false;
                int rowCount = (int)(((long)m_itemCount + m_columns - 1) / m_columns);
                if (reload) {
                    RowScrollView.ReloadData(rowCount);
                }
                else {
                    RowScrollView.Initialize(m_rowAdapter, rowCount);
                }
                m_initialized = true;
                if (preserveItem && m_itemCount > 0) {
                    JumpToItem(Mathf.Min(firstItem, m_itemCount - 1));
                }
            }
            finally {
                m_rebuildingRows = false;
            }

            ViewStateChanged?.Invoke();
        }

        private void EnsureLayoutCurrent()
        {
            if (!IsInitialized || m_callbackDepth > 0) {
                return;
            }

            if (m_configuredColumns != ColumnCount || m_configuredCellSize != CellSize || m_configuredSpacing != CellSpacing || m_configuredCellPrefab != CellPrefab || m_availableWidth != GetAvailableWidth() || (m_usesConfiguredPrefabs && ReferenceEquals(m_adapter, m_callbackAdapter) && !ReferenceEquals(m_callbackAdapter.Prefabs, CellPrefabs))) {
                RebuildRows(true);
            }
        }

        private void Update()
        {
            EnsureLayoutCurrent();
        }

        private void SubscribeRowViewChanges()
        {
            if (m_listeningRowScrollView == RowScrollView) {
                return;
            }

            DetachRowViewChanges();
            m_listeningRowScrollView = RowScrollView;
            m_listeningRowScrollView.ViewStateChanged += OnRowViewStateChanged;
        }

        private void DetachRowViewChanges()
        {
            if (m_listeningRowScrollView != null) {
                m_listeningRowScrollView.ViewStateChanged -= OnRowViewStateChanged;
            }

            m_listeningRowScrollView = null;
        }

        private void OnRowViewStateChanged()
        {
            if (!m_rebuildingRows && m_initialized) {
                ViewStateChanged?.Invoke();
            }
        }

        private void OnEnable()
        {
            if (IsInitialized) {
                SubscribeRowViewChanges();
            }
        }

        private void OnDisable()
        {
            DetachRowViewChanges();
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

        private sealed class RowView: ScrollItemView
        {
            public readonly List<ScrollItemView> Cells = new List<ScrollItemView>();
            public RowView(RectTransform root) : base(root, -1, -1)
            {
            }
        }

        private sealed class RowAdapter: ScrollItemAdapter<RowView>
        {
            private readonly VirtualGridView m_grid;
            public RowAdapter(VirtualGridView grid)
            {
                m_grid = grid;
            }

            public override float EstimatedItemSize
            {
                get
                {
                    return m_grid.CellSize.y;
                }
            }

            protected override RowView CreateView(RectTransform root)
            {
                var row = new RowView(root);
                m_grid.m_rows.Add(row);
                return row;
            }

            public override bool TryGetItemSize(int dataIndex, ScrollItemLayoutContext context, out float size)
            {
                size = m_grid.CellSize.y;
                return true;
            }

            protected override void BindView(RowView row, int rowIndex, ScrollItemLayoutContext context)
            {
                m_grid.BindCells(row, rowIndex);
            }

            protected override void UnbindView(RowView row, int dataIndex)
            {
                m_grid.UnbindCells(row);
            }

            protected override void DestroyView(RowView row)
            {
                m_grid.DestroyCells(row);
            }
        }

        private void BindCells(RowView row, int rowIndex)
        {
            if (!IsRendererReady) {
                return;
            }

            ++m_callbackDepth;
            try {
                for (var column = 0; column < m_columns; ++column) {
                    int dataIndex = rowIndex * m_columns + column;
                    if (dataIndex >= m_itemCount) {
                        break;
                    }

                    RectTransform prefab = m_adapter is IScrollItemPrefabProvider provider ? provider.GetItemPrefab(dataIndex) : CellPrefab;
                    if (prefab == null) {
                        throw new InvalidOperationException("The grid cell prefab provider returned null.");
                    }

                    while (row.Cells.Count <= column) {
                        row.Cells.Add(null);
                    }

                    ScrollItemView cell = row.Cells[column];
                    if (cell != null && !cell.IsBound) {
                        --m_retainedCellCount;
                    }

                    if (cell != null && cell.SourcePrefab != prefab) {
                        row.Cells[column] = null;
                        RecycleCell(cell);
                        cell = null;
                    }

                    if (cell == null) {
                        cell = AcquireCell(prefab, row.Root);
                        row.Cells[column] = cell;
                    }

                    cell.ItemIndex = dataIndex;
                    cell.ViewIndex = dataIndex;
                    cell.ItemSize = CellSize.y;
                    cell.IsBound = true;
                    m_boundCellsByRoot[cell.Root.gameObject] = cell;
                    ConfigureCellRoot(cell.Root, column);
                    m_adapter.Bind(cell, dataIndex, new ScrollItemLayoutContext(ScrollOrientation.Vertical, CellSize.x, CellSize.x, RowScrollView.LayoutRevision));
                    cell.Root.gameObject.SetActive(true);
                }
            }
            finally {
                --m_callbackDepth;
            }
        }

        private void ConfigureCellRoot(RectTransform root, int column)
        {
            // Retained cells normally keep their geometry across row reuse.
            // Read the real transform so business changes are still repaired.
            var topLeft = new Vector2(0f, 1f);
            if (!root.anchorMin.Equals(topLeft)) {
                root.anchorMin = topLeft;
            }

            if (!root.anchorMax.Equals(topLeft)) {
                root.anchorMax = topLeft;
            }

            if (!root.pivot.Equals(topLeft)) {
                root.pivot = topLeft;
            }

            if (!root.sizeDelta.Equals(CellSize)) {
                root.sizeDelta = CellSize;
            }

            var position = new Vector2(column * (CellSize.x + CellSpacing.x), 0f);
            if (!root.anchoredPosition.Equals(position)) {
                root.anchoredPosition = position;
            }
        }

        private ScrollItemView AcquireCell(RectTransform prefab, RectTransform row)
        {
            if (m_cellPools.TryGetValue(prefab, out Stack<ScrollItemView> pool)) {
                while (pool.Count > 0) {
                    ScrollItemView cell = pool.Pop();
                    --m_pooledCellCount;
                    if (cell.Root != null) {
                        cell.Root.SetParent(row, false);
                        return cell;
                    }
                }
            }

            return CreateCell(prefab, row);
        }

        private void RecycleCell(ScrollItemView cell)
        {
            m_boundCellsByRoot.Remove(cell.Root.gameObject);
            try {
                if (cell.IsBound) {
                    cell.IsBound = false;
                    if (cell.OwnerAdapter is IScrollItemViewLifecycle lifecycle) {
                        lifecycle.OnViewUnbound(cell, cell.ItemIndex);
                    }
                }
            }
            finally {
                cell.Root.gameObject.SetActive(false);
                cell.Root.SetParent(m_creationRoot, false);
                cell.ItemIndex = cell.ViewIndex = -1;
                if (!m_cellPools.TryGetValue(cell.SourcePrefab, out Stack<ScrollItemView> pool)) {
                    pool = new Stack<ScrollItemView>();
                    m_cellPools.Add(cell.SourcePrefab, pool);
                }

                pool.Push(cell);
                ++m_pooledCellCount;
            }
        }

        private ScrollItemView CreateCell(RectTransform prefab, RectTransform row)
        {
            if (m_creationRoot == null) {
                var container = new GameObject("Grid cell creation", typeof(RectTransform));
                container.SetActive(false);
                m_creationRoot = (RectTransform)container.transform;
                m_creationRoot.SetParent(transform, false);
            }

            var root = (RectTransform)Instantiate(prefab.gameObject, m_creationRoot, false).transform;
            root.gameObject.SetActive(false);
            try {
                ScrollItemView cell = m_adapter.CreateViewsHolder(root);
                if (cell == null || cell.Root != root) {
                    throw new InvalidOperationException("Return a cell view for the supplied root.");
                }

                cell.SourcePrefab = prefab;
                cell.OwnerAdapter = m_adapter;
                root.SetParent(row, false);
                return cell;
            }
            catch {
                Release(root);
                throw;
            }
        }

        private void UnbindCells(RowView row)
        {
            Exception firstError = null;
            ++m_callbackDepth;
            try {
                for (int column = 0; column < row.Cells.Count; ++column) {
                    ScrollItemView cell = row.Cells[column];
                    if (cell == null || !cell.IsBound) {
                        continue;
                    }

                    cell.IsBound = false;
                    m_boundCellsByRoot.Remove(cell.Root.gameObject);
                    try {
                        if (cell.OwnerAdapter is IScrollItemViewLifecycle lifecycle) {
                            lifecycle.OnViewUnbound(cell, cell.ItemIndex);
                        }
                    }
                    catch (Exception error) {
                        firstError ??= error;
                    }
                    finally {
                        cell.Root.gameObject.SetActive(false);
                        cell.ItemIndex = cell.ViewIndex = -1;
                        ++m_retainedCellCount;
                    }
                }
            }
            finally {
                --m_callbackDepth;
            }

            if (firstError != null) {
                throw firstError;
            }
        }

        private void DestroyCells(RowView row)
        {
            m_rows.Remove(row);
            Exception firstError = null;
            ++m_callbackDepth;
            try {
                for (int column = 0; column < row.Cells.Count; ++column) {
                    ScrollItemView cell = row.Cells[column];
                    if (cell == null) {
                        continue;
                    }

                    try {
                        row.Cells[column] = null;
                        if (!cell.IsBound) {
                            --m_retainedCellCount;
                        }

                        RecycleCell(cell);
                    }
                    catch (Exception error) {
                        firstError ??= error;
                    }
                }

                row.Cells.Clear();
            }
            finally {
                --m_callbackDepth;
            }

            if (firstError != null) {
                throw firstError;
            }
        }

        private void DestroyCell(ScrollItemView cell)
        {
            RectTransform root = cell.Root;
            if (root != null) {
                m_boundCellsByRoot.Remove(root.gameObject);
            }

            try {
                if (cell.OwnerAdapter is IScrollItemViewLifecycle lifecycle) {
                    lifecycle.OnViewDestroyed(cell);
                }
            }
            finally {
                if (root != null) {
                    Release(root);
                }

                cell.ResetState();
            }
        }

        private static void Release(RectTransform root)
        {
            root.gameObject.SetActive(false);
            if (Application.isPlaying) {
                Destroy(root.gameObject);
            }
            else {
                DestroyImmediate(root.gameObject);
            }
        }
    }
}
