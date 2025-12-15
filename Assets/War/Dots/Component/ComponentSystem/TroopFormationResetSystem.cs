using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    using Navigation;


    [UpdateInGroup(typeof(Group.TroopInitializeSystemGroup))]
    [UpdateAfter(typeof(TroopSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopFormationResetSystem : ISystem
    {
        [BurstCompile]
        private partial struct RepositionSoldiersJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;

            [ReadOnly] public BlobAssetReference<FlowFieldBlobRoot> FlowFieldFlowBlobAssetReference;
            [ReadOnly] public int2 NavMeshGridSize;
            [ReadOnly] public float NavMeshCellSize;
            [ReadOnly] public float3 NavMeshMinWorldPosition;
            [ReadOnly] public NativeArray<byte>.ReadOnly NavMeshMask;


            private void Execute(
                [EntityIndexInQuery] int index, Entity troopEntity,
                DynamicBuffer<TroopSoldier> troopSoldiers,
                ref LocalTransform localTransform,
                in TroopFormationReset troopFormationReset, in TroopEntity comTroopEntity)
            {
                localTransform.Position = troopFormationReset.TroopPosition;

                EntityCommandBuffer.SetComponentEnabled<TroopFormationReset>(index, troopEntity, false);

                int troopSoldierCount = troopSoldiers.Length;
                if (troopSoldierCount == 0)
                {
                    return;
                }

                float3 troopPosition = localTransform.Position;
                troopPosition.y = 0f;

                int flowFieldId = -1;
                ref BlobArray<FlowFieldTarget> flowFieldTarget = ref FlowFieldFlowBlobAssetReference.Value.FlowFieldTargets;
                for (int i = 0, flowFieldTargetCount = flowFieldTarget.Length; i < flowFieldTargetCount; ++i)
                {
                    ref FlowFieldTarget target = ref flowFieldTarget[i];
                    if (target.AreaBounds.Contains(troopPosition))
                    {
                        flowFieldId = target.FlowId;
                        break;
                    }
                }

                if (flowFieldId < 0)
                {
                    return;
                }

                int formationWidth = comTroopEntity.HorizontalUnitCount;
                int formationHeight = (int)math.ceil(troopSoldierCount / (float)formationWidth);
                float3 sumLocalPosition = float3.zero;

                NativeArray<float3> soldierPositions = new(troopSoldierCount, Allocator.Temp);

                for (int y = 0; y < formationHeight; ++y)
                {
                    for (int x = 0; x < formationWidth; ++x)
                    {
                        int formationIndex = y * formationWidth + x;
                        if (formationIndex >= troopSoldierCount)
                        {
                            break;
                        }

                        TroopSoldier troopSoldier = troopSoldiers[formationIndex];

                        float3 localPos = new(x * troopSoldier.Radius * 2, 0f, y * troopSoldier.Radius * 2);

                        soldierPositions[formationIndex] = localPos;
                        sumLocalPosition += localPos;
                    }
                }

                float3 center = sumLocalPosition / troopSoldierCount;
                LocalTransform troopTransform = localTransform;
                float maxDistance = float.MinValue;
                float3 maxOffset = float3.zero;
                for (int i = 0; i < troopSoldierCount; ++i)
                {
                    float3 positionInFormation = troopTransform.TransformPoint(soldierPositions[i] - center);

                    soldierPositions[i] = positionInFormation;

                    if (!FlowFieldQuery.IsWalkable(positionInFormation, NavMeshMask, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition))
                    {
                        if (FlowFieldQuery.TryFindNearestWalkableWorldPosition(positionInFormation, NavMeshMask, NavMeshGridSize, NavMeshCellSize, NavMeshMinWorldPosition, out float3 walkablePosition))
                        {
                            float distance = math.distance(positionInFormation, walkablePosition);
                            if (distance > maxDistance)
                            {
                                maxDistance = distance;
                                maxOffset = walkablePosition - positionInFormation;
                            }
                        }
                    }
                }

                if (maxDistance > float.MinValue)
                {
                    float3 offsetDir = math.normalizesafe(maxOffset);
                    maxOffset = offsetDir * (maxDistance + NavMeshCellSize);

                    for (int i = 0; i < troopSoldierCount; ++i)
                    {
                        soldierPositions[i] += maxOffset;
                    }
                }

                for (int i = 0; i < troopSoldierCount; ++i)
                {
                    TroopSoldier troopSoldier = troopSoldiers[i];

                    troopSoldier.FlowFieldId = flowFieldId;
                    troopSoldier.PositionInFormation = soldierPositions[troopSoldier.IndexInFormation];

                    EntityCommandBuffer.SetComponentEnabled<SoldierUpdatePositionInFormation>(index, troopSoldier.Entity, true);

                    troopSoldiers[i] = troopSoldier;
                }

                soldierPositions.Dispose();
            }
        }


        private EntityQuery _troopQuery;
        private SystemHandle _entityCommandBufferSystemHandle;


        [BurstCompile]
        public void OnCreate(ref SystemState state) =>
            _troopQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Troop, Alive, TroopEntity, LocalTransform>()
                    .WithAll<TroopSoldier>()
                    .WithAll<TroopFormationReset>()
                    .Build();

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            BeginInitializationEntityCommandBufferSystem ecbSystem = state.World.GetExistingSystemManaged<BeginInitializationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();
            dependency =
                new RepositionSoldiersJob
                    {
                        EntityCommandBuffer = ecb.AsParallelWriter(),

                        FlowFieldFlowBlobAssetReference = FlowFieldProvider.FlowFieldFlowBlobAssetReference,
                        NavMeshGridSize = FlowFieldProvider.GridSize,
                        NavMeshCellSize = FlowFieldProvider.CellSize,
                        NavMeshMinWorldPosition = FlowFieldProvider.MinWorldPositionInGrid,
                        NavMeshMask = FlowFieldProvider.NavMeshMask
                    }
                    .ScheduleParallel(_troopQuery, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}