using Unity.Entities;
using UnityEngine;


namespace War
{
    using Dots.Component;
    using Dots.Component.ComponentSystem;


    public class SpawnDecalRenderer : MonoBehaviour
    {
        [SerializeField] private GameObject redTeamSpawnDecal;
        [SerializeField] private GameObject blueTeamSpawnDecal;


        private EntityManager _entityManager;
        private EntityQuery _spawnInputQuery;

        private bool _setSpawnSoldierData;
        private SpawnSoldierData _spawnSoldierData;


        private void Awake() => _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        private void Start()
        {
            redTeamSpawnDecal.SetActive(false);
            blueTeamSpawnDecal.SetActive(false);

            _spawnInputQuery =
                _entityManager.CreateEntityQuery(
                    typeof(SpawnInput),
                    typeof(SpawnDecalPosition));
        }

        private void Update()
        {
            if (_spawnInputQuery.IsEmpty)
            {
                redTeamSpawnDecal.SetActive(false);
                blueTeamSpawnDecal.SetActive(false);

                return;
            }

            Entity spawnInputEntity = _spawnInputQuery.GetSingletonEntity();

            if (_entityManager.IsComponentEnabled<UnsetSpawnData>(spawnInputEntity))
            {
                _setSpawnSoldierData = false;

                _entityManager.SetComponentEnabled<UnsetSpawnData>(spawnInputEntity, false);

                redTeamSpawnDecal.SetActive(false);
                blueTeamSpawnDecal.SetActive(false);

                return;
            }

            if (_entityManager.IsComponentEnabled<SetSpawnData>(spawnInputEntity))
            {
                _setSpawnSoldierData = true;

                _entityManager.SetComponentEnabled<SetSpawnData>(spawnInputEntity, false);

                _spawnSoldierData = _spawnInputQuery.GetSingleton<SpawnInput>().SpawnSoldierData;
            }

            if (_setSpawnSoldierData)
            {
                Transform decalTransform;

                switch (_spawnSoldierData.TeamColor)
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

                decalTransform.position = _spawnInputQuery.GetSingleton<SpawnDecalPosition>().Position;
            }
        }
    }
}