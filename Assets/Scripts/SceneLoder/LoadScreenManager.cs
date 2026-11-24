using UnityEngine;
using UnityEngine.UI;

public class LoadScreenManager : MonoBehaviour
{
    public static LoadScreenManager Instance { get; private set; }

    public LoadSceneSelectionButton currentlySelectedSlot;
    public Button confirmButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    private void OnDestroy()
{
    // 내가 현재 활성화된 인스턴스였다면, 파괴될 때 참조를 비워줌
    if (Instance == this)
    {
        Instance = null;
    }
}

    private void Start()
    {
        // Disable the confirm button initially
        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }
    }

    public void SelectSlot(LoadSceneSelectionButton slot)
    {
        Debug.Log(slot);
        // Deselect the previously selected slot, if any
        if (currentlySelectedSlot != null)
        {
            currentlySelectedSlot.Deselect();
        }

        // Select the new slot
        currentlySelectedSlot = slot;
        currentlySelectedSlot.Select();

        // Enable the confirm button
        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }
    }

    public void ConfirmSelectionLoad()
    {
        if (true)//currentlySelectedSlot != null
        {
            //Debug.Log("Confirm button pressed. Loading data for slot: " + currentlySelectedSlot.slotId);

            if (SaveLoadManager.Instance != null)
            {
                //SaveLoadManager.Instance.LoadGame(currentlySelectedSlot.slotId);
                //SaveLoadManager.Instance.selectedIndex = currentlySelectedSlot.slotId;
                SaveLoadManager.Instance.selectedIndex = 0;
                SaveLoadManager.Instance.SettingMode(2);
            }
            else
            {
                Debug.LogError("SaveLoadManager instance not found!");
            }

            // --- YOU WILL IMPLEMENT YOUR LOADING LOGIC HERE ---
            // Example: SaveLoadManager.Instance.LoadGame(currentlySelectedSlot.slotId);
            // After loading data, you would typically load the corresponding scene.
            // UnityEngine.SceneManagement.SceneManager.LoadScene("YourGameScene");
        }
        else
        {
            //Debug.LogWarning("No load slot selected.");
        }
    }
    public void ConfirmSelectionSave()
    {
        if (true)//currentlySelectedSlot != null
        {
            //Debug.Log("Confirm button pressed. Saving data for slot: " + currentlySelectedSlot.slotId);

            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.SaveGame(0);//currentlySelectedSlot.slotId
            }
            else
            {
                Debug.LogError("SaveLoadManager instance not found!");
            }

            // --- YOU WILL IMPLEMENT YOUR LOADING LOGIC HERE ---
            // Example: SaveLoadManager.Instance.LoadGame(currentlySelectedSlot.slotId);
            // After loading data, you would typically load the corresponding scene.
            // UnityEngine.SceneManagement.SceneManager.LoadScene("YourGameScene");
        }
        else
        {
            //Debug.LogWarning("No load slot selected.");
        }
    }
    public void CheckGameSaveData()
    {
        var state = SaveLoadManager.Instance.ReadSlot(0, out _);
        if (state == SaveLoadManager.SlotState.Invalid)
        {
            const string message = "저장 데이터를 읽을 수 없습니다. 기존 저장은 유지됩니다.";
            Debug.LogWarning(message);
            if (LoadingManager.Instance != null)
                LoadingManager.Instance.ShowNotice(message);
            return;
        }

        if (state == SaveLoadManager.SlotState.Valid || state == SaveLoadManager.SlotState.Backup)
        {
            SaveLoadManager.Instance.SettingMode(2); // 2 = LoadGame
            //if (UpgradeManager.Instance != null) UpgradeManager.Instance.ResetRunData();
            //GameManager.Instance.SelectCharacter();//PlayerStats.Instance.CharacterID
            LoadingManager.Instance.LoadScene("Village");
        }
        else // If no save data exists (New Game)
        {
            SaveLoadManager.Instance.SettingMode(1); // 1 = NewGame
            //if (UpgradeManager.Instance != null) UpgradeManager.Instance.ResetRunData();
            //GameManager.Instance.SelectCharacter();
            LoadingManager.Instance.LoadScene("Village");
        }
    }

    private void OnApplicationQuit()
    {
        if (GameObject.FindGameObjectWithTag("Village") != null)
            ConfirmSelectionSave();
    }
}
