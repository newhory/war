using System;
using UnityEngine;


namespace War
{
    public abstract class SingletonSceneObject<TComponent> : MonoBehaviour
        where TComponent : SingletonSceneObject<TComponent>
    {
#pragma warning disable UDR0001
        private static TComponent instance;
#pragma warning restore UDR0001


        public static TComponent Instance
        {
            get
            {
                if (instance is not null)
                {
                    return instance;
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

                    instance = objects[0];
                    instance.Init();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return instance;
            }
        }

        public static bool IsValid => instance;


        private void Awake()
        {
            if (IsValid)
            {
                return;
            }

            instance = this as TComponent;
            instance?.Init();
        }

        private void OnDestroy()
        {
            if (instance)
            {
                instance.Release();
                instance = null;
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