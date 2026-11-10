using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    public static void Save(List<StructureSaveData> structures)
    {
        GameSaveData saveData = new GameSaveData();
        saveData.structures = structures;

        string json = JsonUtility.ToJson(saveData, true);

        File.WriteAllText(SavePath, json);

        Debug.Log($"Game saved successfully at: {SavePath}");
    }

    public static GameSaveData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("Save file not found!");
            return new GameSaveData();
        }

        string json = File.ReadAllText(SavePath);

        GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

        Debug.Log("Game loaded successfully!");

        return saveData ?? new GameSaveData();
    }

    public static bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Save file deleted!");
        }
    }
}