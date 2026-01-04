using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using ZLinq;
using Random = Unity.Mathematics.Random;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SpawnSystemGroup))]
    public partial struct SpawnSoldierSystem : ISystem, ISystemStartStop
    {
        private struct SoldierForSpawn
        {
            public int TroopId;

            public TeamColor TeamColor;
            public SoldierType SoldierType;
            public SoldierData SoldierData;

            public float3 Position;
            public quaternion Rotation;
        }

        private struct TroopForSpawn
        {
            public int TroopId;

            public float2 TroopPosition;
            public quaternion TroopRotation;
            public TeamColor TeamColor;
            public int TroopHorizonSoldierCount;
        }

        public struct JustSpawnedSoldier : IComponentData
        {
        }

        public struct JustSpawnedTroop : IComponentData
        {
        }

        [BurstCompile]
        private struct SpawnSoldierJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

#if DO_NOT_USE_ENTITIES_GRAPHICS
            [ReadOnly] public Entity SoldierProtoType;
#endif
            [ReadOnly] public NativeArray<SoldierForSpawn>.ReadOnly SoldierForSpawns;


            public void Execute(int index)
            {
                SoldierForSpawn soldierForSpawn = SoldierForSpawns[index];
                SoldierData soldierData = soldierForSpawn.SoldierData;

                Entity soldierEntity = EntityCommandBuffer.Instantiate(index,
#if DO_NOT_USE_ENTITIES_GRAPHICS
                    SoldierProtoType
#else
                    soldierForSpawn.TeamColor == TeamColor.Blue ? soldierForSpawn.SoldierData.BlueTeamProtoType : soldierForSpawn.SoldierData.RedTeamProtoType
#endif
                );

            #region troop

                EntityCommandBuffer.AddSharedComponent(index, soldierEntity, new Troop { Id = soldierForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Team { Color = soldierForSpawn.TeamColor });

            #endregion

            #region soldier

                EntityCommandBuffer.AddComponent(index, soldierEntity, new Soldier { Type = soldierForSpawn.SoldierType });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierAttachedTroop
                {
                    TroopId = soldierForSpawn.TroopId,
                    Radius = soldierData.radiusInFormation,
                });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierWeapon { Type = soldierData.weapon });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierTargetForAttack { TargetSoldier = Entity.Null });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierAnimation { Next = SoldierAnimation.State.Default });

            #endregion

            #region state

                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierStateMoveInFormation());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierStateMoveToTarget());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierStateAttackTarget());

            #endregion

            #region formation

                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierUpdatePositionInFormation());
                EntityCommandBuffer.SetComponentEnabled<SoldierUpdatePositionInFormation>(index, soldierEntity, false);

            #endregion

            #region transform

                EntityCommandBuffer.AddComponent(index, soldierEntity, new LocalTransform { Position = soldierForSpawn.Position, Rotation = soldierForSpawn.Rotation, Scale = 1f });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new NavMeshAgentData { Radius = soldierData.radius });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Velocity());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Acceleration { Value = float3.zero, Max = soldierForSpawn.SoldierData.moveAcceleration });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new MoveSpeed { Max = soldierData.moveSpeed, CurrentMax = soldierData.moveSpeed });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Forward { Value = math.forward(soldierForSpawn.Rotation) });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierDestination());

            #endregion

            #region combat

                EntityCommandBuffer.AddComponent(index, soldierEntity, new SearchTargetRange { Value = soldierData.searchTargetRange });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Attack());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new AttackPower { Value = soldierData.attackPower });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new AttackRange { Value = soldierData.attackRange });
                EntityCommandBuffer.AddComponent(
                    index,
                    soldierEntity,
                    new AttackData
                    {
                        Duration = soldierData.attackDuration,
                        HitTime = soldierData.attackHitTime,
                        Delay = soldierData.attackDelay
                    });

                EntityCommandBuffer.AddComponent(index, soldierEntity, new Health { Value = soldierData.health });

            #endregion

            #region buffer

                EntityCommandBuffer.AddBuffer<SpawnHitEffect>(index, soldierEntity);
                EntityCommandBuffer.AddBuffer<Damaged>(index, soldierEntity);

                if (soldierData.weapon == SoldierWeaponType.Arrow)
                {
                    EntityCommandBuffer.AddBuffer<SpawnArrow>(index, soldierEntity);
                }

            #endregion

            #region tag

                EntityCommandBuffer.AddComponent(index, soldierEntity, new Alive());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Movable());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Rotatable());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new UseDefaultMaxSpeed());

            #endregion

                EntityCommandBuffer.AddComponent(index, soldierEntity, new MovableChanged { IsMovable = false, IsChanged = false });

                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, soldierEntity, false);

                EntityCommandBuffer.AddComponent(index, soldierEntity, new JustSpawnedSoldier());
            }
        }

        [BurstCompile]
        private struct SpawnTroopJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public Entity TroopProtoType;
            [ReadOnly] public NativeArray<TroopForSpawn>.ReadOnly TroopForSpawns;


            public void Execute(int index)
            {
                TroopForSpawn troopForSpawn = TroopForSpawns[index];

                Entity troopEntity = EntityCommandBuffer.Instantiate(index, TroopProtoType);

            #region troop

                EntityCommandBuffer.AddSharedComponent(index, troopEntity, new Troop { Id = troopForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, troopEntity, new Team { Color = troopForSpawn.TeamColor });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopEntity
                {
                    Id = troopForSpawn.TroopId,
                    Entity = troopEntity,
                    HorizontalUnitCount = troopForSpawn.TroopHorizonSoldierCount,
                });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopTargetForAttack { TargetTroop = Entity.Null });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopSelected());
                EntityCommandBuffer.SetComponentEnabled<TroopSelected>(index, troopEntity, false);

                EntityCommandBuffer.AddBuffer<TroopSoldier>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopHullPoint>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopSoldierIndexBuffer>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopLowerSoldierIndexBuffer>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopUpperSoldierIndexBuffer>(index, troopEntity);

            #endregion

            #region formation

                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopFormationReset { TroopPosition = new float3(troopForSpawn.TroopPosition.x, 0f, troopForSpawn.TroopPosition.y) });

            #endregion

            #region transform

                EntityCommandBuffer.AddComponent(
                    index,
                    troopEntity,
                    LocalTransform.FromPositionRotationScale(
                        new float3(troopForSpawn.TroopPosition.x, 0f, troopForSpawn.TroopPosition.y),
                        troopForSpawn.TroopRotation,
                        1f));

            #endregion

            #region tag

                EntityCommandBuffer.AddComponent(index, troopEntity, new Alive());
                EntityCommandBuffer.AddComponent(index, troopEntity, new Movable());
                EntityCommandBuffer.AddComponent(index, troopEntity, new Rotatable());

                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopAISearchTarget());
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopAICheckTargetValid());

                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopStateMoveToDestination());
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopStateMoveToTarget());

            #endregion

                EntityCommandBuffer.SetComponentEnabled<TroopAISearchTarget>(index, troopEntity, false);
                EntityCommandBuffer.SetComponentEnabled<TroopAICheckTargetValid>(index, troopEntity, false);
                EntityCommandBuffer.SetComponentEnabled<TroopStateMoveToTarget>(index, troopEntity, false);

                EntityCommandBuffer.AddComponent(index, troopEntity, new JustSpawnedTroop());
            }
        }

        private static Entity s_spawnSoldierDataBufferEntity;
        private static int s_troopId;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeOnLoad()
        {
            s_spawnSoldierDataBufferEntity = Entity.Null;
            s_troopId = 0;
        }


        public static void SpawnSoldier(EntityManager entityManager, SpawnSoldierData spawn) => GetSpawnSoldierDataBuffer(entityManager).Add(spawn);

        private static DynamicBuffer<SpawnSoldierData> GetSpawnSoldierDataBuffer(EntityManager entityManager) =>
            s_spawnSoldierDataBufferEntity == Entity.Null
                ? default
                : entityManager.GetBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity);


        private EntityQuery _troopQuery;
        private Random _rand;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoldierSpawner>();

            _troopQuery = SystemAPI.QueryBuilder().WithAll<Troop, Alive, TroopEntity>().Build();

            s_troopId = 1;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnStartRunning(ref SystemState state)
        {
            s_spawnSoldierDataBufferEntity = SystemAPI.GetSingletonEntity<SoldierSpawner>();

            _rand = new Random();
            _rand.InitState();
        }

        public void OnStopRunning(ref SystemState state)
        {
            state.EntityManager.DestroyEntity(s_spawnSoldierDataBufferEntity);
            s_spawnSoldierDataBufferEntity = Entity.Null;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity))
            {
                return;
            }

            DynamicBuffer<SpawnSoldierData> spawnSoldierDataBuffer = state.EntityManager.GetBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity);
            if (spawnSoldierDataBuffer.Length == 0)
            {
                return;
            }

            SoldierSpawner soldierSpawner = state.EntityManager.GetComponentData<SoldierSpawner>(s_spawnSoldierDataBufferEntity);

            NativeList<SoldierForSpawn> spawnSoldierList = new(Allocator.TempJob);
            NativeList<TroopForSpawn> spawnTroopList = new(Allocator.TempJob);

            int currentTroopId = s_troopId;

            foreach (SpawnSoldierData spawnSoldierData in spawnSoldierDataBuffer)
            {
                for (int i = 0, count = spawnSoldierData.Count; i < count; ++i)
                {
                    _troopQuery.SetSharedComponentFilter(new Troop { Id = currentTroopId });

                    if (_troopQuery.CalculateEntityCount() == 0 &&
                        !spawnTroopList.AsValueEnumerable().Any(troop => troop.TroopId == currentTroopId))
                    {
                        spawnTroopList.Add(
                            new TroopForSpawn
                            {
                                TroopId = s_troopId,
                                TroopPosition = spawnSoldierData.Position.xz,
                                TroopRotation = spawnSoldierData.Rotation,
                                TeamColor = spawnSoldierData.TeamColor,
                                TroopHorizonSoldierCount = Setting.Instance.troopHorizonSoldierCount,
                            });

                        currentTroopId = s_troopId++;
                    }

                    spawnSoldierList.Add(
                        new SoldierForSpawn
                        {
                            TroopId = currentTroopId,
                            TeamColor = spawnSoldierData.TeamColor,
                            SoldierType = spawnSoldierData.SoldierType,

                            SoldierData = spawnSoldierData.SoldierType switch
                            {
                                SoldierType.Spear => soldierSpawner.SpearSoldierData,
                                SoldierType.Archer => soldierSpawner.ArcherSoldierData,
                                SoldierType.Shield => soldierSpawner.ShieldSoldierData,
                                SoldierType.Cavalry => soldierSpawner.CavalrySoldierData,
                                _ => soldierSpawner.SpearSoldierData
                            },

                            Position = new float3(spawnSoldierData.Position.x + _rand.NextFloat(-3, 3), spawnSoldierData.Position.y, spawnSoldierData.Position.z + _rand.NextFloat(-3, 3)),
                            Rotation = spawnSoldierData.Rotation,
                        });
                }
            }

            spawnSoldierDataBuffer.Clear();

            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            bool isNewTroopSpawned = spawnTroopList.Length > 0;
            if (isNewTroopSpawned)
            {
                EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
                dependency =
                    new SpawnTroopJob
                        {
                            EntityCommandBuffer = ecb.AsParallelWriter(),

                            TroopProtoType = soldierSpawner.TroopProtoType,
                            TroopForSpawns = spawnTroopList.AsReadOnly(),
                        }
                        .Schedule(spawnTroopList.Length, 64, dependency);
                ecbSystem.AddJobHandleForProducer(dependency);
            }

            bool isNewSoldierSpawned = spawnSoldierList.Length > 0;
            if (isNewSoldierSpawned)
            {
                EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
                dependency =
                    new SpawnSoldierJob
                        {
                            EntityCommandBuffer = ecb.AsParallelWriter(),
#if DO_NOT_USE_ENTITIES_GRAPHICS
                            SoldierProtoType = soldierSpawner.SoldierProtoType,
#endif
                            SoldierForSpawns = spawnSoldierList.AsReadOnly(),
                        }
                        .Schedule(spawnSoldierList.Length, 64, dependency);
                ecbSystem.AddJobHandleForProducer(dependency);
            }

            state.Dependency = JobHandle.CombineDependencies(spawnTroopList.Dispose(dependency), spawnSoldierList.Dispose(dependency));
        }
    }
}