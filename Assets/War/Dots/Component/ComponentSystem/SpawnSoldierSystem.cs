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

        private struct JustSpawnedSoldier : IComponentData
        {
        }

        private struct JustSpawnedTroop : IComponentData
        {
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
                EntityCommandBuffer.AddComponent(index, soldierEntity, new SoldierAttachedTroop { TroopId = soldierForSpawn.TroopId });
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

                EntityCommandBuffer.AddSharedComponent(index, soldierEntity, new Formation { Id = soldierForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(index, soldierEntity, new FormationUnit { FormationId = soldierForSpawn.TroopId, Radius = soldierData.radiusInFormation });

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

                EntityCommandBuffer.SetComponentEnabled<SoldierStateMoveToTarget>(index, soldierEntity, false);
                EntityCommandBuffer.SetComponentEnabled<SoldierStateAttackTarget>(index, soldierEntity, false);

                EntityCommandBuffer.AddComponent(index, soldierEntity, new JustSpawnedSoldier());
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
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopEntity { Id = troopForSpawn.TroopId, Entity = troopEntity });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopTargetForAttack { TargetTroop = Entity.Null });
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopSelected());
                EntityCommandBuffer.SetComponentEnabled<TroopSelected>(index, troopEntity, false);
                EntityCommandBuffer.AddComponent(index, troopEntity, new TroopAABB { Min = float2.zero, Max = float2.zero, Padding = 0.2f });

                EntityCommandBuffer.AddBuffer<TroopSoldierEntity>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopSoldierPosition>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopHullPoint>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopSoldierIndexBuffer>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopLowerSoldierIndexBuffer>(index, troopEntity);
                EntityCommandBuffer.AddBuffer<TroopUpperSoldierIndexBuffer>(index, troopEntity);

            #endregion

            #region formation

                EntityCommandBuffer.AddSharedComponent(index, troopEntity, new Formation { Id = troopForSpawn.TroopId });
                EntityCommandBuffer.AddComponent(
                    index,
                    troopEntity,
                    new FormationEntity
                    {
                        Id = troopForSpawn.TroopId,
                        Entity = troopEntity,
                        HorizontalUnitCount = troopForSpawn.TroopHorizonSoldierCount,
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

            #endregion

            #region tag

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

        [BurstCompile]
        private partial struct CollectTroopJob : IJobEntity
        {
            public NativeParallelHashMap<int, Entity>.ParallelWriter TroopEntityMap;


            public void Execute(in TroopEntity troopEntity) => TroopEntityMap.TryAdd(troopEntity.Id, troopEntity.Entity);
        }

        private struct FillSoldierIdJob : IJob
        {
            public NativeArray<int> SoldierIds;


            public void Execute()
            {
                for (int i = 0, count = SoldierIds.Length; i < count; ++i)
                {
                    SoldierIds[i] = ++s_soldierId;
                }
            }
        }

        [BurstCompile]
        private partial struct SetSoldierComponentJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashMap<int, Entity>.ReadOnly TroopEntityMap;
            [ReadOnly] public NativeArray<int>.ReadOnly SoldierIds;

            [ReadOnly] public int ArrowLayer;
            [ReadOnly] public int RedTeamLayer;
            [ReadOnly] public int BlueTeamLayer;


            public void Execute([EntityIndexInQuery] int entityIndex, ref Soldier soldier, ref SoldierAttachedTroop soldierAttachedTroop, ref FormationUnit formationUnit, ref PhysicsCollider physicsCollider, in NavMeshAgentData navMeshAgentData, in Team team)
            {
                soldier.Id = SoldierIds[entityIndex];

                if (TroopEntityMap.TryGetValue(soldierAttachedTroop.TroopId, out Entity troopEntity))
                {
                    soldierAttachedTroop.TroopEntity = troopEntity;
                    formationUnit.FormationEntity = troopEntity;
                }

                if (physicsCollider.Value is { IsCreated: true, Value: { Type: ColliderType.Capsule } })
                {
                    unsafe
                    {
                        CapsuleCollider* capsuleCollider = (CapsuleCollider*)physicsCollider.ColliderPtr;
                        CapsuleGeometry geometry = capsuleCollider->Geometry;

                        geometry.Radius = navMeshAgentData.Radius * 0.9f;

                        BlobAssetReference<Collider> newCapsule =
                            CapsuleCollider.Create(
                                geometry,
                                new CollisionFilter
                                {
                                    BelongsTo = 1u << team.Color switch { TeamColor.Red => RedTeamLayer, TeamColor.Blue => BlueTeamLayer, _ => (int)TeamColor.None },
                                    CollidesWith = 1u << ArrowLayer,
                                });

                        physicsCollider.Value = newCapsule;
                    }
                }
            }
        }


#if UNITY_EDITOR
        private partial struct SetSpawnSoldierNameJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity entity, in Soldier soldier, in SoldierAttachedTroop soldierAttachedTroop, in Team team) => EntityCommandBuffer.SetName(entity.Index, entity, $"<[{team.Color}]Troop {soldierAttachedTroop.TroopId}>{soldier.Type}_{soldier.Id}");
        }

        private partial struct SetSpawnTroopNameJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity entity, in TroopEntity troopEntity, in Team team) => EntityCommandBuffer.SetName(entity.Index, entity, $"[{team.Color}]{nameof(Troop)} {troopEntity.Id}");
        }
