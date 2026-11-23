using System.Collections;
using UnityEngine;

public class DungeonSceneSetup : MonoBehaviour, ISceneInitializer
{
    public bool InitializationSucceeded { get; private set; }
    public string InitializationError { get; private set; }
    public QuestUIManager questUIManager;
    public IEnumerator Initialize()
    {
        Debug.Log("던전 씬 초기화 시작...");
        InitializationSucceeded = false;
        InitializationError = null;

        if (!AreManagersReady())
        {
            InitializationError = "던전 초기화에 필요한 매니저가 없습니다.";
            yield break;
        }

        EnCounterSystem.Instance.InitializeSceneComponents();
        TeleportManager.Instance.SetInitialSpawnPoint();

        PlayerStats.Instance.init();
        CharacterSelection characterSelection = GameManager.Instance.GetComponent<CharacterSelection>();
        if (characterSelection != null)
        {
            characterSelection.OnceSetting();
        }
        else
        {
            Debug.LogWarning("[DungeonSceneSetup] CharacterSelection is missing on GameManager.");
        }
        GameManager.Instance.SearchPlayer();
        GameManager.Instance.ResetPause();
        
        ObjectPoolingManager.Instance.PrewarmDamageText(100);
        
        InventorySystem.InventoryController.instance.init();

        GameObject player = GameManager.Instance.Player;
        if (player == null)
        {
            InitializationError = "던전 플레이어를 찾을 수 없습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }
        if (!player.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            InitializationError = "플레이어에 PlayerHealth가 없습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }

        playerHealth.InitializeForNewScene();
        var projectileManager = EnemyProjectileManager.Instance;
        if (projectileManager == null || !projectileManager.BindPlayer(player))
        {
            InitializationError = "적 투사체의 플레이어 대상 연결에 실패했습니다.";
            Debug.LogError(InitializationError);
            yield break;
        }

        Resources.UnloadUnusedAssets();

        if (questUIManager != null) questUIManager.RefreshAllQuestUI();
        yield return new WaitForSeconds(0.5f); // 연출용 대기

        InitializationSucceeded = true;
    }

    private bool AreManagersReady()
    {
        bool isReady = true;

        if (EnCounterSystem.Instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] EnCounterSystem is missing.");
            isReady = false;
        }
        if (TeleportManager.Instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] TeleportManager is missing.");
            isReady = false;
        }
        if (PlayerStats.Instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] PlayerStats is missing.");
            isReady = false;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] GameManager is missing.");
            isReady = false;
        }
        if (ObjectPoolingManager.Instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] ObjectPoolingManager is missing.");
            isReady = false;
        }
        if (InventorySystem.InventoryController.instance == null)
        {
            Debug.LogError("[DungeonSceneSetup] InventoryController is missing.");
            isReady = false;
        }

        return isReady;
    }
}
