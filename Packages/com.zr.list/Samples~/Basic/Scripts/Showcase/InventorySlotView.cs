// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using ZRList.Samples.Support;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class InventorySlotView
    {
        public readonly RectTransform Root;
        public readonly Text Symbol;
        public readonly Text Caption;
        public readonly Text Quantity;
        public readonly Image RarityStripe;
        public readonly Image Selection;
        public readonly Image IconPanel;
        private Action<int> m_onSelected;
        private int m_dataIndex;
        public InventorySlotView(RectTransform root, bool listenForSelection = true)
        {
            Root = root;
            Symbol = root.Find("IconPanel/Symbol").GetComponent<Text>();
            Caption = root.Find("Name").GetComponent<Text>();
            Quantity = root.Find("Quantity").GetComponent<Text>();
            RarityStripe = root.Find("Rarity").GetComponent<Image>();
            Selection = root.Find("Selection").GetComponent<Image>();
            IconPanel = root.Find("IconPanel").GetComponent<Image>();
            if (listenForSelection) {
                root.GetComponent<Button>().onClick.AddListener(Select);
            }
        }

        public void SetData(int dataIndex, string name, string symbol, int quantity, Color color, bool selected, Action<int> onSelected)
        {
            m_dataIndex = dataIndex;
            m_onSelected = onSelected;
            Caption.text = name;
            Symbol.text = symbol;
            Quantity.text = DemoQuantityText.Get(quantity);
            RarityStripe.color = color;
            IconPanel.color = new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 1f);
            Selection.enabled = selected;
        }

        public void Select()
        {
            m_onSelected?.Invoke(m_dataIndex);
        }

        public void Unbind()
        {
            m_onSelected = null;
            Selection.enabled = false;
        }
    }
}
