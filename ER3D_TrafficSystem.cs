using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ER3D_TrafficSystem : SystemBase 
{
    private NativeArray<Entity> roadEntities;
    private NativeArray<Entity> connectionEntities;
    
    protected override void OnStartRunning()
    {
        EntityQuery allRoadsQuery = GetEntityQuery(
            typeof(ERRoadTag),
            ComponentType.ReadOnly<RoadDetails>(),
            ComponentType.ReadOnly<LanePoints>());


        roadEntities = allRoadsQuery.ToEntityArray(Allocator.Persistent);

        EntityQuery allConnectionsQuery = GetEntityQuery(
            typeof(ERConnectionTag),
            ComponentType.ReadOnly<ConnectionDetails>(),
            ComponentType.ReadOnly<LanePoints>());

        connectionEntities = allConnectionsQuery.ToEntityArray(Allocator.Persistent);
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        System.Random random = new System.Random();
        uint randomSeed = (uint)random.Next(88, 1000000);
        float dt = Time.DeltaTime;
        float reachedPositionDistance = 2.5f;
        NativeArray<Entity> roads = roadEntities;
        NativeArray<Entity> connections = connectionEntities;
        ComponentDataFromEntity<RoadDetails> RoadDetailsFromEntity = GetComponentDataFromEntity<RoadDetails>(true);
        ComponentDataFromEntity<ConnectionDetails> ConnectionDetailsFromEntity = GetComponentDataFromEntity<ConnectionDetails>(true);
        BufferFromEntity<LanePoints> LanePointsFromEntity = GetBufferFromEntity<LanePoints>(true);


        Entities.WithAll<ERAutoTag>()
            .ForEach((
            DynamicBuffer<AutoLanePoints> autoLanePoints,
            ref AutoDetails autoDetails,
            ref AutoPosition autoPosition,
            ref Translation translation,
            ref Rotation rotation) =>
            {
                var distance = math.distance(autoPosition.Destination, translation.Value);

                if (distance <= reachedPositionDistance)
                {
                    autoPosition.CurrentPositionIndex += 1;

                    if (autoPosition.CurrentPositionIndex >= autoLanePoints.Length)
                    {
                        autoPosition.CurrentPositionIndex = 0;
                        autoLanePoints.Clear();

                        int laneIndex = 0;
                        int roadIdentity = 0;
                        int connectionIdentityEnd = 0;

                        for (int i = 0; i < roads.Length; i++)
                        {
                            RoadDetails roadDetails = RoadDetailsFromEntity[roads[i]];

                            if ((roadDetails.RoadIdentity == autoPosition.RoadIdentity) &&
                            (roadDetails.LaneIndex == autoPosition.LaneIndex) &&
                            (roadDetails.ConnectionIdentityStart == autoPosition.ConnectionIdentity) &&
                            (roadDetails.ConnectionIndexStart == autoPosition.ConnectionIndex)
                            )
                            {
                                laneIndex = roadDetails.LaneIndex;
                                roadIdentity = roadDetails.RoadIdentity;
                                connectionIdentityEnd = roadDetails.ConnectionIdentityEnd;

                                var roadLanePointsBuffer = LanePointsFromEntity[roads[i]];
                                var roadLanePoints = roadLanePointsBuffer.ToNativeArray(Allocator.Temp);
                                var lanePointAuto = new AutoLanePoints();
                                for (int pointIndex = 0; pointIndex < roadLanePoints.Length; pointIndex++)
                                {
                                    lanePointAuto.value = roadLanePoints[pointIndex].value;
                                    autoLanePoints.Add(lanePointAuto);
                                }
                                break;
                            }
                        }

                        //Get the Connection's Lane's points
                        //First find all options that have the same Idenity and Index.  
                        //Then we can randomly select one of the routes through the connection
                        NativeList<Entity> availableConnections = new NativeList<Entity>(Allocator.Temp);

                        for (int i = 0; i < connections.Length; i++)
                        {
                            var connectionDetails = ConnectionDetailsFromEntity[connections[i]];

                            if ((connectionDetails.ConnectionIdentity == connectionIdentityEnd) &&
                                (connectionDetails.LaneIndexStart == laneIndex) &&
                                (connectionDetails.RoadIdentityStart == roadIdentity) &&
                                (connectionDetails.ConnectionIndexStart == autoPosition.ConnectionIndex))
                            {
                                availableConnections.Add(connections[i]);
                            }
                        }

                        Unity.Mathematics.Random mathRandom = new Unity.Mathematics.Random(randomSeed);
                        int randomValue = mathRandom.NextInt(0, availableConnections.Length);
                        var connectionDetailsNew = ConnectionDetailsFromEntity[availableConnections[randomValue]];
                        var connectionLanePointsBuffer = LanePointsFromEntity[availableConnections[randomValue]];
                        var connectionLanePoints = connectionLanePointsBuffer.ToNativeArray(Allocator.Temp);
                        var lanePoint = new AutoLanePoints();

                        for (int x = 0; x < connectionLanePoints.Length; x++)
                        {
                            lanePoint.value = connectionLanePoints[x].value;
                            autoLanePoints.Add(lanePoint);
                        }

                        //Reset the Auto's variables for the next Road/Connection selection
                        autoPosition.LaneIndex = connectionDetailsNew.LaneIndexEnd;
                        autoPosition.RoadIdentity = connectionDetailsNew.RoadIdentityEnd;
                        autoPosition.ConnectionIdentity = connectionDetailsNew.ConnectionIdentity;
                        autoPosition.ConnectionIndex = connectionDetailsNew.ConnectionIndexEnd;

                        availableConnections.Dispose();
                    }

                    autoPosition.Destination = autoLanePoints[autoPosition.CurrentPositionIndex].value;
                    autoPosition.Destination.y += autoDetails.yOffset;
                }


                float3 lookVector = autoPosition.Destination - translation.Value;
                if (!lookVector.Equals(float3.zero))
                {
                    Quaternion rotationLookAt = Quaternion.LookRotation(lookVector);
                    rotation.Value = rotationLookAt;
                }

                float3 smoothedPosition = math.lerp(translation.Value, autoPosition.Destination, autoDetails.speed * dt);
                translation.Value = smoothedPosition;

            }).Schedule();
    }
}
