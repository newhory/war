using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using ZLinq;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup), OrderFirst = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierInitializeSystem : ISystem
    {
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


            public void Execute(Entity entity) => EntityCommandBuffer.RemoveComponent<SpawnSoldierSystem.JustSpawnedSoldier>(entity.Index, entity);
        }

        [BurstCompile]
        private partial struct RemoveJustSpawnedTroopJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            public void Execute(Entity entity) => EntityCommandBuffer.RemoveComponent<SpawnSoldierSystem.JustSpawnedTroop>(entity.Index, entity);
        }


        private static int s_soldierId;


        private EntityQuery _troopQuery;
        private EntityQuery _spawnTroopQuery;
        private EntityQuery _spawnSoldierQuery;
        private EntityQuery _resetFormationUnitIndexQuery;


        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SoldierSpawner>();

            _troopQuery = SystemAPI.QueryBuilder().WithAll<Troop, TroopEntity>().Build();

            _spawnTroopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, TroopEntity, Team, SpawnSoldierSystem.JustSpawnedTroop>()
                    .Build();

            _spawnSoldierQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<NavMeshAgentData, Team, SpawnSoldierSystem.JustSpawnedSoldier>()
                    .WithAllRW<Soldier>()
                    .WithAllRW<SoldierAttachedTroop, FormationUnit>()
                    .WithAllRW<PhysicsCollider>()
                    .Build();

            _resetFormationUnitIndexQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<ResetFormationUnitIndex>()
                    .Build();

            s_soldierId = 0;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            NativeParallelHashMap<int, Entity> troopEntityMap = new(_troopQuery.CalculateEntityCount(), Allocator.TempJob);
            NativeArray<int> soldierIds = new(_spawnSoldierQuery.CalculateEntityCount(), Allocator.TempJob);

            NativeArray<FormationUnit> formationUnits = _spawnSoldierQuery.ToComponentDataArray<FormationUnit>(Allocator.TempJob);
            NativeHashSet<int> resetFormationIds = new(formationUnits.Length, Allocator.TempJob);
            foreach (int formationId in formationUnits.AsValueEnumerable().Select(formationUnit => formationUnit.FormationId))
            {
                resetFormationIds.Add(formationId);
            }

            dependency = new AddResetFormationUnitIndexJob { ResetFormationIds = resetFormationIds.AsReadOnly() }.Schedule(_resetFormationUnitIndexQuery, dependency);
            dependency = JobHandle.CombineDependencies(resetFormationIds.Dispose(dependency), formationUnits.Dispose(dependency));

            dependency = new CollectTroopJob { TroopEntityMap = troopEntityMap.AsParallelWriter() }.ScheduleParallel(_troopQuery, dependency);
            dependency = new FillSoldierIdJob { SoldierIds = soldierIds }.Schedule(dependency);
            dependency =
                new SetSoldierComponentJob
                    {
                        TroopEntityMap = troopEntityMap.AsReadOnly(),
                        SoldierIds = soldierIds.AsReadOnly(),

                        ArrowLayer = Setting.ArrowLayer,
                        RedTeamLayer = Setting.RedTeamLayer,
                        BlueTeamLayer = Setting.BlueTeamLayer,
                    }
                    .ScheduleParallel(_spawnSoldierQuery, dependency);
            dependency = JobHandle.CombineDependencies(troopEntityMap.Dispose(dependency), soldierIds.Dispose(dependency));

            if (!_spawnSoldierQuery.IsEmpty || !_spawnTroopQuery.IsEmpty)
            {
                EndInitializationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndInitializationEntityCommandBufferSystem>();

                if (!_spawnSoldierQuery.IsEmpty)
                {
                    EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
                    dependency = new RemoveJustSpawnedSoldierJob { EntityCommandBuffer = ecb.AsParallelWriter() }.Schedule(_spawnSoldierQuery, dependency);
                    ecbSystem.AddJobHandleForProducer(dependency);
#if UNITY_EDITOR
                    ecb = ecbSystem.CreateCommandBuffer();
                    dependency = new SetSpawnSoldierNameJob { EntityCommandBuffer = ecb.AsParallelWriter() }.ScheduleParallel(_spawnSoldierQuery, dependency);
                    ecbSystem.AddJobHandleForProducer(dependency);
#endif
                }

                if (!_spawnSoldierQuery.IsEmpty)
                {
                    EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
                    dependency = new RemoveJustSpawnedTroopJob { EntityCommandBuffer = ecb.AsParallelWriter() }.Schedule(_spawnTroopQuery, dependency);
                    ecbSystem.AddJobHandleForProducer(dependency);
#if UNITY_EDITOR
                    ecb = ecbSystem.CreateCommandBuffer();
                    dependency = new SetSpawnTroopNameJob { EntityCommandBuffer = ecb.AsParallelWriter() }.ScheduleParallel(_spawnTroopQuery, dependency);
                    ecbSystem.AddJobHandleForProducer(dependency);
#endif
                }
            }

            state.Dependency = dependency;
        }
    }
}