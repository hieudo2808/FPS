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

        public int Capacity => poolSize;
        public int AvailableCount => objectPool != null ? objectPool.Count : 0;

        private void Awake()
        {
            objectPool = new Queue<GameObject>();
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(objectPrefab);
                obj.SetActive(false);
                obj.name = objectPrefab.name + "_" + (i + 1);
                objectPool.Enqueue(obj);
            }
        }

        public GameObject GetObject()
        {
            if (objectPool.Count > 0)
            {
                GameObject obj = objectPool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            GameLog.Warning("[ObjectPooling] Pool is empty, consider increasing pool size.");
            return null;
        }

        public void ReturnObject(GameObject obj)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                objectPool.Enqueue(obj);
                return;
            }

            GameLog.Warning("[ObjectPooling] Attempted to return a null object to the pool.");
        }

        public bool HasCapacityFor(int expectedActiveCount)
        {
            return poolSize >= expectedActiveCount;
        }
    }
}
