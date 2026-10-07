// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.UI;

namespace ZRList.Samples
{
    // Runs after ScrollRect inertia and the list's layout transaction have settled.
    [DefaultExecutionOrder(100)]
    public sealed class CurvedScrollDemo: MonoBehaviour
    {
        public VirtualScrollView ScrollView;
        public Texture2D[] Icons;
        public Text Status;
        [Min(0)] public int DataCount = 1000;
        public ScrollCurve Curve = new ScrollCurve();
        public bool RotateAlongCurve = true;
        public float CrossAxisOffset = -80f;

        private string[] m_titles;
        private bool m_initialized;
        private bool m_dirty = true;
        private Vector2 m_previousContentPosition;
        private Rect m_previousViewportRect;

        private void Start()
        {
            InitializeDemo();
        }

        public void InitializeDemo()
        {
            if (m_initialized) {
                return;
            }

            int count = Mathf.Max(0, DataCount);
            m_titles = new string[count];
            for (var index = 0; index < count; ++index) {
                m_titles[index] = "CARD " + (index + 1).ToString("D4");
            }

            Curve.RebuildCache();
            ScrollView.ViewStateChanged += MarkDirty;
            ScrollView.OnItemRecycle += OnItemRecycle;
            ScrollView.Initialize(count);
            ScrollView.OnItemRender += OnItemRender;
            m_initialized = true;
            ApplyCurve();
            UpdateStatus();
        }

        public void OnItemRender(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent == null) {
                item.CachedComponent = new CurvedItemView(item.Root);
            }

            Texture icon = Icons != null && Icons.Length > 0 ? Icons[dataIndex % Icons.Length] : null;
            ((CurvedItemView)item.CachedComponent).SetData(m_titles[dataIndex], icon, Color.HSVToRGB((dataIndex % 5) / 5f, 0.45f, 0.95f));
            m_dirty = true;
        }

        private void OnItemRecycle(ScrollItemView item, int dataIndex)
        {
            if (item.CachedComponent is CurvedItemView view) {
                view.ResetVisual();
            }
        }

        private void LateUpdate()
        {
            if (!m_initialized || !ScrollView.isActiveAndEnabled) {
                return;
            }

            if (m_dirty || m_previousContentPosition != ScrollView.Content.anchoredPosition || m_previousViewportRect != ScrollView.Viewport.rect) {
                ApplyCurve();
            }
        }

        /// <summary>Also callable after an immediate jump or a runtime settings change.</summary>
        public void ApplyCurve()
        {
            if (!m_initialized || ScrollView.Content == null || ScrollView.Viewport == null) {
                return;
            }

            RectTransform viewport = ScrollView.Viewport;
            Rect rect = viewport.rect;
            bool horizontal = ScrollView.IsHorizontal;
            float halfExtent = (horizontal ? rect.width : rect.height) * 0.5f;
            Matrix4x4 worldToViewport = viewport.worldToLocalMatrix;
            Matrix4x4 viewportToWorld = viewport.localToWorldMatrix;
            var items = ScrollView.VisibleItems;
            for (var index = 0; index < items.Count; ++index) {
                if (!(items[index].CachedComponent is CurvedItemView view)) {
                    continue;
                }

                Vector3 center = worldToViewport.MultiplyPoint3x4(view.Root.TransformPoint(view.Root.rect.center));
                float distance = horizontal ? center.x - rect.center.x : rect.center.y - center.y;
                Curve.Evaluate(distance, halfExtent, out float offset, out float angle);
                float crossOffset = (horizontal ? rect.center.y - center.y : rect.center.x - center.x) + CrossAxisOffset + offset;
                Vector3 delta = horizontal ? new Vector3(0f, crossOffset, 0f) : new Vector3(crossOffset, 0f, 0f);
                Vector3 localOffset = view.Root.worldToLocalMatrix.MultiplyVector(viewportToWorld.MultiplyVector(delta));
                view.Apply(localOffset, RotateAlongCurve ? angle : 0f);
            }

            m_previousContentPosition = ScrollView.Content.anchoredPosition;
            m_previousViewportRect = rect;
            m_dirty = false;
        }

        public void UseArc()
        {
            Curve.Mode = ScrollCurveMode.Arc;
            SettingsChanged();
        }

        public void UseCustomCurve()
        {
            Curve.Mode = ScrollCurveMode.Custom;
            Curve.RebuildCache();
            SettingsChanged();
        }

        public void IncreaseRadius()
        {
            Curve.ArcRadius += 100f;
            UseArc();
        }

        public void DecreaseRadius()
        {
            Curve.ArcRadius = Mathf.Max(600f, Curve.ArcRadius - 100f);
            UseArc();
        }

        public void FlipCurve()
        {
            Curve.Invert = !Curve.Invert;
            SettingsChanged();
        }

        public void ToggleRotation()
        {
            RotateAlongCurve = !RotateAlongCurve;
            SettingsChanged();
        }

        public void JumpToFirst()
        {
            ScrollView.JumpToDataItem(0, 0.3f);
        }

        public void JumpToLast()
        {
            ScrollView.JumpToDataItem(ScrollView.ItemCount - 1, 0.3f);
        }

        private void SettingsChanged()
        {
            ApplyCurve();
            UpdateStatus();
        }

        private void MarkDirty()
        {
            m_dirty = true;
        }

        private void UpdateStatus()
        {
            if (Status != null) {
                Status.text = (Curve.Mode == ScrollCurveMode.Arc ? "ARC  /  radius " + Curve.ArcRadius.ToString("F0") : "CUSTOM  /  edit AnimationCurve in Inspector") + "  /  " + (RotateAlongCurve ? "tangent rotation" : "upright cards");
            }
        }

        private void OnValidate()
        {
            if (Curve != null) {
                Curve.RebuildCache();
            }

            m_dirty = true;
        }

        private void OnEnable()
        {
            m_dirty = true;
        }

        private void OnDisable()
        {
            if (!m_initialized || ScrollView == null) {
                return;
            }

            var items = ScrollView.VisibleItems;
            for (var index = 0; index < items.Count; ++index) {
                if (items[index].CachedComponent is CurvedItemView view) {
                    view.ResetVisual();
                }
            }
        }

        private void OnDestroy()
        {
            if (ScrollView != null) {
                ScrollView.ViewStateChanged -= MarkDirty;
                ScrollView.OnItemRender -= OnItemRender;
                ScrollView.OnItemRecycle -= OnItemRecycle;
            }
        }
    }
}
