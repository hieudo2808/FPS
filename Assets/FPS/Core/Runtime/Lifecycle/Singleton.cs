using UnityEngine;

namespace FPS
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        private static T instance;
        private static readonly object lockObject = new object();
        private static bool applicationIsQuitting = false;

        public static T Instance
        {
            get
            {
                if (applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again - returning null.");
                    return null;
                }

                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = FindAnyObjectByType<T>();

                        if (instance == null)
                        {
                            Setup();
                        }
                        else if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(instance.gameObject);
                        }
                    }
                    return instance;
                }
            }
        }

        private static void Setup()
        {
            GameObject singletonObject = new GameObject($"{typeof(T).Name} (Singleton)");
            instance = singletonObject.AddComponent<T>();
            if (Application.isPlaying)
                DontDestroyOnLoad(singletonObject);

            Debug.Log($"[Singleton] Created new instance of {typeof(T).Name}");
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                
                if (transform.parent != null)
                    transform.SetParent(null);
                    
                if (Application.isPlaying)
                    DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                // A scene reload can legitimately instantiate a second scene copy
                // while the DontDestroyOnLoad instance is retained. Destruction is
                // deterministic and should not be reported as a runtime warning.
                Debug.Log($"[Singleton] Destroying duplicate instance of {typeof(T).Name}");
                Destroy(gameObject);
                return;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static bool HasInstance => instance != null;

        public static void DestroySingleton()
        {
            if (instance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(instance.gameObject);
                }
                else
                {
                    DestroyImmediate(instance.gameObject);
                }
                instance = null;
            }
        }
    }
}
