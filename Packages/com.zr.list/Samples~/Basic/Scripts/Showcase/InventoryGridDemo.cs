// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using ZRList.Samples.Support;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class InventoryGridDemo: MonoBehaviour
    {
        public DirectGridView Grid;
        public Text Status;
        public Text SelectedItem;
        [Min(0)]
        public int DataCount = 503;
        private Action<int> m_selectItem;
        private DemoStatusText m_statusText;
        private int m_selectedIndex = -1;
        private readonly Dictionary<int, int> m_quantities = new Dictionary<int, int>();
        private static readonly string[] s_names =
        {
            "Health potion",
            "Mana potion",
            "Iron sword",
            "Oak shield",
            "Copper key",
            "Moon gem"
        };
        private static readonly string[] s_symbols =
        {
            "HP",
            "MP",
            "SW",
            "SH",
            "KEY",
            "GEM"
        };
        public int SelectedIndex
        {
            get
            {
                return m_selectedIndex;
            }
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

            m_selectItem = SelectItem;
            m_statusText = new DemoStatusText(Status, " slots  /  ", " columns  /  ", " visible");
            Grid.ViewStateChanged += UpdateStatus;
            Grid.OnItemRecycle += OnItemRecycle;
            Grid.OnItemRender += OnItemRender;
            Grid.Initialize(DataCount);
        }

        public void OnItemRender(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent == null) {
                item.CachedComponent = new InventorySlotView(item.Root);
            }

            int kind = dataIndex % s_names.Length;
            ((InventorySlotView)item.CachedComponent).SetData(dataIndex, s_names[kind], s_symbols[kind], GetQuantity(dataIndex), Color.HSVToRGB(kind / 6f, 0.5f, 0.95f), dataIndex == m_selectedIndex, m_selectItem);
        }

        private void OnItemRecycle(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent != null) {
                ((InventorySlotView)item.CachedComponent).Unbind();
            }
        }

        public void SelectItem(int index)
        {
            if (index < 0 || index >= Grid.ItemCount) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int previous = m_selectedIndex;
            m_selectedIndex = index;
            if (previous >= 0) {
                Grid.RefreshItem(previous);
            }

            Grid.RefreshItem(index);
            if (SelectedItem != null) {
                SelectedItem.text = s_names[index % s_names.Length] + "  #" + (index + 1) + "  /  quantity " + GetQuantity(index);
            }
        }

        public int GetQuantity(int index)
        {
            return m_quantities.TryGetValue(index, out int quantity) ? quantity : (index % 9) + 1;
        }

        public void UseSelectedItem()
        {
            if (m_selectedIndex < 0) {
                return;
            }

            m_quantities[m_selectedIndex] = Mathf.Max(0, GetQuantity(m_selectedIndex) - 1);
            SelectItem(m_selectedIndex);
        }

        public void JumpToFirst()
        {
            Grid.JumpToItem(0, 0.3f);
        }

        public void JumpToLast()
        {
            Grid.JumpToItem(Grid.ItemCount - 1, 0.3f);
        }

        private void OnDestroy()
        {
            if (Grid != null) {
                Grid.ViewStateChanged -= UpdateStatus;
                Grid.OnItemRender -= OnItemRender;
                Grid.OnItemRecycle -= OnItemRecycle;
            }
        }

        private void UpdateStatus()
        {
            if (Grid == null) {
                return;
            }

            if (m_selectedIndex >= Grid.ItemCount) {
                m_selectedIndex = -1;
                if (SelectedItem != null) {
                    SelectedItem.text = "Select a slot to inspect or use an item";
                }
            }

            if (Status != null) {
                m_statusText.Set(Grid.ItemCount, Grid.ResolvedColumnCount, Grid.VisibleCellCount);
            }
        }
    }
}
