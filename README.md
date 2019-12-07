This code is designed for a hover car game I am creating.  Because I wanted to have autos at multiple heights ("Levels") and wanted the scene to feel filled with autos, not just one or two, hear and there.  I am also creating this game for VR, so frame rate is a REAL concern.

Thankfully, Unity has been working on a new technology called DOTS.   This document will not talk in detail about DOTS, for a primer I would recommend starting here:  https://unity.com/dots

DOTS allows for many more active objects within a scene, but getting it working can be a bit tricky.  You cannot access the Easy Roads 3D API in DOTS.   So I create a collection of entities; Roads, Connections and Autos.    With these groups I can iterate through the autos in a Job, if the Auto is at the end of it's current path (a series of points along a ERRoad and ERConnection) it then iterates through the Roads to find the next Road and next Connection.   This allows the traffic to be as random as possible.

This code is MOVEMENT process only.  There is no collision.

Because string values at not blittable, see https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types for details, ERRoads and ERConnections need to have an idientity added to them.   So the first code is; ERRoadConnectionIdentity.cs   This is simply a holder for an INT value, which is added to all Roads and Connections at runtime.

The next code objects are to deal with the fact that this code allows for Autos to be on multiple levels;  ERRoadLevelData is a scriptable object.   Each ER Road **HAS** to have ERRoadLevelDataHolder and an instance of the ERRoadLevelData.  This **HAS** to be done by you the developer.  So do this, I'll wait.   Really, got add this to all your roads now.

