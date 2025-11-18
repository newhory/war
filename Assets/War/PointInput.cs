using System;
using System.Threading;
using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;


namespace War
{
    using Dots.Component.ComponentSystem;


    public class PointInput : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;


        private InputAction _pressAction;
        private InputAction _pointerPositionAction;

        private CancellationTokenSource _ctsDragging;


        private void Awake() => cinemachineBrain ??= Camera.main?.GetComponent<CinemachineBrain>();

        private void OnEnable()
        {
            _pressAction = new InputAction(type: InputActionType.Button, binding: "<Pointer>/press");
            _pointerPositionAction = new InputAction(type: InputActionType.Value, binding: "<Pointer>/position");

            _pressAction.started += OnPressStarted;
            _pressAction.canceled += OnPressCanceled;

            _pressAction.Enable();
            _pointerPositionAction.Enable();
        }

        private void OnDisable()
        {
            if (_ctsDragging is not null)
            {
                _ctsDragging.Cancel();
                _ctsDragging.Dispose();
                _ctsDragging = null;
            }

            _pressAction.Disable();
            _pointerPositionAction.Disable();

            _pressAction.started -= OnPressStarted;
            _pressAction.canceled -= OnPressCanceled;

            _pressAction.Dispose();
            _pointerPositionAction.Dispose();
        }

        private void OnPressStarted(InputAction.CallbackContext ctx)
        {
            if (_ctsDragging is not null)
            {
                _ctsDragging.Cancel();
                _ctsDragging.Dispose();
                _ctsDragging = null;
            }

            Vector2 pressPoint = _pointerPositionAction.ReadValue<Vector2>();
            Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(pressPoint);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            PlayerInputSystem.OnPressStarted(
                entityManager,
                pressPoint,
                new Unity.Physics.Ray
                {
                    Origin = ray.origin,
                    Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                });

            _ctsDragging = new CancellationTokenSource();
            OnDragging(_ctsDragging.Token).Forget();
        }

        private async UniTask OnDragging(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                    Vector2 dragPoint = _pointerPositionAction.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(dragPoint);

                    EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

                    PlayerInputSystem.OnDragging(
                        entityManager,
                        dragPoint,
                        new Unity.Physics.Ray
                        {
                            Origin = ray.origin,
                            Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                        });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void OnPressCanceled(InputAction.CallbackContext ctx)
        {
            if (_ctsDragging is not null)
            {
                _ctsDragging.Cancel();
                _ctsDragging.Dispose();
                _ctsDragging = null;
            }

            Vector2 releasePoint = _pointerPositionAction.ReadValue<Vector2>();
            Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(releasePoint);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            PlayerInputSystem.OnPressCanceled(
                entityManager,
                releasePoint,
                new Unity.Physics.Ray
                {
                    Origin = ray.origin,
                    Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                });
        }
    }
}