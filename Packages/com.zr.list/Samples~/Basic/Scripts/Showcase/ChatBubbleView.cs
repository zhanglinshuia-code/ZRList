// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class ChatBubbleView
    {
        public readonly RectTransform Root;
        public bool IsOutgoing { get; private set; }
        public readonly RectTransform Bubble;
        public readonly RectTransform Avatar;
        public readonly Text Body;
        public readonly Text Speaker;
        public readonly Text TimeLabel;
        public readonly Text AvatarInitial;
        private static readonly string[] s_times = CreateTimes();

        private static string[] CreateTimes()
        {
            var times = new string[60];
            for (var index = 0; index < times.Length; ++index) {
                times[index] = "09:" + index.ToString("D2");
            }

            return times;
        }
        public ChatBubbleView(RectTransform root)
        {
            Root = root;
            Bubble = (RectTransform)root.Find("Bubble");
            Avatar = (RectTransform)root.Find("Avatar");
            Body = Bubble.Find("Message").GetComponent<Text>();
            Speaker = Bubble.Find("Speaker").GetComponent<Text>();
            TimeLabel = Bubble.Find("Time").GetComponent<Text>();
            AvatarInitial = Avatar.Find("Initial").GetComponent<Text>();
        }

        public void SetData(ChatMessage message, int dataIndex)
        {
            IsOutgoing = message.IsOutgoing;
            Body.text = message.Body;
            Speaker.text = message.IsOutgoing ? "YOU" : "MORGAN";
            TimeLabel.text = s_times[dataIndex % s_times.Length];
            AvatarInitial.text = message.IsOutgoing ? "Y" : "M";
        }

        public float Measure(float availableWidth)
        {
            float bubbleWidth = Mathf.Max(60f, Mathf.Min(540f, (availableWidth - 96f) * 0.78f));
            Bubble.anchorMin = Bubble.anchorMax = new Vector2(IsOutgoing ? 1f : 0f, 1f);
            Bubble.pivot = new Vector2(IsOutgoing ? 1f : 0f, 1f);
            Bubble.anchoredPosition = new Vector2(IsOutgoing ? -68f : 68f, -6f);
            Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
            Body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth - 32f);
            float textHeight = Mathf.Max(24f, Body.preferredHeight);
            float bubbleHeight = textHeight + 58f;
            Bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
            Body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
            Avatar.anchorMin = Avatar.anchorMax = new Vector2(IsOutgoing ? 1f : 0f, 1f);
            Avatar.pivot = new Vector2(IsOutgoing ? 1f : 0f, 1f);
            Avatar.anchoredPosition = new Vector2(IsOutgoing ? -12f : 12f, -6f);
            return bubbleHeight + 12f;
        }
    }
}
