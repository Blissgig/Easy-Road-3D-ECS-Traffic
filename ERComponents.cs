using Unity.Entities;
using Unity.Mathematics;

public struct ERConnectionTag : IComponentData { }

public struct ERRoadTag : IComponentData { }

public struct ERAutoTag : IComponentData { }

public struct AutoDetails : IComponentData
{
    public float yOffset;
    public float speed;
}

public struct AutoPosition : IComponentData
{
    public int CurrentPositionIndex;
    public float3 Destination;
    public int LaneIndex;
    public int RoadIdentity;
    public int ConnectionIdentity;
    public int ConnectionIndex;

    //The values: LaneIndex, RoadIdentity, ConnectionIdentity, ConnectionIndex
    //are used when the Auto gets to the end of it's set of Points (AutoLanePoints)
    //to determine which is the next road.
}

/*  There must be a separate component for lanes that
    are part of the roads or connections, vs lanes 
    that are on the Auto.   This is because Unity
    throws an exception as the LanePoints would be 
    part of the job iternation as well as the sub queries.   
    It really f's up the code.  So the Autos have their own set
*/
public struct AutoLanePoints : IBufferElementData
{
    public float3 value;
}

public struct ConnectionDetails : IComponentData
{
    public int ConnectionIdentity;
    public int LaneIndexStart;
    public int LaneIndexEnd;
    public int RoadIdentityEnd;
    public int RoadIdentityStart;
    public int ConnectionIndexEnd;
    public int ConnectionIndexStart;
}

public struct RoadDetails : IComponentData
{
    public int RoadIdentity;
    public int LaneIndex;
    public int ConnectionIdentityEnd;       //The connection at the "end" of the road
    public int ConnectionIdentityStart;     //Connection at the "beginning" of the road
    public int ConnectionIndexEnd;          //The specific connection on the Intersection*  
    public int ConnectionIndexStart;

    //* The ERConnection object should have been named an "Intersection" that has a collection of "Connections"....  
    //  but this is just me whining about naming conventions.
}

public struct LanePoints : IBufferElementData
{
    public float3 value;
}
