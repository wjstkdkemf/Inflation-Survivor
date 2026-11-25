using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 리스트 검색용 (FirstOrDefault 등)
using System.IO;
using TMPro;
using UnityEngine.Localization.Settings;
using Unity.VisualScripting;   // 저장 기능용

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance;

    [Header("Core References")]
    public PlayerWeaponController playerWeaponController; // 무기 관리자 연결
    public GameObject PlayerObject; // 패시브 스탯 적용을 위한 플레이어 참조

    [Header("UI References")]
    [SerializeField] private GameObject UpgradePanelObject; // 전체 UI 패널
    public List<GameObject> UpgradeUiSlots; // UI 슬롯들
    public PowerUpScriptableObject PowerUpgrade;
    private UpgradeUi focusedCard;

    [Header("Data Deck")]
    public List<UpgradeScriptableObject> MasterDeck;
    public List<UpgradeScriptableObject> StartingDeck;
    public List<UpgradeScriptableObject> UpgradeDeck; // 뽑을 카드 목록
    private List<UpgradeScriptableObject> spawnedUpgrades = new List<UpgradeScriptableObject>();
    private int UpgradeCount = 3;
    private bool InGameUpgrade = false;
    public Transform UpgradeUIContainer;
    public GameObject UpgradeUIPrefab;

    [Header("Job System")]
    public List<JobDataSO> allJobs; // 모든 직업 데이터 리스트
    private JobDataSO currentJob = null;

    [Header("Level Up Queue")]
    // 처리되지 않고 대기 중인 업그레이드 횟수
    public int pendingUpgrades = 0;

    private int drawVersion;
    private JobDataSO preferredJob;
    private readonly List<JobDataSO> routeBuffer = new();
    private readonly List<JobDataSO> eligibleJobs = new();
    private readonly HashSet<JobDataSO> declinedJobs = new();

    [SerializeField] private GameObject offRoutePanel;
    [SerializeField] private TMP_Text offRouteMessage;
    private JobDataSO pendingOffRouteJob;

    // 저장 경로
    private string saveUpgradeFilePath;

    private void Awake()
    {
        Instance = this;
        saveUpgradeFilePath = Path.Combine(Application.persistentDataPath, "PlayerUpgradeData.json");
    }

    private void Start()
    {
        // 런타임 데이터 초기화 (저장된 포인트/확률 불러오기 등)
        // 만약 PersistentDataManager를 쓴다면 여기서 Sync 로직 호출
        // SyncChancesFromPersistentData(); 
        
        // UI 초기화
        UpgradeUiSlots.Clear();
        SetUpgradePanelState(false);
    }
    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RequestPause(this, false);
    }
    public void SetUpgradeUICount()
    {
        ClearCardFocus();
        
        foreach (Transform child in UpgradeUIContainer) Destroy(child.gameObject);

        for(int i = 0 ; i < UpgradeCount ; i++)
        {
            GameObject UIObj = Instantiate(UpgradeUIPrefab, UpgradeUIContainer);
            UpgradeUiSlots.Add(UIObj);
        }
    }
    public void SetUpgradeCount()
    {
         if (PowerUpgrade.CurrentLevel > 0 && PowerUpgrade.CurrentLevel <= PowerUpgrade.upgradeValues.Length - 1)
            UpgradeCount = (int)PowerUpgrade.upgradeValues[PowerUpgrade.CurrentLevel] + 3;
        Debug.Log(UpgradeCount);
    }
    public void ResetRunData(List<UpgradeScriptableObject> startingDeck)
    {
        Debug.Log("새 게임을 위해 데이터를 초기화합니다...");

        // 덱 초기화
        // MasterDeck에 있는 모든 카드를 복사해서 UpgradeDeck으로 가져옴
        SetUpgradeCount();
        SetUpgradeUICount();

        // 카드 상태 초기화
        foreach (var card in MasterDeck)
        {
            card.Points = 0;
            card.Chance = card.InitialChance;
        }
        if(startingDeck.Count != 0)
            UpgradeDeck = new List<UpgradeScriptableObject>(startingDeck);
        else
            UpgradeDeck = new List<UpgradeScriptableObject>(StartingDeck);

        // 직업 상태 초기화
        currentJob = null;
        preferredJob = null;
        pendingOffRouteJob = null;
        declinedJobs.Clear();
        offRoutePanel.SetActive(false);
    }

    public void DisplayUpgrades()
    {
        ClearCardFocus();
        spawnedUpgrades.Clear();
        drawVersion++;

        for (int i = UpgradeDeck.Count - 1; i >= 0; i--)
        {
            if (UpgradeDeck[i].Points >= UpgradeDeck[i].MaxPoints)
            {
                UpgradeDeck.RemoveAt(i);
            }
        }

        // 게임 일시정지 및 UI 켜기
        if (MenuButtonController.Instance != null) MenuButtonController.Instance.CloseAllMenus();
        if (GameManager.Instance != null) GameManager.Instance.RequestPause(this, true);


        InGameUpgrade = true;
        SetUpgradePanelState(true);

        foreach (var slot in UpgradeUiSlots)
        {
            if (slot.activeSelf) // 켜져 있는 것만 끔
            {
                slot.SetActive(false);
            }
        }

        // 랜덤 뽑기 로직
        List<UpgradeScriptableObject> availableUpgrades = new List<UpgradeScriptableObject>(UpgradeDeck);
        int slotsCount = Mathf.Min(UpgradeCount, availableUpgrades.Count);

        for (int i = 0; i < UpgradeCount; i++)
        {
            if (i < slotsCount)
            {
                // 가중치합계 계산
                int totalChance = availableUpgrades.Sum(x => x.Chance);

                UpgradeScriptableObject chosenUpgrade = null;
                if (totalChance <= 0)
                {
                    // 비상 대책: 확률 무시하고 그냥 아무거나 뽑음
                    if (availableUpgrades.Count > 0)
                    {
                        int fallbackIndex = Random.Range(0, availableUpgrades.Count);
                        chosenUpgrade = availableUpgrades[fallbackIndex];
                    }
                }
                else
                {
                    int randomValue = Random.Range(0, totalChance);
                    foreach (var upgrade in availableUpgrades)
                    {
                        if (randomValue < upgrade.Chance)
                        {
                            chosenUpgrade = upgrade;
                            break;
                        }
                        randomValue -= upgrade.Chance;
                    }
                }

                // 슬롯에 데이터 세팅
                if (chosenUpgrade != null)
                {
                    UpgradeUiSlots[i].SetActive(true);
                    // UI 슬롯 스크립트에 정보 전달
                    UpgradeUiSlots[i].GetComponent<UpgradeUi>().SetInfo(
                        chosenUpgrade,
                        playerWeaponController
                    );
                    
                    spawnedUpgrades.Add(chosenUpgrade);
                    availableUpgrades.Remove(chosenUpgrade); // 중복 등장 방지
                }
            }
            else
            {
                UpgradeUiSlots[i].SetActive(false); // 남는 슬롯 끄기
            }
        }
        var player = PlayerStats.Instance;
        if (player == null || currentJob == null || player.TargetJob == null)
            return;

        CollectNextJobsTowardTarget(currentJob, player.TargetJob, routeBuffer);
        if (TryRecommendJobCard(routeBuffer, preferredJob, out var card, out _))
            StartCoroutine(ApplyRecommendedCardNextFrame(card));
    }

    public void OnUpgradeSelected(UpgradeScriptableObject chosenUpgrade)
    {
        if (chosenUpgrade == null || pendingOffRouteJob != null)
            return;

        if (chosenUpgrade.IsWeaponUpgrade)
        {
            if (playerWeaponController == null ||
                !playerWeaponController.AddWeapon(chosenUpgrade.linkedWeaponData))
            {
                Debug.LogWarning(
                    $"Upgrade could not be applied: {chosenUpgrade.name}", this);
                return;
            }
        }
        else
        {
            if (PlayerStatsCalculate.Instance == null)
                return;

            ApplyStatBonus(chosenUpgrade);
        }

        chosenUpgrade.Points++;
        RefreshPreferredJob();
        ClearCardFocus();
        ResolveJobsOrContinue();
    }
    public void HandleCardClick(UpgradeUi card)
    {
        if (!InGameUpgrade || card == null ||
            !card.isActiveAndEnabled || card.Upgrade == null || pendingOffRouteJob != null)
            return;

        if (!UpgradeUiSlots.Contains(card.gameObject))
            return;

        if (focusedCard == card)
        {
            var upgrade = card.Upgrade;
            OnUpgradeSelected(upgrade);
            return;
        }

        ClearCardFocus();
        focusedCard = card;
        focusedCard.SetFocused(true);
    }

    private void ClearCardFocus()
    {
        var previous = focusedCard;
        focusedCard = null;

        if (previous != null)
            previous.SetFocused(false);
    }

    public void ReleaseCardFocus(UpgradeUi card)
    {
        if (focusedCard == card)
            ClearCardFocus();
    }

    private void ApplyStatBonus(UpgradeScriptableObject upgrade)
    {
        PlayerStatsCalculate.Instance.AddRealTimeBonus(upgrade);
    }
    private JobDataSO FindAutomaticJob(JobDataSO target)
    {
        if (eligibleJobs.Count == 0)
            return null;

        // 최초 전직, 목표 미설정, 목표 도달 후에는 기존 자동 전직을 유지한다.
        if (currentJob == null || target == null || currentJob == target)
            return eligibleJobs[0];

        CollectNextJobsTowardTarget(currentJob, target, routeBuffer);

        JobDataSO firstOnRoute = null;
        foreach (var job in eligibleJobs)
        {
            if (!routeBuffer.Contains(job))
                continue;

            if (job == target)
                return job;

            if (firstOnRoute == null || job == preferredJob)
                firstOnRoute = job;
        }

        return firstOnRoute;
    }
    private void ResolveJobsOrContinue()
    {
        while (true)
        {
            CollectEligibleJobs(eligibleJobs);

            if (eligibleJobs.Count == 0)
            {
                ProcessNextUpgrade();
                return;
            }

            var player = PlayerStats.Instance;
            var target = player != null ? player.TargetJob : null;
            var automaticJob = FindAutomaticJob(target);

            if (automaticJob != null)
            {
                SetJob(automaticJob);
                continue;
            }

            pendingOffRouteJob = eligibleJobs[0];
            RefreshOffRouteMessage();
            offRoutePanel.SetActive(true);
            return;
        }
    }
    private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnJobLocaleChanged;
    private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnJobLocaleChanged;

    private void OnJobLocaleChanged(UnityEngine.Localization.Locale locale) => RefreshOffRouteMessage();

    private void RefreshOffRouteMessage()
    {
        var target = PlayerStats.Instance != null ? PlayerStats.Instance.TargetJob : null;
        if (pendingOffRouteJob == null || target == null) return;
        offRouteMessage.text = string.Format(UpgradeLocalization.GetUI("job.off_route"),
            pendingOffRouteJob.LocalizedName, target.LocalizedName);
    }

    public void AnswerOffRouteJob(bool accept)
    {
        var job = pendingOffRouteJob;
        if (job == null) return;

        pendingOffRouteJob = null;
        offRoutePanel.SetActive(false);

        if (accept)
        {
            PlayerStats.Instance.ClearTargetJob();
            SetJob(job);
        }
        else
        {
            declinedJobs.Add(job);
        }

        ResolveJobsOrContinue();
    }
    private void CollectEligibleJobs(List<JobDataSO> results)
    {
        results.Clear();

        var candidates = currentJob == null ? allJobs : currentJob.nextAbleJobs;
        if (candidates == null)
            return;

        foreach (var job in candidates)
        {
            if (job == null || declinedJobs.Contains(job) || results.Contains(job))
                continue;

            if (CheckJobRequirements(job))
                results.Add(job);
        }
    }

    private bool CheckJobRequirements(JobDataSO job)
    {
        if(job == null)
            return false;

        foreach (JobDataSO.JobRequirement req in job.requirements)
        {
            // 플레이어가 해당 업그레이드 카드를 가지고 있는지 확인
            UpgradeScriptableObject myUpgrade = MasterDeck.Find(u => u.name == req.requiredUpgrade.name);

            // 카드가 아예 없다면 탈락
            if (myUpgrade == null)
            {
                //Debug.Log($"전직 실패: {req.requiredUpgrade.Title} 없음");
                return false; 
            }

            // 카드는 있지만 레벨이 부족하면 탈락
            if (myUpgrade.Points < req.requiredLevel)
            {
                //Debug.Log($"전직 실패: {myUpgrade.Title} 레벨 부족 (현재:{myUpgrade.Points} / 필요:{req.requiredLevel})");
                return false;
            }
        }
        return true;
    }
    private static void CollectNextJobsTowardTarget(JobDataSO current, JobDataSO target, List<JobDataSO> results)
    {
        results.Clear();

        if (current == null || target == null || current == target ||
            current.nextAbleJobs == null)
            return;

        var visited = new HashSet<JobDataSO>();

        foreach (var next in current.nextAbleJobs)
        {
            if (next == null || results.Contains(next))
                continue;

            visited.Clear();
            visited.Add(current);

            if (CanReachJob(next, target, visited))
                results.Add(next);
        }
    }
    private bool TryScoreJobCard(JobDataSO job, UpgradeScriptableObject card, out int remaining)
    {
        return TryCalculateRemaining(job, card, out remaining, out bool helps) && helps;
    }
    private bool TryCalculateRemaining(JobDataSO job, UpgradeScriptableObject extraCard,
                                    out int remaining, out bool helps)
    {
        remaining = 0;
        helps = false;
        if (job == null || job.requirements == null) return false;

        foreach (var req in job.requirements)
        {
            if (req.requiredUpgrade == null) return false;

            var owned = MasterDeck.Find(c => c != null && c.name == req.requiredUpgrade.name);
            if (owned == null) return false;

            int missing = Mathf.Max(0, req.requiredLevel - owned.Points);
            if (missing > 0 && extraCard != null && extraCard.name == owned.name)
            {
                missing--;
                helps = true;
            }
            remaining += missing;
        }
        return true;
    }
    private void RefreshPreferredJob()
    {
        var player = PlayerStats.Instance;
        if (player == null || currentJob == null || player.TargetJob == null)
            return;

        CollectNextJobsTowardTarget(currentJob, player != null ? player.TargetJob : null, routeBuffer);

        JobDataSO best = null;
        int bestRemaining = int.MaxValue;
        foreach (var route in routeBuffer)
        {
            if (!TryCalculateRemaining(route, null, out int remaining, out _)) continue;

            if (best == null || remaining < bestRemaining ||
                (remaining == bestRemaining && route == preferredJob))
            {
                best = route;
                bestRemaining = remaining;
            }
        }
        preferredJob = best;
    }
    private bool TryRecommendJobCard(List<JobDataSO> routes, JobDataSO preferredJob,
                                    out UpgradeScriptableObject bestCard, out JobDataSO bestJob)
    {
        bestCard = null;
        bestJob = null;

        bool bestIsPreferred = false;
        int bestRemaining = int.MaxValue;

        foreach (var card in spawnedUpgrades)
        {
            foreach (var job in routes)
            {
                if (!TryScoreJobCard(job, card, out int remaining))
                    continue;

                bool isPreferred = job == preferredJob;

                bool isBetter = bestCard == null
                            || remaining < bestRemaining
                            || (remaining == bestRemaining && isPreferred && !bestIsPreferred);

                if (!isBetter)
                    continue;

                bestCard = card;
                bestJob = job;
                bestIsPreferred = isPreferred;
                bestRemaining = remaining;
            }
        }

        return bestCard != null;
    }
    private static bool CanReachJob(JobDataSO job, JobDataSO target, HashSet<JobDataSO> visited)
    {
        if (job == null || !visited.Add(job))
            return false;

        if (job == target)
            return true;

        if (job.nextAbleJobs == null)
            return false;

        foreach (var next in job.nextAbleJobs)
        {
            if (CanReachJob(next, target, visited))
                return true;
        }

        return false;
    }
    private System.Collections.IEnumerator ApplyRecommendedCardNextFrame(UpgradeScriptableObject card)
    {
        int version = drawVersion;
        yield return null;

        if (version != drawVersion || !InGameUpgrade || !spawnedUpgrades.Contains(card))
            yield break;

        OnUpgradeSelected(card);
    }
    private void SetJob(JobDataSO newJob)
    {
        currentJob = newJob;
        
        var player = PlayerStats.Instance;
        if (player != null && player.TargetJob == newJob)
            player.ClearTargetJob();

        preferredJob = null;
        declinedJobs.Clear();
        var discoveries = JobDiscoveryManager.Instance;
        var playerStats = PlayerStats.Instance;

        if (discoveries != null && playerStats != null)
            discoveries.TryDiscover(playerStats.CharacterID, newJob.JobId);
        else
            Debug.LogWarning("[JobDiscovery] 전직은 적용됐지만 발견 기록을 등록하지 못했습니다.");
            
        Debug.Log($"[Job Change] {newJob.jobName} 전직 완료!");

        // 직업 보너스 스킬 추가
        if (newJob.bonusUpgrades != null)
        {
            foreach (var bonusCard in newJob.bonusUpgrades)
            {
                if (!UpgradeDeck.Contains(bonusCard))
                {
                    //bonusCard.Chance += newJob.bonusChanceAmount; 
                    UpgradeDeck.Add(bonusCard);
                }
                else
                {
                    bonusCard.Chance += newJob.bonusChanceAmount;
                }
            }
        }

        // 금지된 스킬 제거
        if (newJob.bannedUpgrades != null)
        {
            foreach (var bannedCard in newJob.bannedUpgrades)
            {
                if (UpgradeDeck.Contains(bannedCard))
                {
                    UpgradeDeck.Remove(bannedCard);
                    
                    // 혹시 모르니 확률도 초기화해둘 수 있음
                    // bannedCard.Chance = 0; 
                    
                    Debug.Log($"[Deck] 직업 제한으로 스킬 제거됨: {bannedCard.Title}");
                }
            }
        }
            
        // SaveUpgradeData(); 
    }
    public void CollectRelatedJobs(UpgradeScriptableObject upgrade,List<JobDataSO> results)
    {
        results.Clear();

        if (upgrade == null)
            return;

        List<JobDataSO> candidates = currentJob == null
            ? allJobs
            : currentJob.nextAbleJobs;

        if (candidates == null)
            return;

        foreach (JobDataSO job in candidates)
        {
            if (job == null || job.requirements == null)
                continue;

            foreach (var requirement in job.requirements)
            {
                var required = requirement.requiredUpgrade;

                if (required == null || required.name != upgrade.name)
                    continue;

                if (!results.Contains(job))
                    results.Add(job);

                break;
            }
        }
    }

    public void Close()
    {
        ClearCardFocus();
        if (GameManager.Instance != null) GameManager.Instance.RequestPause(this, false);
        InGameUpgrade = false;
        
        SetUpgradePanelState(false);
        spawnedUpgrades.Clear();
        pendingOffRouteJob = null;
        offRoutePanel.SetActive(false);
    }
    public void AddPendingUpgrade()
    {
        pendingUpgrades++;

        // 만약 지금 UI가 꺼져있다면, 바로 보여주기 시작
        if (!InGameUpgrade) 
        {
            ProcessNextUpgrade();
        }
    }
    private void ProcessNextUpgrade()
    {
        if (pendingUpgrades > 0)
        {
            // 대기열 하나 소모
            pendingUpgrades--; 
            
            DisplayUpgrades(); 
        }
        else
        {
            // 더 이상 남은 게 없으면 진짜 종료
            Close();
        }
    }

    private void SetUpgradePanelState(bool isActive)
    {
        if (UpgradePanelObject != null) UpgradePanelObject.SetActive(isActive);
    }
    
    public void SetCombatState(bool isActive)
    {
        if (playerWeaponController != null)
        {
            playerWeaponController.ToggleCombatMode(isActive);
        }
        else
        {
            Debug.LogWarning("PlayerWeaponController가 연결되지 않았습니다!");
        }
    }
}
