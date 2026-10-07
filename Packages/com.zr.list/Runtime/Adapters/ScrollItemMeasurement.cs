// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList
{
    /// <summary>Optional Unity-layout measurement used by item views and the legacy adapter.</summary>
    public static class ScrollItemMeasurement
    {
        public static float Measure(RectTransform root, ScrollOrientation orientation)
        {
            root.TryGetComponent(out ContentSizeFitter fitter);
            // Without a root layout controller the slot has a fixed size. Child
            // text/layout cannot resize it, so rebuilding that subtree adds no measurement.
            if (fitter == null && !root.TryGetComponent<ILayoutController>(out _)) {
                return root.rect.size[orientation == ScrollOrientation.Vertical ? 1 : 0];
            }

            if (fitter != null) {
                fitter.enabled = true;
            }

            try {
                LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                return root.rect.size[orientation == ScrollOrientation.Vertical ? 1 : 0];
            }
            finally {
                // The list owns the root geometry after measurement. Child layout
                // components remain untouched, including their prefab settings.
                if (fitter != null) {
                    fitter.enabled = false;
                }
            }
        }
    }
}
