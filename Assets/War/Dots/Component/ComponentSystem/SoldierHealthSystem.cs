using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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


            public void Execute([EntityIndexInQuery] int entityIndex, Entity entity, ref SoldierAnimation soldierAnimation, in Health refHealth)
            {
                if (refHealth.Value > 0)
                {
                    return;
                }

                EntityCommandBuffer.SetComponentEnabled<Alive>(entityIndex, entity, false);
                EntityCommandBuffer.SetComponentEnabled<Movable>(entityIndex, entity, false);
                EntityCommandBuffer.SetComponentEnabled<Rotatable>(entityIndex, entity, false);
                
                EntityCommandBuffer.RemoveComponent<Damaged>(entityIndex, entity);
                EntityCommandBuffer.RemoveComponent<PhysicsCollider>(entityIndex, entity);

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
            using EntityCommandBuffer soldierEcb = new(Allocator.TempJob);
            new CheckSoldierHealthJob
                {
                    EntityCommandBuffer = soldierEcb.AsParallelWriter(),
                }
                .ScheduleParallel(_soldierHealthGroup, state.Dependency)
                .Complete();
            soldierEcb.Playback(state.EntityManager);
        }
    }
}