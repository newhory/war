using Unity.Entities;
using Unity.Mathematics;


namespace War.Dots.Component.ComponentSystem
{
    public struct ResetFormationUnitIndex : IBufferElementData
    {
        public Formation Formation;
    }

    [UpdateInGroup(typeof(Group.JustSpawnedInitializeSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct FormationUnitIndexingSystem : ISystem
    {
        private static Entity s_resetFormationUnitIndexEntity;


        public static DynamicBuffer<ResetFormationUnitIndex> GetResetFormationUnitIndexBuffer(EntityManager entityManager) =>
            entityManager.HasBuffer<ResetFormationUnitIndex>(s_resetFormationUnitIndexEntity)
                ? entityManager.GetBuffer<ResetFormationUnitIndex>(s_resetFormationUnitIndexEntity)
                : entityManager.AddBuffer<ResetFormationUnitIndex>(s_resetFormationUnitIndexEntity);


        private EntityQuery _formationQuery;


        public void OnCreate(ref SystemState state)
        {
            s_resetFormationUnitIndexEntity = state.EntityManager.CreateSingletonBuffer<ResetFormationUnitIndex>(nameof(ResetFormationUnitIndex));

            _formationQuery =
                SystemAPI.QueryBuilder()
                    .WithAll<Formation, FormationEntity>()
                    .Build();
        }

        public void OnDestroy(ref SystemState state) => state.EntityManager.DestroyEntity(s_resetFormationUnitIndexEntity);

        public void OnUpdate(ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<ResetFormationUnitIndex>(s_resetFormationUnitIndexEntity))
            {
                return;
            }

            DynamicBuffer<ResetFormationUnitIndex> resetRequests = state.EntityManager.GetBuffer<ResetFormationUnitIndex>(s_resetFormationUnitIndexEntity);
            if (resetRequests.Length == 0)
            {
                return;
            }

            foreach (ResetFormationUnitIndex resetRequest in resetRequests)
            {
                Formation requestedFormation = resetRequest.Formation;

                _formationQuery.SetSharedComponentFilter(requestedFormation);

                if (_formationQuery.IsEmpty)
                {
                    continue;
                }

                FormationEntity formationEntity = _formationQuery.GetSingleton<FormationEntity>();

                int formationHorizontalCount = formationEntity.HorizontalUnitCount;

                int formationUnitCount = 0;
                float3 sumPosition = float3.zero;
                float3 min = new(float.MaxValue, 0f, float.MaxValue);
                float3 max = new(float.MinValue, 0f, float.MinValue);

                float3 currentLocalPosition = float3.zero;

                foreach (
                    RefRW<FormationUnit> formationUnit
                    in
                    SystemAPI.Query<RefRW<FormationUnit>>()
                        .WithSharedComponentFilter(requestedFormation)
                        .WithAll<Alive>()
                        .WithAll<PooledGameObject>())
                {
                    int indexInFormation = formationUnitCount++;
                    float radius = formationUnit.ValueRO.Radius;

                    float3 localPosition = currentLocalPosition;
                    localPosition.x += radius;
                    localPosition.z -= radius;

                    formationUnit.ValueRW.LocalPositionInFormation = localPosition;

                    sumPosition += localPosition;

                    if (min.x > localPosition.x)
                    {
                        min.x = localPosition.x;
                    }

                    if (min.z > localPosition.z)
                    {
                        min.z = localPosition.z;
                    }

                    if (max.x < localPosition.x)
                    {
                        max.x = localPosition.x;
                    }

                    if (max.z < localPosition.z)
                    {
                        max.z = localPosition.z;
                    }

                    if (indexInFormation % formationHorizontalCount == formationHorizontalCount - 1)
                    {
                        currentLocalPosition.x = 0f;
                        currentLocalPosition.z -= radius * 2f;
                    }
                    else
                    {
                        currentLocalPosition.x += radius * 2f;
                    }
                }

                foreach (
                    RefRW<FormationUnit> formationUnit
                    in
                    SystemAPI.Query<RefRW<FormationUnit>>()
                        .WithSharedComponentFilter(requestedFormation)
                        .WithAll<Alive>()
                        .WithNone<PooledGameObject>())
                {
                    int indexInFormation = formationUnitCount++;
                    float radius = formationUnit.ValueRO.Radius;

                    float3 localPosition = currentLocalPosition;
                    localPosition.x += radius;
                    localPosition.z -= radius;

                    formationUnit.ValueRW.LocalPositionInFormation = localPosition;

                    sumPosition += localPosition;

                    if (min.x > localPosition.x)
                    {
                        min.x = localPosition.x;
                    }

                    if (min.z > localPosition.z)
                    {
                        min.z = localPosition.z;
                    }

                    if (max.x < localPosition.x)
                    {
                        max.x = localPosition.x;
                    }

                    if (max.z < localPosition.z)
                    {
                        max.z = localPosition.z;
                    }

                    if (indexInFormation % formationHorizontalCount == formationHorizontalCount - 1)
                    {
                        currentLocalPosition.x = 0f;
                        currentLocalPosition.z -= radius * 2f;
                    }
                    else
                    {
                        currentLocalPosition.x += radius * 2f;
                    }
                }

                if (formationUnitCount > 0)
                {
                    float3 center = sumPosition / formationUnitCount;

                    foreach (
                        RefRW<FormationUnit> formationUnit
                        in
                        SystemAPI.Query<RefRW<FormationUnit>>()
                            .WithSharedComponentFilter(requestedFormation))
                    {
                        formationUnit.ValueRW.LocalPositionInFormation -= center;
                    }
                }
            }

            resetRequests.Clear();
        }
    }
}