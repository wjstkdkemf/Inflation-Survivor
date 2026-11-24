using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public static class SaveDataMigrator
{
    public const int CurrentGameSaveVersion = 2;

    public static bool TryReadGameSave(string jsonData, out GameSaveData saveData)
    {
        saveData = null;

        if (string.IsNullOrEmpty(jsonData) || jsonData.Trim().Length == 0)
        {
            Debug.LogWarning("[SaveDataMigrator] Save data is empty.");
            return false;
        }

        try
        {
            var root = JObject.Parse(jsonData);
            int version = (int?)root["saveVersion"] ?? 0;
            if (version > CurrentGameSaveVersion || version < 0 ||
                !(root["playerStatsData"] is JObject) ||
                root["inventoryJson"]?.Type != JTokenType.String ||
                !(JObject.Parse((string)root["inventoryJson"])["inventories"] is JArray inventories))
                return false;

            if (version >= 2 &&
                (!HasValidInventoryEntries(inventories) || !(root["powerUpData"] is JObject) ||
                 !(JObject.Parse((string)root["progressJson"])["items"] is JArray) ||
                 !(JObject.Parse((string)root["questJson"])["activeSubQuests"] is JArray)))
                return false;

            saveData = JsonUtility.FromJson<GameSaveData>(jsonData);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[SaveDataMigrator] Failed to parse save data: {exception.Message}");
            return false;
        }

        if (saveData == null)
        {
            Debug.LogError("[SaveDataMigrator] Save data parsed to null.");
            return false;
        }

        MigrateGameSave(saveData);
        return true;
    }

    private static bool HasValidInventoryEntries(JArray inventories)
    {
        var names = new HashSet<string>();
        foreach (var inventory in inventories)
        {
            if (!(inventory is JObject container) ||
                container["inventoryName"]?.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace((string)container["inventoryName"]) ||
                !names.Add((string)container["inventoryName"]) ||
                !(container["items"] is JArray items))
                return false;

            var positions = new HashSet<int>();
            foreach (var entry in items)
            {
                if (!(entry is JObject item) || item["name"]?.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace((string)item["name"]) ||
                    item["amount"]?.Type != JTokenType.Integer || (int)item["amount"] <= 0 ||
                    item["position"]?.Type != JTokenType.Integer || (int)item["position"] < 0 ||
                    !positions.Add((int)item["position"]))
                    return false;
            }
        }
        return true;
    }

    public static void PrepareForSave(GameSaveData saveData)
    {
        if (saveData == null) return;

        saveData.saveVersion = CurrentGameSaveVersion;
        saveData.appVersion = Application.version;
    }

    private static void MigrateGameSave(GameSaveData saveData)
    {
        int originalVersion = saveData.saveVersion;

        if (saveData.saveVersion <= 0)
        {
            MigrateLegacyGameSave(saveData);
        }

        RepairGameSave(saveData);

        if (saveData.saveVersion > CurrentGameSaveVersion)
        {
            Debug.LogWarning($"[SaveDataMigrator] Save version {saveData.saveVersion} is newer than supported version {CurrentGameSaveVersion}. Loading with best effort.");
        }

        if (originalVersion != saveData.saveVersion)
        {
            DevLog.Log($"[SaveDataMigrator] Migrated save data from version {originalVersion} to {saveData.saveVersion}.");
        }
    }

    private static void MigrateLegacyGameSave(GameSaveData saveData)
    {
        if (saveData.inventoryJson == null) saveData.inventoryJson = "";
        saveData.saveVersion = 1;
        saveData.appVersion = string.IsNullOrEmpty(saveData.appVersion) ? "Legacy" : saveData.appVersion;
    }

    private static void RepairGameSave(GameSaveData saveData)
    {
        if (saveData.inventoryJson == null) saveData.inventoryJson = "";
        if (saveData.playerStatsData == null) saveData.playerStatsData = new PlayerStatsData();

        if (saveData.playerStatsData.level <= 0) saveData.playerStatsData.level = 1;
        saveData.playerStatsData.requiredXP = PlayerStats.NormalizeRequiredXP(saveData.playerStatsData.requiredXP);
        if (saveData.playerStatsData.currentXP < 0) saveData.playerStatsData.currentXP = 0;
        if (saveData.playerStatsData.goldAmount < 0) saveData.playerStatsData.goldAmount = 0;
        if (saveData.playerStatsData.stageCleared < 0) saveData.playerStatsData.stageCleared = 0;
        if (saveData.playerStatsData.characterID < 0) saveData.playerStatsData.characterID = 0;
    }
}
