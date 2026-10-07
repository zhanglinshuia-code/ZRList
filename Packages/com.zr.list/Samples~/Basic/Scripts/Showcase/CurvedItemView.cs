// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    /// <summary>A cached business view. The list owns Root; the effect owns Visual.</summary>
    public sealed class CurvedItemView
    {
        public readonly RectTransform Root;
        public readonly RectTransform Visual;
        private readonly RawImage m_icon;
        private readonly Text m_title;
        private readonly Image m_accent;
        private readonly Vector3 m_position;
        private readonly Quaternion m_rotation;

        public CurvedItemView(RectTransform root)
        {
            Root = root;
            Visual = (RectTransform)root.Find("Visual");
            m_icon = Visual.Find("Icon").GetComponent<RawImage>();
            m_title = Visual.Find("Title").GetComponent<Text>();
            m_accent = Visual.Find("Accent").GetComponent<Image>();
            m_position = Visual.anchoredPosition3D;
            m_rotation = Visual.localRotation;
        }

        public void SetData(string title, Texture icon, Color color)
        {
            // Titles are prepared once by the demo, never formatted during recycling.
            m_title.text = title;
            m_icon.texture = icon;
            m_accent.color = color;
        }

        public void Apply(Vector3 localOffset, float angle)
        {
            Vector3 position = m_position + localOffset;
            if (Visual.anchoredPosition3D != position) {
                Visual.anchoredPosition3D = position;
            }

            Quaternion rotation = m_rotation * Quaternion.Euler(0f, 0f, angle);
            if (Visual.localRotation != rotation) {
                Visual.localRotation = rotation;
            }
        }

        public void ResetVisual()
        {
            Apply(Vector3.zero, 0f);
        }
    }
}
