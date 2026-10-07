// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class CatalogItemView
    {
        public readonly RectTransform Root;
        public readonly Text Title;
        public readonly Text Description;
        public readonly Text Index;
        public readonly RawImage Icon;
        public readonly Image Accent;
        public CatalogItemView(RectTransform root)
        {
            Root = root;
            Title = root.Find("Title").GetComponent<Text>();
            Description = root.Find("Description").GetComponent<Text>();
            Index = root.Find("Index").GetComponent<Text>();
            Icon = root.Find("Icon").GetComponent<RawImage>();
            Accent = root.Find("Accent").GetComponent<Image>();
        }

        public void SetData(int dataIndex, string title, string index, Texture texture, bool horizontal)
        {
            Title.text = title;
            Description.text = horizontal ? "A compact collection for the next journey.\nSwipe to explore more kits." : "Explore, collect and make room for the next adventure.";
            Index.text = index;
            Icon.texture = texture;
            Accent.color = Color.HSVToRGB((dataIndex % 7) / 7f, 0.48f, 0.95f);
        }
    }
}
