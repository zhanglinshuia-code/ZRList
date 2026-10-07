// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    public sealed class RewardRevealDemo: MonoBehaviour
    {
        public VirtualGridView Grid;
        public Text Status;
        [Min(0)] public int RewardCount = 24;
        [Min(0f)] public float InitialDelay = 0.25f;
        [Min(0f)] public float ItemInterval = 0.12f;
        [Min(0.01f)] public float PopDuration = 0.38f;
        [Min(0f)] public float RiseDistance = 24f;
        [Range(0f, 0.5f)] public float ScaleOvershoot = 0.18f;

        private readonly List<RewardItemView> m_boundViews = new List<RewardItemView>();
        private bool m_initialized;
        private float m_elapsed;
        private float m_initialDelay;
        private float m_interval;
        private float m_popDuration;
        private float m_totalDuration;
        private int m_startedCount = -1;
        private static readonly string[] s_names = { "Gold coins", "Moon gem", "Health potion", "Iron sword", "Copper key", "Oak shield" };
        private static readonly string[] s_symbols = { "GOLD", "GEM", "HP", "SW", "KEY", "SH" };
        private static readonly int[] s_quantities = { 500, 10, 3, 1, 2, 1 };

        public bool IsRevealing { get; private set; }

        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (m_initialized) {
                return;
            }

            Grid.Initialize(0);
            Grid.OnItemRecycle += OnItemRecycle;
            Grid.OnItemRender += OnItemRender;
            m_initialized = true;
            ReceiveRewards();
        }

        /// <summary>Starts a new batch; resetting the timeline also resets reused visuals.</summary>
        public void ReceiveRewards()
        {
            if (!m_initialized) {
                InitializeDemo();
                return;
            }

            m_elapsed = 0f;
            m_initialDelay = Mathf.Max(0f, InitialDelay);
            m_interval = Mathf.Max(0f, ItemInterval);
            m_popDuration = Mathf.Max(0.01f, PopDuration);
            int count = Mathf.Max(0, RewardCount);
            m_totalDuration = count == 0 ? 0f : m_initialDelay + (count - 1) * m_interval + m_popDuration;
            m_startedCount = -1;
            IsRevealing = count > 0;
            Grid.ReloadData(count);
            Grid.JumpToItem(0);
            ApplyAnimation();
            UpdateStatus();
        }

        public void ShowAllRewards()
        {
            if (!m_initialized) {
                return;
            }

            m_elapsed = m_totalDuration;
            IsRevealing = false;
            ApplyAnimation();
            UpdateStatus();
        }

        private void Update()
        {
            // Reward screens often pause gameplay. Presentation uses unscaled time.
            AdvanceReveal(Time.unscaledDeltaTime);
        }

        private void AdvanceReveal(float deltaTime)
        {
            if (!IsRevealing) {
                return;
            }

            m_elapsed = Mathf.Min(m_totalDuration, m_elapsed + Mathf.Max(0f, deltaTime));
            IsRevealing = m_elapsed < m_totalDuration;
            ApplyAnimation();
            UpdateStatus();
        }

        public void OnItemRender(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent == null) {
                item.CachedComponent = new RewardItemView(item.Root);
            }

            var view = (RewardItemView)item.CachedComponent;
            int kind = dataIndex % s_names.Length;
            view.SetData(dataIndex, s_names[kind], s_symbols[kind], s_quantities[kind], Color.HSVToRGB(kind / 6f, 0.5f, 0.95f));
            if (!m_boundViews.Contains(view)) {
                m_boundViews.Add(view);
            }

            Animate(view);
        }

        private void OnItemRecycle(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent is RewardItemView view) {
                m_boundViews.Remove(view);
                view.Unbind();
            }
        }

        private void ApplyAnimation()
        {
            foreach (RewardItemView view in m_boundViews) {
                Animate(view);
            }
        }

        private void Animate(RewardItemView view)
        {
            // Schedule by reward index, never by the order pooled cells are created.
            float localTime = IsRevealing ? m_elapsed - m_initialDelay - view.DataIndex * m_interval : m_popDuration;
            view.Animate(localTime, m_popDuration, RiseDistance, ScaleOvershoot);
        }

        private void UpdateStatus()
        {
            int count = Grid.ItemCount;
            int started = !IsRevealing ? count : m_elapsed <= m_initialDelay ? 0 :
                m_interval <= 0f ? count : Mathf.Min(count, Mathf.CeilToInt((m_elapsed - m_initialDelay) / m_interval));
            if (started == m_startedCount && IsRevealing) {
                return;
            }

            m_startedCount = started;
            if (Status != null) {
                Status.text = count == 0 ? "No rewards / receive a new batch to play again" :
                    IsRevealing ? "Revealing rewards  " + started + " / " + count :
                    "All " + count + " rewards received / receive again to replay";
            }
        }

        private void OnDisable()
        {
            ShowAllRewards();
        }

        private void OnDestroy()
        {
            if (Grid != null && m_initialized) {
                Grid.OnItemRender -= OnItemRender;
                Grid.OnItemRecycle -= OnItemRecycle;
            }

            m_boundViews.Clear();
        }
    }
}
