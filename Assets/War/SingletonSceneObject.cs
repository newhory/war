using System;
using UnityEngine;


namespace War
{
    public abstract class SingletonSceneObject<TComponent> : MonoBehaviour
        where TComponent : SingletonSceneObject<TComponent>
    {
        private static TComponent _instance;


        public static TComponent Instance
        {
            get
            {
                if (_instance is not null)
                {
                    return _instance;
                }

                try
                {
                    TComponent[] objects = FindObjectsByType<TComponent>(FindObjectsSortMode.None);

                    if (objects == null || objects.Length == 0)
                    {
                        throw new Exception($"There is no <{typeof(TComponent).Name}> exists in this scene.");
                    }

                    if (objects.Length > 1)
                    {
                        Debug.LogError($"[SceneObject<{typeof(TComponent).Name}>] Something went really wrong - there should never be more than 1 singleton! Reopening the scene might fix it.");
                    }

                    _instance = objects[0];
                    _instance.Init();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return _instance;
            }
        }

        public static bool IsValid => _instance;


        private void Awake()
        {
            if (IsValid)
            {
                return;
            }

            _instance = this as TComponent;
            _instance?.Init();
        }

        private void OnDestroy()
        {
            if (_instance)
            {
                _instance.Release();
                _instance = null;
            }
        }

        private void Start() => OnStart();

        protected virtual void Init()
        {
        }

        protected virtual void Release()
        {
        }

        protected virtual void OnStart()
        {
        }
    }
}