#endif

        [BurstCompile]
        private partial struct RemoveJustSpawnedSoldierJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity entity) => EntityCommandBuffer.RemoveComponent<JustSpawnedSoldier>(entity.Index, entity);
        }

        [BurstCompile]
        private partial struct RemoveJustSpawnedTroopJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity entity) => EntityCommandBuffer.RemoveComponent<JustSpawnedTroop>(entity.Index, entity);
        }

        [BurstCompile]
        private partial struct AddResetFormationUnitIndexJob : IJobEntity
        {
            [ReadOnly] public NativeHashSet<int>.ReadOnly ResetFormationIds;


            public void Execute(DynamicBuffer<ResetFormationUnitIndex> resetFormationUnitIndexBuffer)
            {
                foreach (int resetFormationId in ResetFormationIds)
                {
                    resetFormationUnitIndexBuffer.Add(new ResetFormationUnitIndex { Formation = new Formation { Id = resetFormationId } });
                }
            }
        }


        private static Entity s_spawnSoldierDataBufferEntity;
        private static int s_troopId;
        private static int s_soldierId;


        public static void SpawnSoldier(EntityManager entityManager, SpawnSoldierData spawn) => GetSpawnSoldierDataBuffer(entityManager).Add(spawn);

        private static DynamicBuffer<SpawnSoldierData> GetSpawnSoldierDataBuffer(EntityManager entityManager) =>
            s_spawnSoldierDataBufferEntity == Entity.Null
                ? default
                : entityManager.GetBuffer<SpawnSoldierData>(s_spawnSoldierDataBufferEntity);


        private EntityQuery _troopQuery;
        private EntityQuery _spawnTroopQuery;
        private EntityQuery _spawnSoldierQuery;
        private EntityQuery _resetFormationUnitIndexQuery;

        private Random _rand;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoldierSpawner>();

            _troopQuery = SystemAPI.QueryBuilder().WithAll<Troop, TroopEntity>().Build();

            _spawnTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, Team, JustSpawnedTroop>()
                    .Build();

            _spawnSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<NavMeshAgentData, Team, JustSpawnedSoldier>()
                    .WithAllRW<Soldier>()
                    .WithAllRW<SoldierAttachedTroop, FormationUnit>()
                    .WithAllRW<PhysicsCollider>()
                    .Build();

            _resetFormationUnitIndexQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<ResetFormationUnitIndex>()
                    .Build();

            s_troopId = 1;
            s_soldierId = 0;
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

            NativeHashSet<int> resetFormationIds = new(2, Allocator.TempJob);

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

                    resetFormationIds.Add(currentTroopId);
                }
            }

            spawnSoldierDataBuffer.Clear();

            JobHandle dependency = state.Dependency;

            bool isNewTroopSpawned = spawnTroopList.Length > 0;
            if (isNewTroopSpawned)
            {
                using EntityCommandBuffer ecb = new(Allocator.TempJob);
                new SpawnTroopJob
                    {
                        TroopForSpawns = spawnTroopList.AsReadOnly(),
                        EntityCommandBuffer = ecb.AsParallelWriter(),
                    }
                    .Schedule(spawnTroopList.Length, 64, dependency)
                    .Complete();

                ecb.Playback(state.EntityManager);

                spawnTroopList.Dispose();
            }

            bool isNewSoldierSpawned = spawnSoldierList.Length > 0;
            if (isNewSoldierSpawned)
            {
                using EntityCommandBuffer ecb = new(Allocator.TempJob);

                new SpawnSoldierJob
                    {
                        SoldierProtoType = soldierSpawner.SoldierProtoType,
                        SoldierForSpawns = spawnSoldierList.AsReadOnly(),
                        EntityCommandBuffer = ecb.AsParallelWriter(),
                    }
                    .Schedule(spawnSoldierList.Length, 64, dependency)
                    .Complete();

                ecb.Playback(state.EntityManager);

                spawnSoldierList.Dispose();
            }

            new AddResetFormationUnitIndexJob { ResetFormationIds = resetFormationIds.AsReadOnly() }.Schedule(_resetFormationUnitIndexQuery, dependency).Complete();
            resetFormationIds.Dispose();

            NativeParallelHashMap<int, Entity> troopEntityMap = new(_troopQuery.CalculateEntityCount(), Allocator.TempJob);
            NativeArray<int> soldierIds = new(_spawnSoldierQuery.CalculateEntityCount(), Allocator.TempJob);

            dependency = new CollectTroopJob { TroopEntityMap = troopEntityMap.AsParallelWriter() }.ScheduleParallel(_troopQuery, dependency);
            new FillSoldierIdJob { SoldierIds = soldierIds }.Schedule(dependency).Complete();

            new SetSoldierComponentJob
                {
                    TroopEntityMap = troopEntityMap.AsReadOnly(),
                    SoldierIds = soldierIds.AsReadOnly(),

                    ArrowLayer = Setting.ArrowLayer,
                    RedTeamLayer = Setting.RedTeamLayer,
                    BlueTeamLayer = Setting.BlueTeamLayer,
                }
                .ScheduleParallel(_spawnSoldierQuery, dependency)
                .Complete();

            troopEntityMap.Dispose();
            soldierIds.Dispose();

            EntityCommandBuffer ecbRemoveJustSpawned;
