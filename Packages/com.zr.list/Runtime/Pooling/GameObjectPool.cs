// Copyright 2026 zhanglinshuia-code
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using UnityEngine;

namespace ZRList
{
    public class GameObjectPool
    {
        public GameObject Prefab;
        public Queue<GameObject> AvailableObjects = new Queue<GameObject>();
        public int InitialCapacity = 16;
        public int MaxCapacity = 25;
        public void Init()
        {
            Dispose();
            int count = Mathf.Clamp(InitialCapacity, 0, Mathf.Max(0, MaxCapacity));
            for (int i = 0; i < count; ++i) {
                var obj = GameObject.Instantiate(Prefab);
                obj.SetActive(false);
                AvailableObjects.Enqueue(obj);
            }
        }

        public GameObject Get()
        {
            GameObject obj = null;
            while (AvailableObjects.Count > 0 && obj == null) {
                obj = AvailableObjects.Dequeue();
            }

            if (obj == null) {
                obj = GameObject.Instantiate(Prefab);
            }

            obj.SetActive(true);
            return obj;
        }

        public void Remove(GameObject obj)
        {
            if (obj == null || AvailableObjects.Contains(obj)) {
                return;
            }

            if (AvailableObjects.Count < Mathf.Max(0, MaxCapacity)) {
                obj.SetActive(false);
                AvailableObjects.Enqueue(obj);
            }
            else {
                Release(obj);
            }
        }

        public void Dispose()
        {
            while (AvailableObjects.Count > 0) {
                Release(AvailableObjects.Dequeue());
            }
        }

        private static void Release(GameObject obj)
        {
            if (obj == null) {
                return;
            }

            obj.SetActive(false);
            if (Application.isPlaying) {
                GameObject.Destroy(obj);
            }
            else {
                GameObject.DestroyImmediate(obj);
            }
        }
    }
}
