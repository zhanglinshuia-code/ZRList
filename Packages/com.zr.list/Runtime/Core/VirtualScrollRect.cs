// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZRList
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "MyScrollRect")]
    public class VirtualScrollRect: ScrollRect
    {
        public bool IsDragging { get; private set; }

        private Vector2 m_previousPosition;
        private bool m_hasPreviousPosition;
        private ScrollRect m_dragParent;
        public override void Rebuild(CanvasUpdate executing)
        {
            base.Rebuild(executing);
            if (executing == CanvasUpdate.PostLayout && content != null) {
                m_previousPosition = content.anchoredPosition;
                m_hasPreviousPosition = true;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (content != null) {
                m_previousPosition = content.anchoredPosition;
                m_hasPreviousPosition = true;
            }
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            base.OnInitializePotentialDrag(eventData);
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive()) {
                return;
            }

            // Pressing an inner list also stops ancestor inertia, so the group
            // stays in place while the user drags its child content.
            for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent) {
                ancestor.TryGetComponent(out ScrollRect candidate);
                if (candidate != null && candidate.IsActive()) {
                    candidate.StopMovement();
                }
            }
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsActive() || eventData.button != PointerEventData.InputButton.Left) {
                return;
            }

            Vector2 delta = eventData.position - eventData.pressPosition;
            if (delta == Vector2.zero) {
                delta = eventData.delta;
            }

            m_dragParent = FindScrollParent(delta);
            if (m_dragParent != null) {
                // Keep subsequent EventSystem events on the stable parent, even
                // when the originating child is recycled during this gesture.
                eventData.pointerDrag = m_dragParent.gameObject;
                m_dragParent.OnInitializePotentialDrag(eventData);
                m_dragParent.OnBeginDrag(eventData);
                return;
            }

            base.OnBeginDrag(eventData);
            IsDragging = true;
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (m_dragParent != null) {
                m_dragParent.OnDrag(eventData);
            }
            else {
                base.OnDrag(eventData);
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) {
                return;
            }

            if (m_dragParent != null) {
                m_dragParent.OnEndDrag(eventData);
                m_dragParent = null;
                return;
            }

            base.OnEndDrag(eventData);
            IsDragging = false;
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (!IsActive()) {
                return;
            }

            ScrollRect parent = FindScrollParent(eventData.scrollDelta);
            if (parent != null) {
                parent.OnScroll(eventData);
            }
            else {
                base.OnScroll(eventData);
            }
        }

        private ScrollRect FindScrollParent(Vector2 delta)
        {
            if (delta == Vector2.zero) {
                return null;
            }

            bool horizontalGesture = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            if (horizontalGesture ? horizontal : vertical) {
                return null;
            }

            for (Transform ancestor = transform.parent; ancestor != null; ancestor = ancestor.parent) {
                ancestor.TryGetComponent(out ScrollRect candidate);
                if (candidate != null && candidate.IsActive() && (horizontalGesture ? candidate.horizontal : candidate.vertical)) {
                    return candidate;
                }
            }

            return null;
        }

        protected override void OnDisable()
        {
            m_dragParent = null;
            IsDragging = false;
            m_hasPreviousPosition = false;
            base.OnDisable();
        }

        public void CompensateContentPosition(int axisIndex, float delta)
        {
            m_ContentStartPosition[axisIndex] += delta;
            if (!m_hasPreviousPosition || content == null || delta == 0f) {
                return;
            }

            // Rebase ScrollRect's previous sample as well as its drag origin. Layout
            // corrections must not be interpreted as a large pointer velocity.
            m_previousPosition[axisIndex] += delta;
            Vector2 actual = content.anchoredPosition;
            content.anchoredPosition = m_previousPosition;
            UpdatePrevData();
            content.anchoredPosition = actual;
        }
    }
}
