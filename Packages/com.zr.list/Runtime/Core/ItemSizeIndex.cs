// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;

namespace ZRList
{
    /// <summary>Current layout lengths plus spacing. This geometry index never decides whether to measure a view.</summary>
    internal sealed class ItemSizeIndex
    {
        private float[] m_sizes = Array.Empty<float>();
        private double[] m_tree = Array.Empty<double>();
        private int m_count;
        private float m_spacing;
        private float m_estimate;
        private double m_totalSize;
        private int m_searchBit = 1;
        public long Version { get; private set; }
        public int Count
        {
            get
            {
                return m_count;
            }
        }

        public double TotalSize
        {
            get
            {
                return m_totalSize;
            }
        }

        public void Clear()
        {
            ++Version;
            m_sizes = Array.Empty<float>();
            m_tree = Array.Empty<double>();
            m_count = 0;
            m_totalSize = 0d;
            m_searchBit = 1;
        }

        public void Reset(int count, float estimatedSize, float itemSpacing)
        {
            EnsureCapacity(count);
            ++Version;
            m_count = count;
            UpdateSearchBit();
            m_estimate = estimatedSize;
            m_spacing = itemSpacing;
            double stride = (double)estimatedSize + m_spacing;
            m_totalSize = count == 0 ? 0d : count * stride - m_spacing;
            for (int i = 0; i < count; ++i) {
                m_sizes[i] = estimatedSize;
            }

            for (int i = 1; i <= count; ++i) {
                m_tree[i] = stride * (i & -i);
            }
        }

        private void EnsureCapacity(int count)
        {
            if (count < 0 || count == int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count <= m_sizes.Length) {
                return;
            }

            int capacity = (int)Math.Min(int.MaxValue - 1L, Math.Max(count, Math.Max(16L, (long)m_sizes.Length * 2)));
            Array.Resize(ref m_sizes, capacity);
            Array.Resize(ref m_tree, capacity + 1);
        }

        public void Append(int count)
        {
            if (count < 0 || count > int.MaxValue - 1 - Count) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0) {
                return;
            }

            int previousCount = Count;
            int nextCount = previousCount + count;
            EnsureCapacity(nextCount);
            double stride = (double)m_estimate + m_spacing;
            // Existing tree nodes stay valid. Each new node covers the preceding
            // range plus its own estimated item, including across capacity growth.
            for (int i = previousCount + 1; i <= nextCount; ++i) {
                m_sizes[i - 1] = m_estimate;
                m_tree[i] = PrefixSize(i - 1) - PrefixSize(i - (i & -i)) + stride;
            }

            m_count = nextCount;
            ++Version;
            UpdateSearchBit();
            m_totalSize += count * stride - (previousCount == 0 ? m_spacing : 0d);
        }

        public float GetSize(int index)
        {
            return m_sizes[index];
        }

        public void SetSize(int index, float size)
        {
            double difference = (double)size - m_sizes[index];
            m_sizes[index] = size;
            if (difference == 0d) {
                return;
            }

            ++Version;
            m_totalSize += difference;
            for (int i = index + 1; i <= Count; i += i & -i) {
                m_tree[i] += difference;
            }
        }

        public void ChangeSpacing(float itemSpacing)
        {
            if (m_spacing == itemSpacing) {
                return;
            }

            ++Version;
            m_spacing = itemSpacing;
            Array.Clear(m_tree, 0, Count + (m_tree.Length > 0 ? 1 : 0));
            m_totalSize = 0d;
            for (int i = 1; i <= Count; ++i) {
                double stride = (double)m_sizes[i - 1] + m_spacing;
                m_totalSize += stride;
                m_tree[i] += stride;
                int parent = i + (i & -i);
                if (parent <= Count) {
                    m_tree[parent] += m_tree[i];
                }
            }

            if (Count > 0) {
                m_totalSize -= m_spacing;
            }
        }

        public double PrefixSize(int count)
        {
            double total = 0d;
            for (int i = count; i > 0; i -= i & -i) {
                total += m_tree[i];
            }

            return total;
        }

        private void UpdateSearchBit()
        {
            m_searchBit = 1;
            while (m_searchBit <= Count / 2) {
                m_searchBit <<= 1;
            }
        }

        public int FindIndex(double offset)
        {
            int index = 0;
            double prefix = 0d;
            for (int bit = m_searchBit; bit > 0; bit >>= 1) {
                int next = index + bit;
                if (next <= Count && prefix + m_tree[next] <= offset) {
                    index = next;
                    prefix += m_tree[next];
                }
            }

            return Math.Max(0, Math.Min(index, Count - 1));
        }
    }
}
