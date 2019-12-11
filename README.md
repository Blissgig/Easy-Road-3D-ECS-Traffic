Dec 11th, 2019.   There is an issue because I do not currently use ConnectionIndex in my model.  I am working this out now.   However I am going on holiday on the 13th.   Fingers crossed I will get it done before then.

-------------------------
This code is designed for a hover car game I am creating.  Because I wanted to have autos at multiple heights ("Levels") and wanted the scene to feel filled with autos, not just one or two, hear and there.  I am also creating this game for VR, so frame rate is a REAL concern.

Thankfully, Unity has been working on a new technology called DOTS.   This document will not talk in detail about DOTS, for a primer I would recommend starting here:  https://unity.com/dots

I watched this video for the list of all the DOTS/ECS packages:  https://youtu.be/C56bbgtPr_w   Make sure that "Hybrid Renderer" is also installed as without it the Auto entities will not be visible.

DOTS allows for many more active objects within a scene, but getting it working can be a bit tricky.  You cannot access the Easy Roads 3D API in DOTS.   So I create a collection of entities; Roads, Connections and Autos.    With these groups I can iterate through the autos in a Job, if the Auto is at the end of it's current path (a series of points along a ERRoad and ERConnection) it then iterates through the Roads to find the next Road and next Connection.   This allows the traffic to be as random as possible.

This code is MOVEMENT process only.  There is no collision.

Because string values at not blittable, see https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types for details, ERRoads and ERConnections need to have an idientity added to them.   So the first code is; ERRoadConnectionIdentity.cs   This is simply a holder for an INT value, which is added to all Roads and Connections at runtime.

The next code objects are to deal with the fact that this code allows for Autos to be on multiple levels;  ERRoadLevelData is a scriptable object.   Each ER Road **HAS** to have ERRoadLevelDataHolder and an instance of the ERRoadLevelData.  This **HAS** to be done by you the developer.  So do this, I'll wait.   Really, got add this to all your roads now.

The two main code files are: ER3D_Traffic.cs and ER3D_TrafficSystem.cs   ER3D_Traffic needs to be added to a game object as this code creates the Auto, Road and Connection entities.

ER3D_Traffic has a few options:
* "autos" - This is a list of GameObjects that will be converted to Entities
* "speedMinimum"
* "speedMaximum" - These are used to randomly set the speed to each Auto.
* "vehicleLength" - This is used when setting the initial placement of the autos on the road to insure that they are not too far into the next Road/Connection.
* "percentageLanesPopulated" - This value determines if an Auto is placed on a road lane.  If set to 100, then each road, each lane, each level will get an Auto when the scene starts.

When ER3D_Traffic starts:
1) Adds "ERRoadConnectionIdentity" to each Road and Connection and updates it's Identity value.  See function; "AddIndentityMono"
2) Converts the Auto game objects to a new list of Entities.   This saves a ton of time of converting each Auto during the process of adding the Autos to the scene.  See function:  "CreateAutoEntities"
3) Creates the ER Road and Connection entities.   Each Road or Connection has a collection of Lanes, each lane has collection of Points.   Connections have an Entry Lane and Exit Lane, as well as an Entry Road and Exit Road.   So entities are created for all instances of these options.   These are used in ER3D_TrafficSystem.
4) TODO: Remove ERRoadConnectionIdentity from roads and connections after the entities have been created.  No need for them.   I have not do so yet as I have additional tests to run.

Process:
* Autos are assigned to a Road and given it's series of Lane points as well as the next connection and one of the paths through the connection and those points.   
* The benefit of DOTS is speed, and it is terrific, so to avoid having to query the Road object when an Auto reaches the next point, a set of Road and Connection points is added to a car, when it reaches the end of these points the code, the Job iterates through the Road and Connection Entities to get the next set of points.   While this is a duplication of data, I was told by Unity that for DOTS duplicate data, in this instance, is prefered.   Source:  https://forum.unity.com/threads/speed-vs-redundant-data.776660/#post-5168798

Note: There is currently a hack in reference to the list of road and connection entities in the job.  In the "JobHandle OnUpdate" the code checks to see if the road list is at zero, if so creates the lists.   Creating these lists in the OnCreate causes an issue, sometimes, because the entities are not created in ER3D_Traffic before the Job starts.   Will need to find a solution to this, so at the moment this check works.  (hack!)

Note: There is a known bug in which if a connection does not have roads attached, or if a road does not have a connection at both ends there will be an issue.

Note: Make sure to check off "Enable GPU Instancing" on each Material you are using.  This affects performance.

First off I want to thank Raoul from http://www.unityterraintools.com/   His asset, Easy Roads 3D is very good, however it is his support of this product that impresses me so much.  Bravo Raoul!

Second is that  Unity's DOTS technology is still in beta, as is the Lane data in ER3D, so this code works as of today; Dec 7th 2019.   

Third, this release is to help enlighten others to the power of DOTS, but beware it took a lot of hours to get this information and the code working.  So if you find a problem with this code, or with DOTS in general, give yourself some time and some patience.   It's a great technology and it's brand new.   I absolutely hear your frustration, but this is going to be SUCH a help for VR development.

If you have any comments or improvements, please let me know.
