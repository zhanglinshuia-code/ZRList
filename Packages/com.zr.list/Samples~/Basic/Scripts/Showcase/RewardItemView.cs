// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList.Samples
{
    /// <summary>Animates the visual child while the grid owns the cell's layout root.</summary>
    public sealed class RewardItemView
    {
        public readonly RectTransform Visual;
        public readonly CanvasGroup Group;
        public readonly InventorySlotView Slot;
        public int DataIndex { get; private set; }

        public RewardItemView(RectTransform root)
        {
            Visual = (RectTransform)root.Find("Visual");
            Group = Visual.GetComponent<CanvasGroup>();
            Slot = new InventorySlotView(Visual, false);
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }

        public void SetData(int index, string name, string symbol, int quantity, Color color)
        {
            DataIndex = index;
            Slot.SetData(index, name, symbol, quantity, color, false, null);
        }

        public void Animate(float elapsed, float duration, float riseDistance, float overshoot)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            if (elapsed <= 0f) {
                SetVisual(0f, 0.55f, -riseDistance);
                return;
            }

            if (progress >= 1f) {
                ResetVisual();
                return;
            }

            float scale;
            if (progress < 0.65f) {
                float phase = progress / 0.65f;
                float remaining = 1f - phase;
                float eased = 1f - remaining * remaining * remaining;
                scale = Mathf.LerpUnclamped(0.55f, 1f + overshoot, eased);
            }
            else {
                float phase = (progress - 0.65f) / 0.35f;
                scale = Mathf.Lerp(1f + overshoot, 1f, Mathf.SmoothStep(0f, 1f, phase));
            }

            float remainingProgress = 1f - progress;
            SetVisual(Mathf.Clamp01(progress * 5f), scale, -riseDistance * remainingProgress * remainingProgress * remainingProgress);
        }

        public void Unbind()
        {
            Slot.Unbind();
            ResetVisual();
            DataIndex = -1;
        }

        private void ResetVisual()
        {
            SetVisual(1f, 1f, 0f);
        }

        private void SetVisual(float alpha, float scale, float positionY)
        {
            // Waiting and completed rewards share the timeline but do not change
            // visually. Avoid dirtying their Canvas/Transform on every frame.
            if (Group.alpha != alpha) {
                Group.alpha = alpha;
            }

            Vector3 localScale = Vector3.one * scale;
            if (Visual.localScale != localScale) {
                Visual.localScale = localScale;
            }

            Vector2 position = new Vector2(0f, positionY);
            if (Visual.anchoredPosition != position) {
                Visual.anchoredPosition = position;
            }
        }
    }
}
