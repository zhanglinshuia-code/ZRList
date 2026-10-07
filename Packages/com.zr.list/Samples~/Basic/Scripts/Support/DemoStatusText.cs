// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples.Support
{
    /// <summary>Cache the small set of visible/pool statistics, not a string for every rebind.</summary>
    public sealed class DemoStatusText
    {
        private readonly Text m_target;
        private readonly string m_countSuffix;
        private readonly string m_firstSuffix;
        private readonly string m_secondSuffix;
        private readonly Dictionary<Vector2Int, string> m_messages = new Dictionary<Vector2Int, string>();
        private int m_count = -1;
        private string m_prefix;

        public DemoStatusText(Text target, string countSuffix, string firstSuffix, string secondSuffix)
        {
            m_target = target;
            m_countSuffix = countSuffix;
            m_firstSuffix = firstSuffix;
            m_secondSuffix = secondSuffix;
        }

        public void Set(int count, int first, int second = -1)
        {
            if (m_target == null) {
                return;
            }

            if (m_count != count) {
                m_count = count;
                m_messages.Clear();
                m_prefix = count + m_countSuffix;
            }

            var key = new Vector2Int(first, second);
            if (!m_messages.TryGetValue(key, out string message)) {
                message = m_prefix + first + m_firstSuffix + (second >= 0 ? second.ToString() : string.Empty) + m_secondSuffix;
                m_messages.Add(key, message);
            }

            m_target.text = message;
        }
    }
}
