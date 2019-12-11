using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ER3D_TrafficSystem : JobComponentSystem
{
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
    [RequireComponentTag(typeof(ERAutoTag))]
    protected struct AutoNavigationJob : IJobForEach_BCCCC<
        AutoLanePoints,
        AutoDetails,
        AutoPosition,
        Translation,
        Rotation>
    {
        [ReadOnly]
        public ComponentDataFromEntity<RoadDetails> RoadDetailsFromEntity;

        [ReadOnly]
        public ComponentDataFromEntity<ConnectionDetails> ConnectionDetailsFromEntity;

        [ReadOnly]
        public BufferFromEntity<LanePoints> LanePointsFromEntity;

        [ReadOnly]
        public NativeArray<Entity> roadEntities;

        [ReadOnly]
        public NativeArray<Entity> connectionEntities;

        public float deltaTime;
        public float reachedPositionDistance;
        public uint randomSeed;

        public void Execute(
            DynamicBuffer<AutoLanePoints> autoLanePoints, 
            [ReadOnly] ref AutoDetails autoDetails, 
            ref AutoPosition autoPosition,
            ref Translation autoTranslation, 
            ref Rotation autoRotation)
        {
            if (autoLanePoints.Length == 0)
            {
                /* This is a basic check, 
                 * there seems to be a few instances 
                 * where the auto does not have points, 
                 */
                Debug.Log("no lanes");
                return; 
            }

            var distance = math.distance(autoPosition.Destination, autoTranslation.Value);
            int connectionIdentityEnd = 0;

            if (distance < reachedPositionDistance)
            {
                autoPosition.CurrentPositionIndex += 1;

                //If the Auto's current position is at the end of it's list of 
                //points, get the next list of points from the road or connection.
                if (autoPosition.CurrentPositionIndex >= autoLanePoints.Length)
                {
                    autoPosition.CurrentPositionIndex = 0;
                    autoLanePoints.Clear();

                    int laneIndex = 0;
                    int roadIdentity = 0;


                    for (int i = 0; i < roadEntities.Length; i++)
                    {
                        RoadDetails roadDetails = RoadDetailsFromEntity[roadEntities[i]];

                        if ((roadDetails.RoadIdentity == autoPosition.RoadIdentity) &&
                            (roadDetails.LaneIndex == autoPosition.LaneIndex) &&
                            (roadDetails.ConnectionIdentityStart == autoPosition.ConnectionIdentity)
                            )
                        {
                            laneIndex = roadDetails.LaneIndex;
                            roadIdentity = roadDetails.RoadIdentity;
                            connectionIdentityEnd = roadDetails.ConnectionIdentityEnd;

                            var roadLanePointsBuffer = LanePointsFromEntity[roadEntities[i]];
                            var roadLanePoints = roadLanePointsBuffer.ToNativeArray(Allocator.Temp);
                            var lanePointAuto = new AutoLanePoints();

                            for (int x = 0; x < roadLanePoints.Length; x++)
                            {
                                lanePointAuto.value = roadLanePoints[x].value;
                                autoLanePoints.Add(lanePointAuto);
                            }
                            break;
                        }
                    }


                    //Get the Connection's Lane's points
                    //First find all options that have the same Idenity and Index.  
                    //Then we can randomly select one of the routes through the connection
                    NativeList<Entity> availableConnections = new NativeList<Entity>(Allocator.Temp);

                    for (int i = 0; i < connectionEntities.Length; i++)
                    {
                        var connectionDetails = ConnectionDetailsFromEntity[connectionEntities[i]];

                        if ((connectionDetails.ConnectionIdentity == connectionIdentityEnd) &&
                            (connectionDetails.RoadIdentityStart == roadIdentity) && 
                            (connectionDetails.LaneIndexStart == laneIndex)
                            ) 
                        {
                            availableConnections.Add(connectionEntities[i]);
                        }
                    }
                    
                    //uint seed = 88;
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
                    autoPosition.LaneIndex = connectionDetailsNew.LaneIndexEnd;
                    autoPosition.RoadIdentity = connectionDetailsNew.RoadIdentityEnd;
                    autoPosition.ConnectionIdentity = connectionDetailsNew.ConnectionIdentity;

                    availableConnections.Dispose();
                }

                autoPosition.Destination = autoLanePoints[autoPosition.CurrentPositionIndex].value;
                autoPosition.Destination.y += autoDetails.yOffset;
            }

            float3 lookVector = autoPosition.Destination - autoTranslation.Value;

            if (!lookVector.Equals(float3.zero))
            {
                Quaternion rotationLookAt = Quaternion.LookRotation(lookVector);
                autoRotation.Value = rotationLookAt;
            }
            
            float3 smoothedPosition = math.lerp(autoTranslation.Value, autoPosition.Destination, autoDetails.speed * deltaTime);
            autoTranslation.Value = smoothedPosition;
        }
    }

    private const float REACHED_POSITION_DISTANCE = 1.0f;
    private NativeArray<Entity> roadEntities;
    private NativeArray<Entity> connectionEntities;
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        uint randomSeed = (uint)System.DateTime.Now.Millisecond;

        if (randomSeed == 0)
        {
            randomSeed = 88;
        }

        AutoNavigationJob autoNaviationJob = new AutoNavigationJob
        {
            RoadDetailsFromEntity = GetComponentDataFromEntity<RoadDetails>(true),
            ConnectionDetailsFromEntity = GetComponentDataFromEntity<ConnectionDetails>(true),
            LanePointsFromEntity = GetBufferFromEntity<LanePoints>(true),
            roadEntities = roadEntities,
            connectionEntities = connectionEntities,
            deltaTime = Time.deltaTime,
            randomSeed = randomSeed,
            reachedPositionDistance = REACHED_POSITION_DISTANCE
        };

        JobHandle jobHandle = autoNaviationJob.Schedule(this, inputDeps);

        //JobHandle jobHandle = autoNaviationJob.Run(this, inputDeps);  //USED FOR DEBUGGING

        return jobHandle;
    }
}
