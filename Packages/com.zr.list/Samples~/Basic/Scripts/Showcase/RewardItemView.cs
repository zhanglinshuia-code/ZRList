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
                Group.alpha = 0f;
                Visual.localScale = Vector3.one * 0.55f;
                Visual.anchoredPosition = new Vector2(0f, -riseDistance);
                return;
            }

            if (progress >= 1f) {
                ResetVisual();
                return;
            }

            float scale;
            if (progress < 0.65f) {
                float phase = progress / 0.65f;
                float eased = 1f - Mathf.Pow(1f - phase, 3f);
                scale = Mathf.LerpUnclamped(0.55f, 1f + overshoot, eased);
            }
            else {
                float phase = (progress - 0.65f) / 0.35f;
                scale = Mathf.Lerp(1f + overshoot, 1f, Mathf.SmoothStep(0f, 1f, phase));
            }

            Group.alpha = Mathf.Clamp01(progress * 5f);
            Visual.localScale = Vector3.one * scale;
            Visual.anchoredPosition = new Vector2(0f, -riseDistance * Mathf.Pow(1f - progress, 3f));
        }

        public void Unbind()
        {
            Slot.Unbind();
            ResetVisual();
            DataIndex = -1;
        }

        private void ResetVisual()
        {
            if (Group.alpha != 1f) {
                Group.alpha = 1f;
            }

            if (Visual.localScale != Vector3.one) {
                Visual.localScale = Vector3.one;
            }

            if (Visual.anchoredPosition != Vector2.zero) {
                Visual.anchoredPosition = Vector2.zero;
            }
        }
    }
}
