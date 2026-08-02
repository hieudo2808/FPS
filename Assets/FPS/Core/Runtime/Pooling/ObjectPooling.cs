using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class ObjectPooling : MonoBehaviour
    {
        [SerializeField] private GameObject objectPrefab;
        [SerializeField] private int poolSize = 100;

        private Queue<GameObject> objectPool;
        private int createdObjectCount;

        public int Capacity => poolSize;
        public int AvailableCount => objectPool != null ? objectPool.Count : 0;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (objectPool != null)
                return;

            objectPool = new Queue<GameObject>();
            if (objectPrefab == null)
            {
                GameLog.Error("[ObjectPooling] Cannot initialize because objectPrefab is missing.");
                return;
            }

            for (int i = 0; i < Mathf.Max(0, poolSize); i++)
            {
                GameObject obj = CreatePooledObject(++createdObjectCount);
                if (obj != null)
                    objectPool.Enqueue(obj);
            }
        }

        public GameObject GetObject()
        {
            EnsureInitialized();
            if (objectPool.Count > 0)
            {
                GameObject obj = objectPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            // A transient burst can legitimately exceed the warm pool size. Grow
            // the pool instead of dropping a visual effect and emitting a runtime
            // warning that the caller cannot recover from.
            GameObject expandedObject = CreatePooledObject(++createdObjectCount);
            if (expandedObject == null)
                return null;

            expandedObject.SetActive(true);
            return expandedObject;
        }

        public void ReturnObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                objectPool.Enqueue(obj);
                return;
            }

            // Returning null can happen during teardown when a pooled object was
            // already destroyed. Treat it as an idempotent no-op.
        }

        public bool HasCapacityFor(int expectedActiveCount)
        {
            return poolSize >= expectedActiveCount;
        }

        private GameObject CreatePooledObject(int index)
        {
            if (objectPrefab == null)
            {
                GameLog.Error("[ObjectPooling] Cannot create an object because objectPrefab is missing.");
                return null;
            }

            GameObject obj = Instantiate(objectPrefab);
            obj.SetActive(false);
            obj.name = objectPrefab.name + "_" + index;
            return obj;
        }
    }
}
