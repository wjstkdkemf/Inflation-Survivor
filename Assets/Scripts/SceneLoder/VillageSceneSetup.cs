using UnityEngine;
using System.Collections;
using InventorySystem;
public class VillageSceneSetup : MonoBehaviour, ISceneInitializer
{
    public bool InitializationSucceeded { get; private set; }
    public string InitializationError { get; private set; }
    public AudioClip MainBGM;
    public QuestUIManager questUIManager;
    public IEnumerator Initialize()
    {
        InitializationSucceeded = false;
        InitializationError = null;
        if (GameManager.Instance == null)
        {
            InitializationError = "마을 초기화에 필요한 GameManager가 없습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }
        string prevScene = LoadingManager.Instance != null ? LoadingManager.Instance.PreviousSceneName : "";
        
        // 분기 처리
        if (LoadingManager.Instance != null && LoadingManager.Instance.IsFailureRecovery)
        {
            yield return StartCoroutine(InitFromFailedEntry());
        }
        else if (prevScene == "Main Menu 1" || string.IsNullOrEmpty(prevScene))
        {
            // [케이스 1] 게임을 처음 켰을 때 (타이틀 -> 마을)
            InitializationError = "마을 진입 초기화가 완료되지 않았습니다.";
            yield return StartCoroutine(InitFromTitle());
        }
        else if (prevScene == "GameplayScene") // 던전 씬 이름 확인 필요
        {
            // [케이스 2] 던전을 돌고 복귀했을 때 (던전 -> 마을)
            yield return StartCoroutine(InitFromDungeon());
        }
        else
        {
            // 그 외 (상점 등에서 왔을 때)
            Debug.Log("일반 복귀 초기화 수행");
        }
        if (!string.IsNullOrEmpty(InitializationError))
        {
            yield break;
        }
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(MainBGM);
        }
        else
        {
            Debug.LogWarning("[VillageSceneSetup] AudioManager is missing. BGM will not play.");
        }

        yield return null;
        InitializationSucceeded = true;
    }

    // 게임 시작 시 초기화 (데이터 로드 위주)
    private IEnumerator InitFromTitle()
    {
        Debug.Log(">>> 타이틀 화면에서 진입: 저장된 데이터 로드 중...");

        if (!AreTitleManagersReady())
        {
            InitializationError = "마을 데이터 로드에 필요한 매니저가 없습니다.";
            yield break;
        }

        TeleportManager.Instance.LoadData();
        if (SaveLoadManager.Instance != null &&
            SaveLoadManager.Instance.startMode != SaveLoadManager.GameStartMode.LoadGame)
            GameProgressManager.Instance.LoadProgress();
        InventoryController.instance.init();
        if (SaveLoadManager.Instance == null ||
            SaveLoadManager.Instance.startMode == SaveLoadManager.GameStartMode.LoadGame ||
            SaveLoadManager.Instance.startMode == SaveLoadManager.GameStartMode.NewGame)
        {
            InitializationError = "저장 데이터를 적용하지 못했습니다.";
            yield break;
        }
        GameManager.Instance.ResetPause();

        Resources.UnloadUnusedAssets();

        if (questUIManager != null) questUIManager.RefreshAllQuestUI();
        if (TutorialManager.Instance != null) TutorialManager.Instance.CheckTutorial();


        yield return new WaitForSeconds(0.5f); // 연출용 딜레이
        InitializationError = null;
    }

    // 던전 복귀 시 초기화 (정산 및 저장 위주)
    private IEnumerator InitFromDungeon()
    {
        Debug.Log(">>> 던전에서 복귀: 전리품 정산 및 자동 저장 중...");

        if (InventoryController.instance == null)
        {
            InitializationError = "던전 복귀 처리에 필요한 InventoryController가 없습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }

        InventoryController.instance.init();

        if (LoadScreenManager.Instance != null)
        {
            LoadScreenManager.Instance.ConfirmSelectionSave();
        }
        else
        {
            Debug.LogWarning("[VillageSceneSetup] LoadScreenManager is missing. Auto-save skipped.");
        }

        if (questUIManager != null) questUIManager.RefreshAllQuestUI();

        GameManager.Instance.ResetPause();

        yield return new WaitForSeconds(0.5f); // 연출용 딜레이
    }
    private IEnumerator InitFromFailedEntry()
    {
        var controller = InventoryController.instance;
        var inventoryManager = InventoryManager.Instance;

        if (controller == null || inventoryManager == null ||
            EquipmentEffectManager.Instance == null ||
            !controller.TryInitializeStructure() ||
            controller.GetInventory(InventoryController.HotBarInventoryName) == null ||
            controller.GetInventory(InventoryController.InventoryName) == null ||
            !inventoryManager.TryRestoreAllInventories("Current.json"))
        {
            InitializationError = "던전 진입 전 인벤토리를 복원하지 못했습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }

        controller.ReapplyAllEquipmentEffects();
        controller.SyncEquippedStatus(
            InventoryController.HotBarInventoryName, InventoryController.InventoryName);
        if (questUIManager != null)
            questUIManager.RefreshAllQuestUI();
        GameManager.Instance.ResetPause();
        yield return null;
    }
    private bool AreTitleManagersReady()
    {
        bool isReady = true;

        if (TeleportManager.Instance == null)
        {
            Debug.LogError("[VillageSceneSetup] TeleportManager is missing.");
            isReady = false;
        }
        if (GameProgressManager.Instance == null)
        {
            Debug.LogError("[VillageSceneSetup] GameProgressManager is missing.");
            isReady = false;
        }
        if (InventoryController.instance == null)
        {
            Debug.LogError("[VillageSceneSetup] InventoryController is missing.");
            isReady = false;
        }

        return isReady;
    }
}
