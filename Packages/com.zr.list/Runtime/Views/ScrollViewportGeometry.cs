// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace ZRList
{
    /// <summary>Viewport-local rectangle tests without temporary arrays or descendant traversal.</summary>
    internal readonly struct ScrollViewportGeometry
    {
        private readonly Matrix4x4 m_worldToLocal;
        private readonly Rect m_rect;

        public ScrollViewportGeometry(RectTransform viewport)
        {
            m_worldToLocal = viewport != null ? viewport.worldToLocalMatrix : Matrix4x4.identity;
            m_rect = viewport != null ? viewport.rect : default;
        }

        public bool IsVisible(RectTransform item, bool fullyVisible)
        {
            if (item == null || !item.gameObject.activeInHierarchy || m_rect.width <= 0f || m_rect.height <= 0f) {
                return false;
            }

            Rect rect = item.rect;
            if (rect.width <= 0f || rect.height <= 0f) {
                return false;
            }

            Matrix4x4 matrix = m_worldToLocal * item.localToWorldMatrix;
            Vector2 a = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin));
            Vector2 b = matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMin));
            Vector2 c = matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMax));
            Vector2 d = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMax));
            Vector2 min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            Vector2 max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            if (max.x <= m_rect.xMin || min.x >= m_rect.xMax || max.y <= m_rect.yMin || min.y >= m_rect.yMax) {
                return false;
            }

            if (fullyVisible) {
                return min.x >= m_rect.xMin && max.x <= m_rect.xMax && min.y >= m_rect.yMin && max.y <= m_rect.yMax;
            }

            // The viewport axes were checked above. Test the item's edge normals
            // as well so rotated rectangles do not report false intersections.
            Vector2 horizontal = b - a;
            Vector2 vertical = d - a;
            return OverlapsOnAxis(new Vector2(-horizontal.y, horizontal.x), a, b, c, d) && OverlapsOnAxis(new Vector2(-vertical.y, vertical.x), a, b, c, d);
        }

        private bool OverlapsOnAxis(Vector2 axis, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (axis.sqrMagnitude == 0f) {
                return false;
            }

            float pa = Vector2.Dot(axis, a);
            float pb = Vector2.Dot(axis, b);
            float pc = Vector2.Dot(axis, c);
            float pd = Vector2.Dot(axis, d);
            float itemMin = Mathf.Min(Mathf.Min(pa, pb), Mathf.Min(pc, pd));
            float itemMax = Mathf.Max(Mathf.Max(pa, pb), Mathf.Max(pc, pd));
            float center = Vector2.Dot(axis, m_rect.center);
            float radius = (Mathf.Abs(axis.x) * m_rect.width + Mathf.Abs(axis.y) * m_rect.height) * 0.5f;
            return itemMax > center - radius && itemMin < center + radius;
        }
    }
}
