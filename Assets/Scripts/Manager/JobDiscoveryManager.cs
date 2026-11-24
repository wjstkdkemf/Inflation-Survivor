using UnityEngine;

public sealed class JobDiscoveryManager : MonoBehaviour
{
    public static JobDiscoveryManager Instance { get; private set; }

    private JobDiscoverySaveData data;
    private bool isDirty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (JobDiscoveryStorage.TryLoad(out var loadedData))
            data = loadedData;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadData(JobDiscoverySaveData loadedData)
    {
        data = loadedData ?? new JobDiscoverySaveData();
    }

    public bool HasDiscovered(int characterId, string jobId)
    {
        return data != null && data.HasDiscovered(characterId, jobId);
    }

    public bool TryDiscover(int characterId, string jobId)
    {
        if (data == null)
        {
            Debug.LogError("[JobDiscovery] 기록을 로드하지 못해 발견을 등록하지 않았습니다.");
            return false;
        }

        bool discovered = data.TryDiscover(characterId, jobId);
        if (discovered)
            isDirty = true;

        SaveIfDirty();
        return discovered;
    }

    private void SaveIfDirty()
    {
        if (isDirty && JobDiscoveryStorage.TrySave(data))
            isDirty = false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveIfDirty();
    }

    private void OnApplicationQuit()
    {
        SaveIfDirty();
    }
}