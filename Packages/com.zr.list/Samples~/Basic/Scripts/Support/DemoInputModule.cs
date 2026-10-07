// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZRList.Samples.Support
{
    /// <summary>Selects the host project's input backend before the first EventSystem update.</summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EventSystem))]
    public sealed class DemoInputModule: MonoBehaviour
    {
        private void Awake()
        {
            Type moduleType;
#if ENABLE_INPUT_SYSTEM
            // Reflection keeps the Input System package optional. link.xml preserves
            // this component in player builds; its OnEnable assigns default UI actions.
            moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType == null) {
                Debug.LogError("ZRList Demos: Input System is enabled, but its UI input module could not be loaded. Check the Input System package installation.", this);
                return;
            }
#else
            moduleType = typeof(StandaloneInputModule);
#endif
            BaseInputModule selectedModule = GetComponent(moduleType) as BaseInputModule;
            foreach (BaseInputModule module in GetComponents<BaseInputModule>()) {
                if (module != selectedModule) {
                    module.enabled = false;
                }
            }

            if (selectedModule == null) {
                selectedModule = (BaseInputModule)gameObject.AddComponent(moduleType);
            }
            selectedModule.enabled = true;
        }
    }
}
