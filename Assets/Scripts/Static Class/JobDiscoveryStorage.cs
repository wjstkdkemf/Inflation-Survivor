using System;
using System.IO;
using UnityEngine;

public static class JobDiscoveryStorage
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "jobDiscoveries.json");

    public static bool TryLoad(out JobDiscoverySaveData data)
    {
        data = null;
        try
        {
            string json = File.ReadAllText(SavePath);
            var loaded = JsonUtility.FromJson<JobDiscoverySaveData>(json);
            if (loaded == null || loaded.characters == null)
                throw new InvalidDataException("전직 발견 목록이 없습니다.");

            data = loaded;
            return true;
        }
        catch (FileNotFoundException)
        {
            data = new JobDiscoverySaveData();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[JobDiscovery] 기록 로드 실패: {e.Message}");
            return false;
        }
    }

    public static bool TrySave(JobDiscoverySaveData data)
    {
        if (data == null) return false;

        string path = SavePath;
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[JobDiscovery] 기록 저장 실패: {e.Message}");
            return false;
        }
    }
}