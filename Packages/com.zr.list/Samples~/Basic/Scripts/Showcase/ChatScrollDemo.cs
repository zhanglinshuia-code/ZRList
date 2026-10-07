// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using ZRList.Samples.Support;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class ChatScrollDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public InputField MessageInput;
        public Text Status;
        [Min(0)]
        public int InitialMessageCount = 400;
        private readonly List<ChatMessage> m_messages = new List<ChatMessage>();
        private bool m_messagesCreated;
        private DemoStatusText m_statusText;
        public IReadOnlyList<ChatMessage> Messages
        {
            get
            {
                return m_messages;
            }
        }

        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (ScrollView.IsInitialized) {
                return;
            }

            if (!m_messagesCreated) {
                if (ScrollView.ItemPrefabs == null || ScrollView.ItemPrefabs.Length == 0) {
                    throw new InvalidOperationException("Configure at least one chat item prefab.");
                }

                for (var index = 0; index < InitialMessageCount; ++index) {
                    bool outgoing = index % 3 == 1;
                    m_messages.Add(new ChatMessage { IsOutgoing = outgoing, PrefabIndex = SelectMessagePrefab(outgoing, index), Body = CreateMessageBody(index) });
                }

                m_messagesCreated = true;
            }

            ScrollView.ViewStateChanged += UpdateStatus;
            m_statusText = new DemoStatusText(Status, " messages  /  ", " visible  /  ", " prefab types");
            ScrollView.InitializeWithPrefabIndex(m_messages.Count, GetItemPrefabIndex);
            ScrollView.OnItemRender += OnItemRender;
        }

        private int GetItemPrefabIndex(int dataIndex)
        {
            return m_messages[dataIndex].PrefabIndex;
        }

        // Example business rule: even slots are received styles, odd slots are sent styles.
        // One shared template supports both senders; the view fills the sender and alignment.
        private int SelectMessagePrefab(bool outgoing, int messageIndex)
        {
            if (ScrollView.ItemPrefabs.Length == 1) {
                return 0;
            }

            int first = outgoing ? 1 : 0;
            int variants = (ScrollView.ItemPrefabs.Length - first + 1) / 2;
            return first + ((messageIndex % variants) * 2);
        }

        public void OnItemRender(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent == null) {
                item.CachedComponent = new ChatBubbleView(item.Root);
            }

            var bubble = (ChatBubbleView)item.CachedComponent;
            ChatMessage message = m_messages[dataIndex];
            if (item.Prefab != ScrollView.ItemPrefabs[message.PrefabIndex]) {
                throw new InvalidOperationException("The wrong chat prefab was reused.");
            }

            bubble.SetData(message, dataIndex);
            float height = bubble.Measure(item.Root.rect.width);
            ScrollView.SetItemSize(dataIndex, height);
        }

        private static string CreateMessageBody(int index)
        {
            switch (index % 4) {
                case 0:
                    return "Have you packed everything for the weekend?";
                case 1:
                    return "Almost ready! I added a first aid kit and an extra bottle of water.";
                case 2:
                    return "Great. Let's meet at the station at nine.\nThe trail is longer than last time, so bring a warm jacket and comfortable shoes.\nI'll send you the map in a moment.";
                default:
                    return "Sounds good. See you there!";
            }
        }

        public void SendMessage()
        {
            if (MessageInput != null && !string.IsNullOrWhiteSpace(MessageInput.text)) {
                AppendMessage(MessageInput.text.Trim(), true);
                MessageInput.text = string.Empty;
                MessageInput.ActivateInputField();
            }
        }

        public void AddReceivedMessage()
        {
            AppendMessage("Here is a new reply from Morgan.\nDifferent lengths still keep the conversation aligned.", false);
        }

        public void AppendMessage(string body, bool isOutgoing)
        {
            if (string.IsNullOrWhiteSpace(body)) {
                throw new ArgumentException("A message needs text.", nameof(body));
            }

            m_messages.Add(new ChatMessage { IsOutgoing = isOutgoing, PrefabIndex = SelectMessagePrefab(isOutgoing, m_messages.Count), Body = body });
            ScrollView.AppendItems(1);
            ScrollView.JumpToDataItem(m_messages.Count - 1);
        }

        public void ToggleSender(int dataIndex)
        {
            if (dataIndex < 0 || dataIndex >= m_messages.Count) {
                throw new ArgumentOutOfRangeException(nameof(dataIndex));
            }

            m_messages[dataIndex].IsOutgoing = !m_messages[dataIndex].IsOutgoing;
            m_messages[dataIndex].PrefabIndex = SelectMessagePrefab(m_messages[dataIndex].IsOutgoing, dataIndex);
            using (ScrollView.BeginUpdateScope()) {
                ScrollView.RefreshItem(dataIndex);
            }
        }

        public void JumpToFirst()
        {
            ScrollView.JumpToDataItem(0, 0.3f);
        }

        public void JumpToLast()
        {
            ScrollView.JumpToDataItem(m_messages.Count - 1, 0.3f);
        }

        private void OnDestroy()
        {
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= UpdateStatus;
                ScrollView.OnItemRender -= OnItemRender;
            }
        }

        private void UpdateStatus()
        {
            if (Status != null && ScrollView != null) {
                m_statusText.Set(ScrollView.ItemCount, ScrollView.VisibleItems.Count, ScrollView.ItemPrefabs.Length);
            }
        }
    }
}
