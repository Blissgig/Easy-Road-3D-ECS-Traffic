using UnityEngine;
using EasyRoads3Dv3;
using Unity.Entities;
using System.Collections.Generic;
using Unity.Transforms;
using Unity.Mathematics;
using System.Linq;

public class ER3D_Traffic : MonoBehaviour
{
    [Header("Autos")]
    [SerializeField]
    private List<GameObject> autos = new List<GameObject>();

    [Header("Settings")]
    [SerializeField]
    private float speedMinimum = 10;

    [SerializeField]
    private float speedMaximum = 40;

    [SerializeField]
    private int vehicleLength = 3;

    [SerializeField]
    private int percentageLanesPopulated = 100;


    private EntityManager entityManager;
    private ConnectedTo connectedTo;
    private List<Entity> autoEntities = new List<Entity>();


    private void Start()
    {
        entityManager = World.Active.EntityManager;

        if (percentageLanesPopulated > 100)
        {
            //Percentage can't be over 100, duh.
            percentageLanesPopulated = 100;  
        }

        AddIndentityMono();

        CreateAutoEntities();

        CreateRoadEntities();

        //TODO: remove mono identity after the road/connection entities have been created.
    }

    private void CreateAutoEntities()
    {
        /* Because of allowing the input of GameObjects and the 
         * time cost to converting them, this function is to create 
         * instances of the Entities, and those are used in the 
         * random selection in CreateAutoEntity.   
         * Saves TONS of time, like MINUTES worth.
         * eg: 3000 instances took 2 minutes, vs less than half a second
         */

        foreach (GameObject auto in autos)
        {
            Entity prefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(auto, World.Active);
            var entity = entityManager.Instantiate(prefab);

            entityManager.AddComponent(entity, typeof(ERAutoTag));
            entityManager.AddComponent(entity, typeof(AutoPosition));
            entityManager.AddComponent(entity, typeof(AutoDetails));
            entityManager.AddComponent(entity, typeof(AutoLanePoints));

            autoEntities.Add(entity);
        }
    }

