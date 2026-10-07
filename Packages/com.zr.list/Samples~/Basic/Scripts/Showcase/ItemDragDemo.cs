// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class ItemDragDemo: MonoBehaviour
    {
        public VirtualGridView Grid;
        public RectTransform DragLayer;
        public Text Status;
        [Min(1)]
        public int DataCount = 503;
        private readonly List<int> m_items = new List<int>();
        private readonly List<RaycastResult> m_hits = new List<RaycastResult>();
        private ItemDragHandle m_source;
        private RectTransform m_preview;
        private CanvasGroup m_sourceGroup;
        private int m_sourceIndex = -1;
        private int m_pointerId;
        private Vector2 m_grabOffset;
        private float m_sourceAlpha;
        private bool m_scrollEnabled;
        private string[] m_titles;
        private static readonly string[] s_names = { "Health potion", "Mana potion", "Iron sword", "Oak shield", "Copper key", "Moon gem" };
        private static readonly string[] s_symbols = { "HP", "MP", "SW", "SH", "KEY", "GEM" };

        public bool IsDragging
        {
            get
            {
                return m_sourceIndex >= 0;
            }
        }

        public int GetItemId(int index)
        {
            return m_items[index];
        }

        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (Grid.IsInitialized) {
                return;
            }

            m_items.Clear();
            int count = Mathf.Max(0, DataCount);
            m_titles = new string[count];
            for (int index = 0; index < count; ++index) {
                m_items.Add(index);
                m_titles[index] = s_names[index % s_names.Length] + " #" + (index + 1);
            }

            Grid.Initialize(m_items.Count);
            Grid.OnItemRecycle += OnItemRecycle;
            Grid.OnItemRender += OnItemRender;
            SetStatus("Drag the MOVE handle to swap items / drag the rest of a slot to scroll / drop outside to cancel");
        }

        private void OnItemRender(ScrollItemView item, int index)
        {
            if (item.CachedComponent == null) {
                item.CachedComponent = new InventorySlotView(item.Root);
                item.Root.GetComponent<Image>().raycastTarget = true;
                item.Root.gameObject.AddComponent<CanvasGroup>();
                ItemDragHandle handle = item.Root.Find("DragHandle").gameObject.AddComponent<ItemDragHandle>();
                handle.Owner = this;
                handle.ItemRoot = item.Root;
            }

            Bind((InventorySlotView)item.CachedComponent, m_items[index]);
        }

        private void Bind(InventorySlotView view, int id)
        {
            int kind = id % s_names.Length;
            view.SetData(id, m_titles[id], s_symbols[kind], id % 9 + 1,
                Color.HSVToRGB(kind / 6f, 0.5f, 0.95f), false, null);
        }

        private void OnItemRecycle(ScrollItemView item, int index)
        {
            CancelItemDrag(item.Root.GetComponentInChildren<ItemDragHandle>());
            ((InventorySlotView)item.CachedComponent)?.Unbind();
        }

        public void BeginItemDrag(ItemDragHandle source, PointerEventData eventData)
        {
            if (IsDragging || eventData.button != PointerEventData.InputButton.Left ||
                source.Owner != this || source.ItemRoot == null ||
                !Grid.TryGetItemIndex(source.ItemRoot.gameObject, out int index)) {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, eventData.position,
                eventData.pressEventCamera, out Vector2 pointer)) {
                return;
            }

            m_source = source;
            m_sourceIndex = index;
            m_pointerId = eventData.pointerId;
            m_scrollEnabled = Grid.RowScrollView.ScrollRect.enabled;
            Grid.RowScrollView.CancelJump();
            Grid.RowScrollView.ScrollRect.StopMovement();
            Grid.RowScrollView.ScrollRect.enabled = false;
            m_sourceGroup = source.ItemRoot.GetComponent<CanvasGroup>();
            m_sourceAlpha = m_sourceGroup.alpha;
            m_sourceGroup.alpha = 0.3f;

            // Clone the prefab, not the pooled cell: the preview has no grid binding or drag handler.
            m_preview = Instantiate(Grid.CellPrefab, DragLayer, false);
            m_preview.name = "Dragged item preview";
            m_preview.anchorMin = m_preview.anchorMax = DragLayer.pivot;
            m_preview.sizeDelta = source.ItemRoot.rect.size;
            m_preview.position = source.ItemRoot.position;
            Bind(new InventorySlotView(m_preview), m_items[index]);
            CanvasGroup group = m_preview.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            m_grabOffset = (Vector2)m_preview.localPosition - pointer;
            MoveItemDrag(eventData);
            SetStatus("Dragging " + s_names[m_items[index] % s_names.Length] + " / release over another slot");
        }

        public void MoveItemDrag(PointerEventData eventData)
        {
            if (IsDragging && eventData.pointerId == m_pointerId &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, eventData.position,
                    eventData.pressEventCamera, out Vector2 pointer)) {
                m_preview.localPosition = pointer + m_grabOffset;
            }
        }

        public void EndItemDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != m_pointerId) {
                return;
            }

            int target = -1;
            m_hits.Clear();
            if (EventSystem.current != null) {
                EventSystem.current.RaycastAll(eventData, m_hits);
            }
            if (m_hits.Count > 0) {
                // Any part of a target cell accepts the drop, including its handle.
                for (Transform hit = m_hits[0].gameObject.transform; hit != null && hit != Grid.transform; hit = hit.parent) {
                    if (Grid.TryGetItemIndex(hit.gameObject, out target)) {
                        break;
                    }
                }
            }

            int source = m_sourceIndex;
            FinishDrag();
            if (target >= 0 && target != source) {
                int value = m_items[source];
                m_items[source] = m_items[target];
                m_items[target] = value;
                Grid.RefreshItem(source);
                Grid.RefreshItem(target);
                SetStatus("Swapped slots " + (source + 1) + " and " + (target + 1));
            }
            else {
                SetStatus("Drag cancelled / items kept in their original slots");
            }
        }

        public void CancelItemDrag(ItemDragHandle source)
        {
            if (IsDragging && source == m_source) {
                FinishDrag();
                SetStatus("Drag cancelled / items kept in their original slots");
            }
        }

        private void FinishDrag()
        {
            m_sourceIndex = -1;
            m_source = null;
            if (m_sourceGroup != null) {
                m_sourceGroup.alpha = m_sourceAlpha;
            }

            m_sourceGroup = null;
            if (m_preview != null) {
                m_preview.gameObject.SetActive(false);
                if (Application.isPlaying) {
                    Destroy(m_preview.gameObject);
                }
                else {
                    DestroyImmediate(m_preview.gameObject);
                }
            }

            m_preview = null;
            Grid.RowScrollView.ScrollRect.enabled = m_scrollEnabled;
        }

        private void SetStatus(string message)
        {
            if (Status != null) {
                Status.text = message;
            }
        }

        private void OnDisable()
        {
            if (IsDragging) {
                FinishDrag();
            }
        }

        private void OnDestroy()
        {
            if (Grid != null) {
                Grid.OnItemRender -= OnItemRender;
                Grid.OnItemRecycle -= OnItemRecycle;
            }
        }
    }
}
