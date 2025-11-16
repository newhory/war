using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.SpawnSystemGroup))]
    public partial class SpawnSoldierSystem : SystemBase
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
        }

        [BurstCompile]
        private struct SpawnSoldierJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public Entity SoldierProtoType;
            [ReadOnly] public NativeArray<SoldierForSpawn>.ReadOnly SoldierForSpawns;


            public void Execute(int index)
            {
                SoldierForSpawn soldierForSpawn = SoldierForSpawns[index];
                SoldierData soldierData = soldierForSpawn.SoldierData;

                Entity soldierEntity = EntityCommandBuffer.Instantiate(index, SoldierProtoType);

            #region troop

                EntityCommandBuffer.AddSharedComponent(index, soldierEntity, new Troop { Id = soldierForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Team { Color = soldierForSpawn.TeamColor });

            #endregion

            #region soldier

                EntityCommandBuffer.AddComponent(index, soldierEntity, new Soldier { Type = soldierForSpawn.SoldierType });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierAttachedTroop());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierWeapon { Type = soldierData.weapon });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierAnimation { Next = SoldierAnimation.State.Default });

            #endregion

            #region AI

                EntityCommandBuffer.AddComponent(index, soldierEntity, new AI { CheckTargetInterval = soldierData.checkTargetInterval, LastCheckTargetTime = -1f });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new AISearchTarget());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new AICheckTargetValid());

            #endregion

            #region state

                EntityCommandBuffer.AddComponent(index, soldierEntity, new StateMoveInFormation());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new StateMoveToTarget());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new StateAttackTarget());

            #endregion

            #region formation

                EntityCommandBuffer.AddSharedComponent(index, soldierEntity, new Formation { Id = soldierForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new FormationUnit { Radius = soldierData.radiusInFormation });

            #endregion

            #region transform

                EntityCommandBuffer.AddComponent(index, soldierEntity, new LocalTransform { Position = soldierForSpawn.Position, Scale = 1f });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new NavMeshAgentData { Radius = soldierData.radius });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Velocity());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Acceleration { Value = float3.zero, Max = soldierForSpawn.SoldierData.moveAcceleration });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new MoveSpeed { Max = soldierData.moveSpeed, CurrentMax = soldierData.moveSpeed });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Forward { Value = math.forward(soldierForSpawn.Rotation) });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Destination());

            #endregion

            #region combat

                EntityCommandBuffer.AddComponent(index, soldierEntity, new SearchTargetRange { Value = soldierData.searchTargetRange });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new TargetForAttack { Target = Entity.Null });

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
                EntityCommandBuffer.AddBuffer<SpawnArrow>(index, soldierEntity);
                EntityCommandBuffer.AddBuffer<Damaged>(index, soldierEntity);

            #endregion

            #region tag

                EntityCommandBuffer.AddComponent(index, soldierEntity, new Alive());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Movable());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new Rotatable());
                EntityCommandBuffer.AddComponent(index, soldierEntity, new UseDefaultMaxSpeed());

            #endregion

                EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<StateAttackTarget>(index, soldierEntity, false);
            }
        }

        [BurstCompile]
        private struct SpawnTroopJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public NativeArray<TroopForSpawn>.ReadOnly TroopForSpawns;


            public void Execute(int index)
            {
                TroopForSpawn troopForSpawn = TroopForSpawns[index];

                Entity troopEntity = EntityCommandBuffer.CreateEntity(index);

            #region troop

                EntityCommandBuffer.AddSharedComponent(index, troopEntity, new Troop { Id = troopForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, troopEntity, new Team { Color = troopForSpawn.TeamColor });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopEntity { Entity = troopEntity });

            #endregion

            #region formation

                EntityCommandBuffer.AddSharedComponent(index, troopEntity, new Formation { Id = troopForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(
                    index,
                    troopEntity,
                    new FormationEntity
                    {
                        Entity = troopEntity,
                        HorizontalUnitCount = 10,
                    });

            #endregion

            #region transform

                EntityCommandBuffer.AddComponent(
                    index,
                    troopEntity,
                    LocalTransform.FromPositionRotationScale(
                        new float3(troopForSpawn.TroopPosition.x, 0f, troopForSpawn.TroopPosition.y),
                        troopForSpawn.TroopRotation,
                        1f));

                EntityCommandBuffer.AddComponent(index, troopEntity, new Velocity());
                EntityCommandBuffer.AddComponent(index, troopEntity, new Acceleration { Max = 1f });
                EntityCommandBuffer.AddComponent(index, troopEntity, new MoveSpeed { Max = 4f, CurrentMax = 4f });
                EntityCommandBuffer.AddComponent(index, troopEntity, new Forward { Value = math.forward(troopForSpawn.TroopRotation) });
                EntityCommandBuffer.AddComponent(index, troopEntity, new Destination { Position = new float3(troopForSpawn.TroopPosition.x, 0f, troopForSpawn.TroopPosition.y) });

            #endregion

            #region combat

                EntityCommandBuffer.AddComponent(index, troopEntity, new SearchTargetRange { Value = 20f });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TargetForAttack { Target = Entity.Null });

            #endregion

            #region tag

                EntityCommandBuffer.AddComponent(index, troopEntity, new Movable());
                EntityCommandBuffer.AddComponent(index, troopEntity, new Rotatable());

                EntityCommandBuffer.AddComponent(index, troopEntity, new AISearchTarget());
                EntityCommandBuffer.AddComponent(index, troopEntity, new AICheckTargetValid());

                EntityCommandBuffer.AddComponent(index, troopEntity, new StateMoveInFormation());
                EntityCommandBuffer.AddComponent(index, troopEntity, new StateMoveToTarget());

            #endregion

                EntityCommandBuffer.SetComponentEnabled<AISearchTarget>(index, troopEntity, false);
                EntityCommandBuffer.SetComponentEnabled<AICheckTargetValid>(index, troopEntity, false);
                EntityCommandBuffer.SetComponentEnabled<StateMoveToTarget>(index, troopEntity, false);
            }
        }


        private static Entity s_spawnSoldierDataBufferEntity;


        private static DynamicBuffer<SpawnSoldierData> GetSpawnSoldierDataBuffer(EntityManager entityManager) =>
            s_spawnSoldierDataBufferEntity == Entity.Null
                ? default
                : entityManager.GetBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity);

        public static void SpawnSoldier(EntityManager entityManager, SpawnSoldierData spawn) => GetSpawnSoldierDataBuffer(entityManager).Add(spawn);

        private static int s_soldierId;


        private EntityQuery _troopEntityQuery;

        private Random _rand;


        protected override void OnCreate()
        {
            RequireForUpdate<SoldierSpawner>();

            _troopEntityQuery = SystemAPI.QueryBuilder().WithAll<Troop, TroopEntity>().Build();
        }

        protected override void OnDestroy()
        {
            s_soldierId = 0;
        }

        protected override void OnStartRunning()
        {
            s_spawnSoldierDataBufferEntity = SystemAPI.GetSingletonEntity<SoldierSpawner>();

            _rand = new Random();
            _rand.InitState();
        }

        protected override void OnStopRunning()
        {
            EntityManager.DestroyEntity(s_spawnSoldierDataBufferEntity);
            s_spawnSoldierDataBufferEntity = Entity.Null;
        }

        protected override void OnUpdate()
        {
            if (!EntityManager.HasBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity))
            {
                return;
            }

            DynamicBuffer<SpawnSoldierData> spawnSoldierDataBuffer = EntityManager.GetBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity);
            if (spawnSoldierDataBuffer.Length == 0)
            {
                return;
            }

            SoldierSpawner soldierSpawner = EntityManager.GetComponentData<SoldierSpawner>(s_spawnSoldierDataBufferEntity);

            using NativeList<SoldierForSpawn> spawnSoldierList = new(Allocator.TempJob);
            using NativeList<TroopForSpawn> spawnTroopList = new(Allocator.TempJob);
            using NativeHashSet<int> resetFormationIds = new(2, Allocator.TempJob);

            DynamicBuffer<ResetFormationUnitIndex> resetFormationUnitIndexBuffer = FormationUnitIndexingSystem.GetResetFormationUnitIndexBuffer(EntityManager);

            foreach (SpawnSoldierData spawnSoldierData in spawnSoldierDataBuffer)
            {
                for (int i = 0, count = spawnSoldierData.Count; i < count; ++i)
                {
                    spawnSoldierList.Add(
                        new SoldierForSpawn
                        {
                            TroopId = spawnSoldierData.TroopId,
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

                    _troopEntityQuery.SetSharedComponentFilter(new Troop { Id = spawnSoldierData.TroopId });

                    if (_troopEntityQuery.CalculateEntityCount() == 0 &&
                        !spawnTroopList.AsValueEnumerable().Any(troop => troop.TroopId == spawnSoldierData.TroopId))
                    {
                        spawnTroopList.Add(
                            new TroopForSpawn
                            {
                                TroopId = spawnSoldierData.TroopId,
                                TroopPosition = spawnSoldierData.Position.xz,
                                TroopRotation = spawnSoldierData.Rotation,
                                TeamColor = spawnSoldierData.TeamColor,
                            });
                    }

                    if (resetFormationIds.Add(spawnSoldierData.TroopId))
                    {
                        resetFormationUnitIndexBuffer.Add(new ResetFormationUnitIndex { Formation = new Formation { Id = spawnSoldierData.TroopId } });
                    }
                }
            }

            spawnSoldierDataBuffer.Clear();

            bool isNewTroopSpawned = spawnTroopList.Length > 0;
            if (isNewTroopSpawned)
            {
                using EntityCommandBuffer troopEcb = new(Allocator.TempJob);
                new SpawnTroopJob
                    {
                        TroopForSpawns = spawnTroopList.AsReadOnly(),
                        EntityCommandBuffer = troopEcb.AsParallelWriter(),
                    }
                    .Schedule(spawnTroopList.Length, 64, Dependency)
                    .Complete();
                troopEcb.Playback(EntityManager);
            }

            bool isNewSoldierSpawned = spawnSoldierList.Length > 0;
            if (isNewSoldierSpawned)
            {
                using EntityCommandBuffer ecbSpawnSoldier = new(Allocator.TempJob);
                new SpawnSoldierJob
                    {
                        SoldierProtoType = soldierSpawner.SoldierProtoType,
                        SoldierForSpawns = spawnSoldierList.AsReadOnly(),
                        EntityCommandBuffer = ecbSpawnSoldier.AsParallelWriter(),
                    }
                    .Schedule(spawnSoldierList.Length, 64, Dependency)
                    .Complete();
                ecbSpawnSoldier.Playback(EntityManager);
            }

            EntityManager.GetAllUniqueSharedComponents(out NativeList<Troop> troops, Allocator.Temp);

            foreach (Troop troop in troops)
            {
                _troopEntityQuery.SetSharedComponentFilter(troop);
                if (_troopEntityQuery.CalculateEntityCount() != 1)
                {
                    continue;
                }

                TroopEntity troopEntity = _troopEntityQuery.GetSingleton<TroopEntity>();

                foreach (
                    (RefRW<Soldier> soldier, RefRW<SoldierAttachedTroop> soldierAttachedTroop, RefRW<PhysicsCollider> physicsCollider, RefRO<NavMeshAgentData> navMeshAgentData, RefRO<Team> team)
                    in
                    SystemAPI.Query<RefRW<Soldier>, RefRW<SoldierAttachedTroop>, RefRW<PhysicsCollider>, RefRO<NavMeshAgentData>, RefRO<Team>>()
                        .WithAll<Alive, Troop>()
                        .WithNone<UnityTransform, UnityAnimator>()
                        .WithSharedComponentFilter(troop))
                {
                    soldier.ValueRW.Id = ++s_soldierId;
                    soldierAttachedTroop.ValueRW.TroopEntity = troopEntity.Entity;

                    if (physicsCollider.ValueRO.Value is { IsCreated: true, Value: { Type: ColliderType.Capsule } })
                    {
                        unsafe
                        {
                            CapsuleCollider* capsuleCollider = (CapsuleCollider*)physicsCollider.ValueRO.ColliderPtr;
                            CapsuleGeometry geometry = capsuleCollider->Geometry;

                            geometry.Radius = navMeshAgentData.ValueRO.Radius * 0.9f;

                            BlobAssetReference<Collider> newCapsule =
                                CapsuleCollider.Create(
                                    geometry,
                                    new CollisionFilter
                                    {
                                        BelongsTo = (uint)(1 << Setting.GetMyTeamLayer(team.ValueRO.Color)),
                                        CollidesWith = (uint)(1 << Setting.ArrowLayer),
                                    });

                            physicsCollider.ValueRW.Value = newCapsule;
                        }
                    }
                }
            }

            troops.Dispose();
#if UNITY_EDITOR
            if (isNewSoldierSpawned)
            {
                using EntityCommandBuffer ecbSetSoldierName = new(Allocator.TempJob);
                foreach (
                    (RefRO<Soldier> refSoldier, RefRO<Team> refTeam, Entity entity)
                    in
                    SystemAPI.Query<RefRO<Soldier>, RefRO<Team>>()
                        .WithAll<Alive, Troop>()
                        .WithNone<UnityTransform, UnityAnimator>()
                        .WithEntityAccess())
                {
                    Troop troop = EntityManager.GetSharedComponent<Troop>(entity);

                    ecbSetSoldierName.SetName(entity, $"<[{refTeam.ValueRO.Color}]Troop {troop.Id}>{refSoldier.ValueRO.Type}_{refSoldier.ValueRO.Id}");
                }

                ecbSetSoldierName.Playback(EntityManager);
            }

            if (isNewTroopSpawned)
            {
                using EntityCommandBuffer ecbSetTroopName = new(Allocator.TempJob);
                foreach (
                    (RefRO<Team> refTeam, Entity entity)
                    in
                    SystemAPI.Query<RefRO<Team>>()
                        .WithAll<Troop, TroopEntity>()
                        .WithEntityAccess())
                {
                    Troop troop = EntityManager.GetSharedComponent<Troop>(entity);

                    ecbSetTroopName.SetName(entity, $"[{refTeam.ValueRO.Color}]{nameof(Troop)} {troop.Id}");
                }

                ecbSetTroopName.Playback(EntityManager);
            }
#endif
        }
    }
}