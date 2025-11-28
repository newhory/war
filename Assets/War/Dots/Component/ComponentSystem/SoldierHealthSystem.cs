using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;


namespace War.Dots.Component.ComponentSystem
{
    [UpdateInGroup(typeof(Group.HealthSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct SoldierHealthSystem : ISystem
    {
        [BurstCompile]
        private partial struct CheckSoldierHealthJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter EntityCommandBuffer;


            private void Execute(Entity entity, ref SoldierAnimation soldierAnimation, in Health refHealth)
            {
                if (refHealth.Value > 0)
                {
                    return;
                }

                EntityCommandBuffer.SetComponentEnabled<Alive>(entity.Index, entity, false);
                EntityCommandBuffer.SetComponentEnabled<Movable>(entity.Index, entity, false);
                EntityCommandBuffer.SetComponentEnabled<Rotatable>(entity.Index, entity, false);

                EntityCommandBuffer.RemoveComponent<Damaged>(entity.Index, entity);
                EntityCommandBuffer.RemoveComponent<PhysicsCollider>(entity.Index, entity);

                soldierAnimation.Next = SoldierAnimation.State.Dead;
            }
        }


        private EntityQuery _soldierHealthGroup;


        public void OnCreate(ref SystemState state) =>
            _soldierHealthGroup =
                SystemAPI.QueryBuilder()
                    .WithAll<Alive, Soldier, Health>()
                    .WithAllRW<SoldierAnimation>()
                    .Build();

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;

            EndSimulationEntityCommandBufferSystem ecbSystem = state.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer soldierEcb = ecbSystem.CreateCommandBuffer();
            dependency = new CheckSoldierHealthJob { EntityCommandBuffer = soldierEcb.AsParallelWriter() }.ScheduleParallel(_soldierHealthGroup, dependency);
            ecbSystem.AddJobHandleForProducer(dependency);

            state.Dependency = dependency;
        }
    }
}