using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq;
using System.IO;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    // [핵심 추가] 이전 씬의 이름을 저장할 변수
    public string PreviousSceneName { get; private set; } = "";

    // ... (UI 변수 및 Awake는 기존과 동일) ...
    [Header("UI Components")]
    [SerializeField] private GameObject loadingCanvasPrefab;
    private GameObject loadingCanvasInstance;
    private Slider progressBar;
    private TextMeshProUGUI loadingText;
    [SerializeField] private LoadFailureNotice failureNoticePrefab;
    private bool isLoading; // 목적지 시도부터 복귀 종료까지, 전체 전환 동안 새 요청 차단.
    private bool lastLoadSucceeded; // 방금 끝난 한 번의 씬 로딩 결과.
    public bool IsFailureRecovery { get; private set; } // 두 번째 LoadSequence() 동안만 true. 마을 Setup이 이 값을 보고 정산·자동 저장을 건너뜀.
    private string returnSceneName;
    private string pendingFailureMessage;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        TryLoadScene(sceneName);
    }

    public bool TryLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[LoadingManager] 이동할 씬 이름이 비어 있습니다.");
            return false;
        }

        if (isLoading || IsFailureRecovery)
        {
            Debug.LogWarning($"[LoadingManager] 이미 씬 전환 중입니다: {sceneName}");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LoadingManager] 로드할 수 없는 씬입니다: {sceneName}");
            return false;
        }

        returnSceneName = SceneManager.GetActiveScene().name;
        PreviousSceneName = returnSceneName;
        pendingFailureMessage = null;

        StartCoroutine(LoadWithRecovery(sceneName));
        return true;
    }

    public void LoadScene(int sceneIndex)
    {
        string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
        if (string.IsNullOrEmpty(scenePath))
        {
            Debug.LogWarning($"[LoadingManager] Cannot load scene because build index {sceneIndex} is invalid.");
            return;
        }

        LoadScene(Path.GetFileNameWithoutExtension(scenePath));
    }

    private IEnumerator LoadWithRecovery(string destination)
    {
        isLoading = true;
        try
        {
            bool isSlotEntry = returnSceneName == "Main Menu 1" && destination == "Village";
            var saves = SaveLoadManager.Instance;
            if (isSlotEntry && saves != null)
                saves.BeginSlotEntry();

            yield return LoadSequence(destination);

            if (lastLoadSucceeded && isSlotEntry && saves != null && saves.FinishSlotEntry())
            {
                const string backupNotice = "저장 데이터를 읽지 못해 이전 저장본으로 복구했습니다.";
                ShowNotice(backupNotice);
            }

            if (lastLoadSucceeded || pendingFailureMessage == null)
                yield break;

            bool canRecover =
                (returnSceneName == "Main Menu 1" && destination == "Village") ||
                (returnSceneName == "Village" && destination == "GameplayScene");

            if (!canRecover)
                yield break;

            PreviousSceneName = SceneManager.GetActiveScene().name;
            IsFailureRecovery = true;

            yield return LoadSequence(returnSceneName);

            if (!lastLoadSucceeded)
            {
                pendingFailureMessage = null;
                Debug.LogError($"[LoadingManager] 출발 씬 복귀 실패: {returnSceneName}");
                yield break;
            }
            if (returnSceneName == "Main Menu 1" && Time.timeScale == 0f &&
                (GameManager.Instance == null || !GameManager.Instance.Pause))
            {
                Time.timeScale = 1f;
            }
            string message = pendingFailureMessage;
            pendingFailureMessage = null;

            if (!string.IsNullOrEmpty(message))
            {
                ShowNotice(message);
            }
            // 복귀 성공. pendingFailureMessage는 다음 단계에서 안내창이 소비한다.
        }
        finally
        {
            IsFailureRecovery = false;
            isLoading = false;
        }
    }
    public void ShowNotice(string message)
    {
        if (failureNoticePrefab != null)
            Instantiate(failureNoticePrefab).Show(message);
        else
            Debug.LogError($"[LoadingManager] 안내창 프리팹 누락: {message}");
    }

    private IEnumerator LoadSequence(string sceneName)
    {
        lastLoadSucceeded = false;
        // ... (이전과 동일한 로딩 시퀀스 로직) ...
        // 1. 로딩 UI 생성
        if (loadingCanvasInstance == null)
        {
            if (loadingCanvasPrefab == null)
            {
                Debug.LogError("[LoadingManager] Loading canvas prefab is missing.");
                yield break;
            }

            loadingCanvasInstance = Instantiate(loadingCanvasPrefab);
            DontDestroyOnLoad(loadingCanvasInstance);
            progressBar = loadingCanvasInstance.GetComponentInChildren<Slider>();
            loadingText = loadingCanvasInstance.GetComponentInChildren<TextMeshProUGUI>();
        }
        loadingCanvasInstance.SetActive(true);
        if (progressBar != null)
            progressBar.value = 0f;

        // 2. 데이터 매니저 초기화 (공통)
        var previousGameManager = GameManager.Instance;
        if (previousGameManager != null)
            previousGameManager.RequestPause(this, true);

        string initializationError = null;
        try
        {
            yield return PrepareCurrentSceneForExit();

            if (loadingText != null)
                loadingText.text = "데이터 매니저 초기화 중...";

            if (progressBar != null)
                progressBar.value = 0.3f;

            // 3. 씬 로드
            if (loadingText != null)
                loadingText.text = "이동 중...";
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingManager] Failed to start loading scene: {sceneName}");
                loadingCanvasInstance.SetActive(false);
                yield break;
            }

            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                float progress = Mathf.Clamp01(op.progress / 0.9f);
                if (progressBar != null)
                    progressBar.value = 0.3f + (progress * 0.4f);
                if (op.progress >= 0.9f) op.allowSceneActivation = true;
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            yield return null;

            // 4. 씬별 초기화 실행
            if (loadingText != null)
                loadingText.text = "환경 설정 적용 중...";
            if (progressBar != null)
                progressBar.value = 0.8f;

            var loadedScene = SceneManager.GetActiveScene();

            var initializers = FindObjectsOfType<MonoBehaviour>()
                                .Where(component => component.gameObject.scene == loadedScene &&
                                                    component.isActiveAndEnabled)
                                .OfType<ISceneInitializer>()
                                .ToArray();

            if (loadedScene.name == "Village" &&
                !initializers.Any(setup => setup is VillageSceneSetup))
            {
                initializationError = "마을 씬에 활성화된 VillageSceneSetup이 없습니다.";
            }
            else if (loadedScene.name == "GameplayScene" &&
                    !initializers.Any(setup => setup is DungeonSceneSetup))
            {
                initializationError = "던전 씬에 활성화된 DungeonSceneSetup이 없습니다.";
            }

            if (initializationError != null)
            {
                Debug.LogError($"[LoadingManager] {initializationError}");
                yield break;
            }
            foreach (var initializer in initializers)
            {
                yield return StartCoroutine(initializer.Initialize());

                if (!initializer.InitializationSucceeded)
                {
                    initializationError = string.IsNullOrEmpty(initializer.InitializationError)
                                        ? $"{initializer.GetType().Name}: 초기화를 완료하지 못했습니다."
                                        : initializer.InitializationError;

                    Debug.LogError($"[LoadingManager] {initializationError}");
                    yield break;
                }
            }

            if (!IsFailureRecovery && returnSceneName == "Main Menu 1" && sceneName == "Village" &&
                SaveLoadManager.Instance != null && !SaveLoadManager.Instance.TryCommitRecoveredSlot())
            {
                initializationError = "복구한 저장 데이터를 기록하지 못했습니다.";
                Debug.LogError(initializationError);
                yield break;
            }
            if (progressBar != null)
                progressBar.value = 1.0f;
            if (loadingText != null)
                loadingText.text = "완료!";
            yield return new WaitForSecondsRealtime(0.5f);

            lastLoadSucceeded = true;
        }
        finally
        {
            if (previousGameManager != null)
                previousGameManager.RequestPause(this, false);

            if (initializationError != null)
            {
                pendingFailureMessage = "화면을 불러오지 못해 이전 화면으로 돌아왔습니다.";
                if (loadingText != null)
                    loadingText.text = $"초기화 실패\n{initializationError}";

                var currentGameManager = GameManager.Instance;
                if (currentGameManager != null)
                    currentGameManager.RequestPause(this, true);
            }
            else
            {
                if (loadingCanvasInstance != null)
                    loadingCanvasInstance.SetActive(false);
            }
        }
    }
    private IEnumerator PrepareCurrentSceneForExit()
    {
        MainMapManager.Instance?.CancelForSceneExit();
        
        var waveSpawner = WaveSpawner.Instance;
        if (waveSpawner != null)
            waveSpawner.StopWaves();
        EnCounterSystem.Instance?.CancelEncounterForMapChange();
        ObjectPoolingManager.Instance?.ClearAllPools();

        yield return null;

        if (waveSpawner != null)
        {
            yield return new WaitUntil(() => !waveSpawner.HasPendingWaveLoads);
            waveSpawner.ReleaseWaveAssets();
        }
    }
}
