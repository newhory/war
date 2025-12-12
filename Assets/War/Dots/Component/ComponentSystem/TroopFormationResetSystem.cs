using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [UpdateAfter(typeof(TroopSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TroopFormationResetSystem : ISystem
    {
        [BurstCompile]
        private partial struct RepositionSoldiersJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


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
                for (int i = 0; i < troopSoldierCount; ++i)
                {
                    soldierPositions[i] = troopTransform.TransformPoint(soldierPositions[i] - center);
                }

                for (int i = 0; i < troopSoldierCount; ++i)
                {
                    TroopSoldier troopSoldier = troopSoldiers[i];

                    troopSoldier.PositionInFormation = soldierPositions[troopSoldier.IndexInFormation];

                    EntityCommandBuffer.SetComponentEnabled<SoldierUpdatePositionInFormation>(index, troopSoldier.Entity, true);

                    troopSoldiers[i] = troopSoldier;
                }

                soldierPositions.Dispose();
            }
        }


        private EntityQuery _troopQuery;


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
            EntityCommandBuffer ecb = new(Allocator.TempJob);
            new RepositionSoldiersJob { EntityCommandBuffer = ecb.AsParallelWriter() }.ScheduleParallel(_troopQuery, state.Dependency).Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}