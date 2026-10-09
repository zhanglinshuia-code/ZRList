// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

namespace ZRList
{
    // Optional readiness contract for callback-driven adapters. No views are bound until ready.
    internal interface IScrollItemRenderState
    {
        bool IsRendererReady { get; }
    }
}
