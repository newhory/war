using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using War.Game;


namespace War
{
    using Game.Systems;


    public class PointInput : MonoBehaviour, BattleInputAction.IPointerActions
    {
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private GameObject redTeamSpawnDecal;
        [SerializeField] private GameObject blueTeamSpawnDecal;

        
        private static bool isSetCurrentSpawnSoldierData;
        private static SpawnSoldierData currentSpawnSoldierData;
        
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            isSetCurrentSpawnSoldierData = false;
            currentSpawnSoldierData = default;
        }
        
        
        public static bool IsSetCurrentSpawnSoldierData
        {
            get => isSetCurrentSpawnSoldierData;
            set => isSetCurrentSpawnSoldierData = value;
        }

        public static SpawnSoldierData CurrentSpawnSoldierData
        {
            get => currentSpawnSoldierData;
            set => currentSpawnSoldierData = value;
        }


        private BattleInputAction _battleInputAction;
        private BattleInputAction.PointerActions _action;

        private World _clientWorld;

        private bool _isPressStarted;
        private bool _isPressCanceled;
        private bool _isMoving;
        private bool _isDragging;

        private RaycastHit[] _raycastHitBuffer;
        private int _groundLayerMask;
        
        private Vector3 _lastGroundPosition;


        private void Awake()
        {
            cinemachineBrain ??= Camera.main?.GetComponent<CinemachineBrain>();

            _battleInputAction = new BattleInputAction();
            _action = _battleInputAction.Pointer;
            _action.AddCallbacks(this);

            _raycastHitBuffer = new RaycastHit[16];
            _groundLayerMask = 1 << LayerMask.NameToLayer("World");
        }

        private void OnDestroy() => _battleInputAction.Dispose();

        private void OnEnable() => _action.Enable();
        private void OnDisable() => _action.Disable();

        private void Start()
        {
            //for (int i = 0, count = World.All.Count; i < count; ++i)
            //{
            //    if (World.All[i].Flags == WorldFlags.GameClient)
            //    {
            //        _clientWorld = World.All[i];
            //        
            //        break;
            //    }
            //}

            _clientWorld = World.DefaultGameObjectInjectionWorld;
            
            redTeamSpawnDecal.SetActive(false);
            blueTeamSpawnDecal.SetActive(false);
        }

        private void Update()
        {
            if (_isPressStarted)
            {
                _isPressStarted = false;

                if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                {
                    Vector2 pressPoint = _action.position.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(pressPoint);

                    int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitBuffer, cinemachineBrain.OutputCamera.farClipPlane, _groundLayerMask);
                    if (hitCount > 0)
                    {
                        EntityManager entityManager = _clientWorld.EntityManager;

                        Game.Systems.PlayerInputSystem.OnPointerPressStarted(
                            entityManager,
                            _raycastHitBuffer[0].point,
                            pressPoint,
                            new Unity.Physics.Ray
                            {
                                Origin = ray.origin,
                                Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                            });

                        _isDragging = true;
                    }
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

                    int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitBuffer, cinemachineBrain.OutputCamera.farClipPlane, _groundLayerMask);
                    if (hitCount > 0)
                    {
                        EntityManager entityManager = _clientWorld.EntityManager;

                        Game.Systems.PlayerInputSystem.OnPointerPressCanceled(entityManager, _raycastHitBuffer[0].point);
                    }
                }
            }

            if (_isMoving)
            {
                _isMoving = false;

                if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
                {
                    Vector2 point = _action.position.ReadValue<Vector2>();
                    Ray ray = cinemachineBrain.OutputCamera.ScreenPointToRay(point);

                    int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitBuffer, cinemachineBrain.OutputCamera.farClipPlane, _groundLayerMask);
                    if (hitCount > 0)
                    {
                        EntityManager entityManager = _clientWorld.EntityManager;

                        if (_isDragging)
                        {
                            Game.Systems.PlayerInputSystem.OnPointerDragging(
                                entityManager,
                                _raycastHitBuffer[0].point,
                                point,
                                new Unity.Physics.Ray
                                {
                                    Origin = ray.origin,
                                    Displacement = ray.direction * cinemachineBrain.OutputCamera.farClipPlane
                                });
                        }
                        else
                        {
                            _lastGroundPosition = _raycastHitBuffer[0].point;
                            
                            Game.Systems.PlayerInputSystem.OnPointerMove(entityManager, _lastGroundPosition);
                        }
                    }
                }
            }
        }
        
        private void LateUpdate()
        {
            if (!IsSetCurrentSpawnSoldierData)
            {
                redTeamSpawnDecal.SetActive(false);
                blueTeamSpawnDecal.SetActive(false);

                return;
            }

            Transform decalTransform;

            switch (CurrentSpawnSoldierData.TeamColor)
            {
                case TeamColor.Red:
                    redTeamSpawnDecal.SetActive(true);
                    blueTeamSpawnDecal.SetActive(false);
                    decalTransform = redTeamSpawnDecal.transform;
                    break;
                case TeamColor.Blue:
                    redTeamSpawnDecal.SetActive(false);
                    blueTeamSpawnDecal.SetActive(true);
                    decalTransform = blueTeamSpawnDecal.transform;
                    break;
                default:
                    redTeamSpawnDecal.SetActive(false);
                    blueTeamSpawnDecal.SetActive(false);
                    return;
            }

            decalTransform.position = _lastGroundPosition;
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