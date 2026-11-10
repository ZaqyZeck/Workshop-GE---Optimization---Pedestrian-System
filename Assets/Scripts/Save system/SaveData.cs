using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StructureSaveData
{
    public Vector3Int position;
    public Vector3Int roadPosition;
    public int structureId;
    //public int yHeight;

    public StructureSaveData(Vector3Int position, Vector3Int roadPosition, int structureId)
    {
        this.position = position;
        this.roadPosition = roadPosition;
        this.structureId = structureId;
        //this.yHeight = yHeight;
    }
}

[Serializable]
public class GameSaveData
{
    public List<StructureSaveData> structures = new List<StructureSaveData>();
}