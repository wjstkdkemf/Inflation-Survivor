using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class SaveLoadManager : MonoBehaviour
{
    // Defines how the game should start in the next scene.
    
    public enum GameStartMode { None, NewGame, LoadGame , Running , ToIngame}
    // Stores the choice from the main menu.
    public GameStartMode startMode { get; set; } = GameStartMode.None;

    public static SaveLoadManager Instance { get; private set; }

    public bool IsLoadingFromFile { get; private set; } = false;
    private bool slotEntryPending;
    private bool appliedBackup;
    private string recoveredSlotJson;
    private int recoveredSlotNumber;

    public void BeginSlotEntry()
    {
        slotEntryPending = true;
        appliedBackup = false;
        recoveredSlotJson = null;
    }

    public bool TryCommitRecoveredSlot()
    {
        if (!slotEntryPending || !appliedBackup) return true;
        if (string.IsNullOrEmpty(recoveredSlotJson)) return false;
        try
        {
            GameProgressManager.Instance.SaveProgress();
            QuestManager.Instance.SaveQuestData();
            PlayerPrefs.SetString(SaveSlotKey + recoveredSlotNumber, recoveredSlotJson);
            PlayerPrefs.Save();
            recoveredSlotJson = null;
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public bool FinishSlotEntry()
    {
        bool notify = slotEntryPending && appliedBackup;
        slotEntryPending = false;
        appliedBackup = false;
        return notify;
    }
    public int selectedIndex { get; set; }

    // Temporary storage for transferring items between scenes.
    public List<InventorySystem.ItemSaveData> itemsToTransfer = null;

    private const string SaveSlotKey = "SaveSlot_";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SaveGame(int slotNumber)
    {
        if (!CanSaveGame())
        {
            return;
        }

        // Get save data from each manager.
        string invJson = InventorySystem.InventoryController.instance.GetSaveData();
        PowerUpSaveData powData = PowerUpManager.Instance.GetSaveData();
        PlayerStatsData statsData = PlayerStats.Instance.GetSaveData();

        // Create the main save data object.
        GameSaveData saveData = new GameSaveData(invJson, powData, statsData);
        saveData.progressJson = GameProgressManager.Instance.CaptureProgressJson();
        saveData.questJson = QuestManager.Instance.CaptureQuestJson();
        if (InventoryManager.Instance != null)
            saveData.pendingInventoryItems = InventoryManager.Instance.GetPendingSaveData();
        SaveDataMigrator.PrepareForSave(saveData);
        DevLog.Log("Loading Inventory JSON: " + saveData.inventoryJson);
        
        // Serialize to JSON and save to PlayerPrefs.
        string jsonData = JsonUtility.ToJson(saveData, true);
        string saveKey = SaveSlotKey + slotNumber;
        if (!SaveDataMigrator.TryReadGameSave(jsonData, out _))
        {
            Debug.LogError("[SaveLoadManager] Invalid save candidate; existing slot and backup retained.");
            return;
        }
        string previous = PlayerPrefs.GetString(saveKey, "");
        if (!string.IsNullOrEmpty(previous) &&
            SaveDataMigrator.TryReadGameSave(previous, out var oldData) && oldData.saveVersion == 2)
        {
            PlayerPrefs.SetString(saveKey + "_Previous", previous);
            PlayerPrefs.Save();
        }
        PlayerPrefs.SetString(saveKey, jsonData);
        PlayerPrefs.Save();

        GameProgressManager.Instance.SaveProgress();
        QuestManager.Instance.SaveQuestData();

        DevLog.Log($"<color=green>[SaveLoadManager]</color> Game Saved to Slot {slotNumber}.");
    }

    public bool LoadGame(int slotNumber)
    {
        try { return TryApplySlot(slotNumber); }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private bool TryApplySlot(int slotNumber)
    {
        if (!CanLoadGame())
        {
            return false;
        }

        SlotState state = ReadSlot(slotNumber, out var saveData);
        DevLog.Log("[SaveLoadManager] LoadGame started.");

        if (saveData != null)
        {
            if (state != SlotState.Backup)
                GameProgressManager.Instance.LoadProgress();
            // Distribute data to the managers.
            DevLog.Log("Loading Inventory JSON: " + saveData.inventoryJson);
            if (!InventorySystem.InventoryController.instance.TryRestoreFromData(saveData.inventoryJson))
                return false;
            // 불러온 장비 아이템 효과를 다시 적용합니다.
            InventorySystem.InventoryController.instance.ReapplyAllEquipmentEffects();
            InventorySystem.InventoryController.instance.ReapplyAllEquipmentSkills();
            InventorySystem.InventoryController.instance.SyncEquippedStatus("HotBar", "Inventory");
            EquipmentEffectManager.Instance.Change = false;

            PowerUpManager.Instance.LoadData(saveData.powerUpData);
            PlayerStats.Instance.LoadData(saveData.playerStatsData);
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.LoadPendingSaveData(saveData.pendingInventoryItems);
                InventoryManager.Instance.RestoreInventoryTo(InventorySystem.InventoryController.InventoryName);
            }
            if (state == SlotState.Backup)
            {
                if (!GameProgressManager.Instance.TryRestoreProgressSnapshot(saveData.progressJson) ||
                    !QuestManager.Instance.TryRestoreQuestSnapshot(saveData.questJson))
                    return false;
            }
            else QuestManager.Instance.LoadQuestData();
            IsLoadingFromFile = true; // Set the flag
            appliedBackup = state == SlotState.Backup;
            if (appliedBackup)
            {
                recoveredSlotNumber = slotNumber;
                recoveredSlotJson = JsonUtility.ToJson(saveData);
            }
            DevLog.Log($"<color=green>[SaveLoadManager]</color> Game Loaded from Slot {slotNumber}.");
            return true;
        }
        else
        {
            Debug.LogError($"[SaveLoadManager] Cannot load slot {slotNumber}. Existing progress was not reset.");
            return false;
        }
    }

    public void ClearAllDataForNewGame()
    {
        // Clear data in all managers for a fresh start.
        if (InventorySystem.InventoryController.instance != null) InventorySystem.InventoryController.instance.ClearAllInventories();
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearStoredInventory();
            InventoryManager.Instance.DeleteSavedInventoryFile("Current.json");
        }
        if (PowerUpManager.Instance != null) PowerUpManager.Instance.LoadData(null); // Will load defaults
        if (PlayerStats.Instance != null) PlayerStats.Instance.LoadData(null); // Will load defaults
        if (GameProgressManager.Instance != null) GameProgressManager.Instance.ResetProgress();
        if (QuestManager.Instance != null) QuestManager.Instance.ResetQuestDataForNewGame();
        if (TeleportManager.Instance != null) TeleportManager.Instance.ResetDataForNewGame();

        itemsToTransfer = null;
        IsLoadingFromFile = false; // Reset the flag
        DevLog.Log("<color=yellow>[SaveLoadManager]</color> All data has been reset for a new game.");
    }

    public bool CanSaveGame()
    {
        if (slotEntryPending) return false;
        if (!IsVillageReadyForSave()) return false;
        
        bool canSave = true;

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: pending inventory storage is missing.");
            canSave = false;
        }

        if (InventorySystem.InventoryController.instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: InventoryController is missing.");
            canSave = false;
        }
        if (PowerUpManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: PowerUpManager is missing.");
            canSave = false;
        }
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: PlayerStats is missing.");
            canSave = false;
        }
        if (GameProgressManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: GameProgressManager is missing.");
            canSave = false;
        }
        if (QuestManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot save: QuestManager is missing.");
            canSave = false;
        }

        return canSave;
    }

    private bool CanLoadGame()
    {
        bool canLoad = true;
        if (GameProgressManager.Instance == null || EquipmentEffectManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: progress or equipment manager is missing.");
            canLoad = false;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: pending inventory storage is missing.");
            canLoad = false;
        }

        if (InventorySystem.InventoryController.instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: InventoryController is missing.");
            canLoad = false;
        }
        if (PowerUpManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: PowerUpManager is missing.");
            canLoad = false;
        }
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: PlayerStats is missing.");
            canLoad = false;
        }
        if (QuestManager.Instance == null)
        {
            Debug.LogError("[SaveLoadManager] Cannot load: QuestManager is missing.");
            canLoad = false;
        }

        return canLoad;
    }

    public GameSaveData GetSaveSlotData(int slotNumber)
    {
        ReadSlot(slotNumber, out var data);
        return data;
    }
    private bool IsVillageReadyForSave()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Village")
            return true;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var setup = root.GetComponentInChildren<VillageSceneSetup>(true);
            if (setup != null)
                return setup.InitializationSucceeded;
        }

        // 마을인데 초기화 담당자가 없다면 정상 데이터라고 보장할 수 없다.
        return false;
    }

    public enum SlotState { Missing, Valid, Backup, Invalid }

    public SlotState ReadSlot(int slotNumber, out GameSaveData data)
    {
        data = null;
        string key = SaveSlotKey + slotNumber;
        if (PlayerPrefs.HasKey(key) && SaveDataMigrator.TryReadGameSave(PlayerPrefs.GetString(key), out data))
            return SlotState.Valid;
        if (PlayerPrefs.HasKey(key + "_Previous") &&
            SaveDataMigrator.TryReadGameSave(PlayerPrefs.GetString(key + "_Previous"), out data) &&
            data.saveVersion == 2)
            return SlotState.Backup;
        data = null;
        return PlayerPrefs.HasKey(key) || PlayerPrefs.HasKey(key + "_InvalidBackup") ||
            PlayerPrefs.HasKey(key + "_Previous") ? SlotState.Invalid : SlotState.Missing;
    }

    public void DeleteSaveData(int slotNumber)
    {
        string saveKey = SaveSlotKey + slotNumber;
        if (PlayerPrefs.HasKey(saveKey) || PlayerPrefs.HasKey(saveKey + "_InvalidBackup") ||
            PlayerPrefs.HasKey(saveKey + "_Previous"))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.DeleteKey(saveKey + "_InvalidBackup");
            PlayerPrefs.DeleteKey(saveKey + "_Previous");
            PlayerPrefs.Save();
            DevLog.Log($"<color=yellow>[SaveLoadManager]</color> Save data for slot {slotNumber} deleted.");
        }
    }

    public void SettingMode(int mode)
    {
        startMode = (GameStartMode)mode;
        DevLog.Log(startMode);
    }
}
