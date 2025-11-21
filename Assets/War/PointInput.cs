using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


namespace War
{
    using Dots.Component.ComponentSystem;


    public class PointInput : MonoBehaviour, BattleInputAction.IPointerActions
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;


        private BattleInputAction _battleInputAction;
        private BattleInputAction.PointerActions _action;

        private bool _isPressStarted;
        private bool _isPressCanceled;
        private bool _isMoving;
        private bool _isDragging;


        private void Awake()
        {
            cinemachineBrain ??= Camera.main?.GetComponent<CinemachineBrain>();

            _battleInputAction = new BattleInputAction();
            _action = _battleInputAction.Pointer;
            _action.AddCallbacks(this);
        }

        private void OnDestroy() => _battleInputAction.Dispose();

        private void OnEnable() => _action.Enable();
        private void OnDisable() => _action.Disable();

        private void LateUpdate()
        {
            if (_isPressStarted)
            {
                _isPressStarted = false;

                if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                {
                    Vector2 pressPoint = _action.position.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(pressPoint);

                    EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                    PlayerInputSystem.OnPointerPressStarted(
                        entityManager,
                        pressPoint,
                        new Unity.Physics.Ray
                        {
                            Origin = ray.origin,
                            Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                        });

                    _isDragging = true;
                }
            }

            if (_isPressCanceled)
            {
                _isPressCanceled = false;

                if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                {
                    _isDragging = false;

                    Vector2 releasePoint = _action.position.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(releasePoint);

                    EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                    PlayerInputSystem.OnPointerPressCanceled(
                        entityManager,
                        releasePoint,
                        new Unity.Physics.Ray
                        {
                            Origin = ray.origin,
                            Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                        });
                }
            }

            if (_isMoving)
            {
                _isMoving = false;

                if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                {
                    Vector2 point = _action.position.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(point);

                    EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                    if (_isDragging)
                    {
                        PlayerInputSystem.OnPointerDragging(
                            entityManager,
                            point,
                            new Unity.Physics.Ray
                            {
                                Origin = ray.origin,
                                Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                            });
                    }
                    else
                    {
                        PlayerInputSystem.OnPointerMove(
                            entityManager,
                            point,
                            new Unity.Physics.Ray
                            {
                                Origin = ray.origin,
                                Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                            });
                    }
                }
            }
        }

        public void OnPress(InputAction.CallbackContext context)
        {
            if (context.started)
            {
                _isPressStarted = true;
            }

            if (context.canceled)
            {
                _isPressCanceled = true;
            }
        }

        public void OnPosition(InputAction.CallbackContext context)
        {
            _isMoving = true;
        }
    }
}