    private void CreateRoadEntities()
    {
        var roadEntityArchetype = entityManager.CreateArchetype(
            typeof(ERRoadTag),
            typeof(RoadDetails),
            typeof(LanePoints)
            );

        var connectionEntityArchetype = entityManager.CreateArchetype(
            typeof(ERConnectionTag),
            typeof(ConnectionDetails),
            typeof(LanePoints)
        );

        int nextRoadIdentity = 0;
        int nextLaneIndex = 0;
        int connectionIndex;
        int connectionIdentityEnd = 0;
        int connectionIdentityStart = 0;
        ERConnection erConnectionEnd;
        ERConnection erConnectionStart;
        ERRoadNetwork roadNetwork = new ERRoadNetwork();
        ERRoad[] roads = roadNetwork.GetRoads();
        List<Vector3> allLanePoints = new List<Vector3>();
        List<Vector3> connectionLanePoints = new List<Vector3>();
        ConnectedTo connectedTo;


        //Make entities for each of these objects
        //Roads first.  They need a collection of Lanes and each lane has an input and output connection identity.
        //This collection of Road Identity & Lane Identity, or Connection Identity & Lane Identity
        //will be used in the JOB to get the next load of data
        foreach (ERRoad road in roads)
        {
            var roadLevelData = road.gameObject.GetComponent<ERRoadLevelDataHolder>().levelData;
            int roadIdentity = road.gameObject.GetComponent<ERRoadConnectionIdentity>().value;
            int lanes = road.GetLaneCount();
            

            for (int lane = 0; lane < lanes; lane++)
            {
                ERLaneData erLaneData = road.GetLaneData(lane);
                Vector3[] roadLanePoints = erLaneData.points;

                if (erLaneData.direction == ERLaneDirection.Right)
                {
                    erConnectionEnd = road.GetConnectionAtEnd(out connectionIndex);
                    erConnectionStart = road.GetConnectionAtStart(out connectionIdentityStart);
                }
                else
                {
                    erConnectionEnd = road.GetConnectionAtStart(out connectionIndex);
                    erConnectionStart = road.GetConnectionAtEnd(out connectionIdentityStart);                    
                }

                connectionIdentityStart = RoadConnectionIdentity(erConnectionStart.gameObject);
                connectionIdentityEnd = RoadConnectionIdentity(erConnectionEnd.gameObject);

                //Create the Road/Lane entity
                Entity roadEntity = entityManager.CreateEntity(roadEntityArchetype);
                entityManager.SetComponentData(
                    roadEntity,
                    new RoadDetails
                    {
                        RoadIdentity = roadIdentity,
                        LaneIndex = erLaneData.laneIndex,
                        ConnectionIdentityEnd = connectionIdentityEnd,
                        ConnectionIdentityStart = connectionIdentityStart
                    });

                //Get and store the road's lane points
                var roadLanePointsBuffer = entityManager.GetBuffer<LanePoints>(roadEntity);
                var roadLanePoint = new LanePoints();

                for (int point = 0; point < roadLanePoints.Length; point++)
                {
                    roadLanePoint.value = roadLanePoints[point];
                    roadLanePointsBuffer.Add(roadLanePoint);
                }
                //---------------------------------------------------

                //Create this Road/Lane/Connection combo
                ERLaneConnector[] laneConnectors = erConnectionEnd.GetLaneData(connectionIndex, erLaneData.laneIndex);

                for (int i = 0; i < laneConnectors.Length; i++)
                {
                    var laneConnector = laneConnectors[i];

                    var endRoad = erConnectionEnd.GetConnectedRoad(laneConnector.endConnectionIndex, out connectedTo);
                    var roadIdentityEnd = RoadConnectionIdentity(endRoad.gameObject);

                    Entity connectionEntity = entityManager.CreateEntity(connectionEntityArchetype);
                    entityManager.SetComponentData(
                        connectionEntity,
                        new ConnectionDetails
                        {
                            ConnectionIdentity = connectionIdentityEnd,
                            LaneIndexStart = erLaneData.laneIndex,
                            LaneIndexEnd = laneConnector.endLaneIndex,
                            RoadIdentityStart = roadIdentity,
                            RoadIdentityEnd = roadIdentityEnd,
                        });

                    var connectionLanePointsBuffer = entityManager.GetBuffer<LanePoints>(connectionEntity);
                    var connectionLanePoint = new LanePoints();

                    for (int x = 0; x < laneConnector.points.Length; x++)
                    {
                        connectionLanePoint.value = laneConnector.points[x];
                        connectionLanePointsBuffer.Add(connectionLanePoint);
                    }
                }
                //---------------------------------------------------
                                
                //Pick a connection and one of the output values to give to the cars that are about to be created
                int randomValue = UnityEngine.Random.Range(0, laneConnectors.Length);
                var laneConnect = laneConnectors[randomValue];
                var exitRoad = erConnectionEnd.GetConnectedRoad(laneConnect.endConnectionIndex, out connectedTo);

                nextLaneIndex = laneConnect.endLaneIndex;
                nextRoadIdentity = RoadConnectionIdentity(exitRoad.gameObject);
                connectionLanePoints = laneConnect.points.ToList();

                allLanePoints.Clear();
                allLanePoints.AddRange(roadLanePoints);
                allLanePoints.AddRange(connectionLanePoints);
                connectionLanePoints.Clear();

                //Create a car for each level
                for (int level = 0; level < roadLevelData.levelCount; level++)
                {
                    //Option to not fill every lane 
                    int random = UnityEngine.Random.Range(0, 100);

                    if (random <= percentageLanesPopulated)
                    {
                        CreateAutoEntity(
                            allLanePoints,
                            level,
                            nextLaneIndex,
                            nextRoadIdentity,
                            connectionIdentityEnd,
                            roadLevelData);
                    }
                }
            }
        }

        allLanePoints.Clear();
        connectionLanePoints.Clear();
    }

