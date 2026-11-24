using System;
using System.Collections.Generic;

[Serializable]
public class JobDiscoverySaveData
{
    public List<CharacterJobDiscoveries> characters = new List<CharacterJobDiscoveries>();
    public bool HasDiscovered(int characterId, string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return false;

        var record = FindCharacterRecord(characterId);
        return record?.discoveredJobIds?.Contains(jobId) == true;
    }

    public bool TryDiscover(int characterId, string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return false;

        var record = FindCharacterRecord(characterId);
        if (record == null)
        {
            record = new CharacterJobDiscoveries { characterId = characterId };
            if (characters == null)
                characters = new List<CharacterJobDiscoveries>();

            characters.Add(record);
        }

        if (record.discoveredJobIds == null)
            record.discoveredJobIds = new List<string>();

        if (record.discoveredJobIds.Contains(jobId))
            return false;

        record.discoveredJobIds.Add(jobId);
        return true;
    }

    private CharacterJobDiscoveries FindCharacterRecord(int characterId)
    {
        return characters?.Find(record =>
            record != null && record.characterId == characterId);
    }
}

[Serializable]
public class CharacterJobDiscoveries
{
    public int characterId;
    public List<string> discoveredJobIds = new List<string>();
}