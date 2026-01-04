using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;


namespace War.VAT.Systems
{
    [UpdateInGroup(typeof(Group.VertexAnimationInitializeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct VertexAnimationInitializeSystem : ISystem
    {
        private EntityQuery vatMeshQuery;


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            vatMeshQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<VatMeshData, InitializeVertexAnimation>()
                    .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (vatMeshQuery.IsEmpty)
            {
                return;
            }

            VatMeshData[] vatMeshDataArray = vatMeshQuery.ToComponentArray<VatMeshData>();
            NativeArray<Entity> entities = vatMeshQuery.ToEntityArray(Allocator.Temp);

            EntitiesGraphicsSystem entitiesGraphicsSystem = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            EndInitializationEntityCommandBufferSystem ecbSystem = state.World.GetExistingSystemManaged<EndInitializationEntityCommandBufferSystem>();

            EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

            for (int i = 0, count = entities.Length; i < count; ++i)
            {
                Entity entity = entities[i];
                VatMeshData vatMeshData = vatMeshDataArray[i];

                RenderMeshUtility.AddComponents(
                    entity,
                    state.EntityManager,
                    new RenderMeshDescription(
                        shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.On,
                        receiveShadows: false),
                    new MaterialMeshInfo(entitiesGraphicsSystem.RegisterMaterial(vatMeshData.material), entitiesGraphicsSystem.RegisterMesh(vatMeshData.mesh)));

                ecb.RemoveComponent<InitializeVertexAnimation>(entity);
                ecb.RemoveComponent<VatMeshData>(entity);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}