using UnityEngine;

[CreateAssetMenu(fileName = "New Road Level Data", menuName = "EasyRoads3D/LevelData")]
public class ERRoadLevelData : ScriptableObject
{
    public int levelCount = 8;

    public float levelHeight = 80;

    public float startHeight = 80;
}