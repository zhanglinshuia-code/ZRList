// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;
using UnityEngine.EventSystems;

namespace ZRList.Samples
{
    /// <summary>Owns item drag gestures so they are not handled by the parent ScrollRect.</summary>
    public sealed class ItemDragHandle: MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ItemDragDemo Owner;
        public RectTransform ItemRoot;

        public void OnBeginDrag(PointerEventData eventData)
        {
            Owner?.BeginItemDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Owner?.MoveItemDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Owner?.EndItemDrag(eventData);
        }

        private void OnDisable()
        {
            Owner?.CancelItemDrag(this);
        }
    }
}
