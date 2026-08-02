using UnityEngine;

namespace FPS
{
    public class SceneSingleton<T> : MonoBehaviour where T : Component
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();
                    if (instance == null)
                    {
                        Debug.LogWarning($"[SceneSingleton] {typeof(T).Name} is requested but not found in the current scene.");
                    }
                }
                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[SceneSingleton] Destroying duplicate instance of {typeof(T).Name}");
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        
        public static bool HasInstance => instance != null;
    }
}