#if UNITY_EDITOR
            ecbRemoveJustSpawned = new EntityCommandBuffer(Allocator.TempJob);
            new SetSpawnSoldierNameJob { EntityCommandBuffer = ecbRemoveJustSpawned.AsParallelWriter() }.ScheduleParallel(_spawnSoldierQuery, dependency).Complete();
            ecbRemoveJustSpawned.Playback(state.EntityManager);
            ecbRemoveJustSpawned.Dispose();

            ecbRemoveJustSpawned = new EntityCommandBuffer(Allocator.TempJob);
            new SetSpawnTroopNameJob { EntityCommandBuffer = ecbRemoveJustSpawned.AsParallelWriter() }.ScheduleParallel(_spawnTroopQuery, dependency).Complete();
            ecbRemoveJustSpawned.Playback(state.EntityManager);
            ecbRemoveJustSpawned.Dispose();
#endif
            ecbRemoveJustSpawned = new EntityCommandBuffer(Allocator.TempJob);
            new RemoveJustSpawnedSoldierJob { EntityCommandBuffer = ecbRemoveJustSpawned.AsParallelWriter() }.Schedule(_spawnSoldierQuery, dependency).Complete();
            ecbRemoveJustSpawned.Playback(state.EntityManager);
            ecbRemoveJustSpawned.Dispose();

            ecbRemoveJustSpawned = new EntityCommandBuffer(Allocator.TempJob);
            new RemoveJustSpawnedTroopJob { EntityCommandBuffer = ecbRemoveJustSpawned.AsParallelWriter() }.Schedule(_spawnTroopQuery, dependency).Complete();
            ecbRemoveJustSpawned.Playback(state.EntityManager);
            ecbRemoveJustSpawned.Dispose();
        }
    }
}