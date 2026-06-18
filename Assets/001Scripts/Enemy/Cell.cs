using UnityEngine;
using System.Collections.Generic;

public class Cell
{
    public Vector3 worldPos;
    public Vector2Int gridIndex;
    public byte cost;
    public ushort bestCost;
    public Vector3 bestDirection;

    public HashSet<Transform> enemiesInThisCell = new HashSet<Transform>();

    public Cell(Vector3 _worldPos, Vector2Int _gridIndex)
    {
        worldPos = _worldPos;
        gridIndex = _gridIndex;
        cost = 1;
        bestCost = ushort.MaxValue;
        bestDirection = Vector3.zero;
    }
}