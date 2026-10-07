// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class NestedListChildView
    {
        public readonly Text Title;
        public readonly Text Description;
        public readonly Image Accent;

        public NestedListChildView(RectTransform root)
        {
            Title = root.Find("Title").GetComponent<Text>();
            Description = root.Find("Description").GetComponent<Text>();
            Accent = root.Find("Accent").GetComponent<Image>();
        }

        public void SetData(int groupIndex, string title, string description)
        {
            Title.text = title;
            Description.text = description;
            Accent.color = Color.HSVToRGB((groupIndex % 8) / 8f, 0.45f, 0.92f);
        }
    }
}