    private void CreateConnectionEntity(
        ERConnection erConnection,
        int connectionIndexStart,
        int roadIdentityStart,
        int laneIndex)
    {
        var entityArchetype = entityManager.CreateArchetype(
                    typeof(ERConnectionTag),
                    typeof(ConnectionDetails),
                    typeof(LanePoints)
                );
            
        ConnectedTo connectedTo;
        int connectionIdentity = RoadConnectionIdentity(erConnection.gameObject);
        ERLaneConnector[] laneConnectors = erConnection.GetLaneData(connectionIndexStart, laneIndex);
            
        if (laneConnectors != null)
        {
            for (int i = 0; i < laneConnectors.Length; i++)
            {
                var laneConnector = laneConnectors[i];
                var nextLaneIndex = laneConnector.endLaneIndex;
                var connectionLanePoints = laneConnector.points;
                var endRoad = erConnection.GetConnectedRoad(laneConnector.endConnectionIndex, out connectedTo);
                var roadIdentityEnd = RoadConnectionIdentity(endRoad.gameObject);
                Entity entity = entityManager.CreateEntity(entityArchetype);
                entityManager.SetComponentData(
                    entity,
                    new ConnectionDetails
                    {
                        ConnectionIdentity = connectionIdentity,
                        LaneIndexStart = laneConnector.startLaneIndex,
                        LaneIndexEnd = laneConnector.endLaneIndex,
                        RoadIdentityStart = roadIdentityStart,
                        RoadIdentityEnd = roadIdentityEnd,
                    });
            }
        }
    }

    private void CreateAutoEntity(
        List<Vector3> lanePoints,
        int level,
        int nextLaneIdentity,
        int nextRoadIdentity,
        int nextConnectionIdentity,
        ERRoadLevelData roadLevelData)
    {
        Entity prefab = autoEntities[UnityEngine.Random.Range(0, autoEntities.Count - 1)];
        var entity = entityManager.Instantiate(prefab);
       
        float speed = UnityEngine.Random.Range(speedMinimum, speedMaximum);
        float yOffset = roadLevelData.startHeight + ((level + 1) * roadLevelData.levelHeight);
        int currentIndex = UnityEngine.Random.Range(0, lanePoints.Count - vehicleLength);

        Vector3 translation = lanePoints[currentIndex];
        translation.y += yOffset;

        Vector3 destination = lanePoints[currentIndex + 1];
        destination.y += yOffset;

        float3 lookVector = destination - translation;
        Quaternion rotation = new Quaternion();
        if (!lookVector.Equals(float3.zero))
        {
            rotation = Quaternion.LookRotation(lookVector);
        }

        entityManager.SetComponentData(entity, 
            new AutoDetails {
            yOffset = yOffset,
            speed = speed });

        entityManager.SetComponentData(entity, 
            new AutoPosition
            {
            CurrentPositionIndex = currentIndex,
            Destination = destination,
            LaneIndex = nextLaneIdentity,
            RoadIdentity = nextRoadIdentity,
            ConnectionIdentity = nextConnectionIdentity
            });

        entityManager.SetComponentData(entity, new Translation { Value = translation });
        entityManager.SetComponentData(entity, new Rotation { Value = rotation });

        var lanePointsBuffer = entityManager.GetBuffer<AutoLanePoints>(entity);
        var autoLanePoints = new AutoLanePoints();

        for (int point = 0; point < lanePoints.Count; point++)
        {
            autoLanePoints.value = lanePoints[point];
            lanePointsBuffer.Add(autoLanePoints);
        }
    }

    private void AddIndentityMono()
    {
        /*
         * Set up a unique value for each Road and Connection.
         * This is because we cannot use the Road or Connection name in ECS as strings are not allowed (only bittable data can be used)
         * AND because road and connection names may not be unique.
         * We need the unique value to identify the relationship between road and connection
         */

        ERRoadNetwork roadNetwork = new ERRoadNetwork();
        ERRoad[] roads = roadNetwork.GetRoads();
        ERConnection[] connections = roadNetwork.GetConnections();

        int identity = 0;

        foreach (ERRoad road in roads)
        {
            road.gameObject.AddComponent<ERRoadConnectionIdentity>();
            road.gameObject.GetComponent<ERRoadConnectionIdentity>().value = identity;
            identity++;
        }

        foreach (ERConnection connection in connections)
        {
            connection.gameObject.AddComponent<ERRoadConnectionIdentity>();
            connection.gameObject.GetComponent<ERRoadConnectionIdentity>().value = identity;
            identity++;
        }
    }

    private int RoadConnectionIdentity(GameObject gameObject)
    {
        return gameObject.GetComponent<ERRoadConnectionIdentity>().value;
    }   
}
