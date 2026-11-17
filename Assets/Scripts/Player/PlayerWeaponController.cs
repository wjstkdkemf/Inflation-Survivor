using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    public List<WeaponBase> activeWeapons = new List<WeaponBase>();
    public List<WeaponBase> WeaponsFromEquipment = new List<WeaponBase>();
    [SerializeField] private Transform weaponHolder;
    private Transform weaponCreationRoot;
    public PlayerSkillHUD playerHUD;

    private sealed class WeaponState
    {
        public int LearnedLevel;
        public int EquipmentLevel;
        public WeaponBase Weapon;
    }
    private sealed class PreparedWeaponChange
    {
        public WeaponDataSO Data;
        public int LearnedLevel;
        public int EquipmentLevel;
        public WeaponBase Weapon;
    }

    private readonly Dictionary<WeaponDataSO, WeaponState> weaponStates = new Dictionary<WeaponDataSO, WeaponState>();
    private readonly Dictionary<WeaponDataSO, int> requestedEquipmentLevels = new Dictionary<WeaponDataSO, int>();
    private readonly List<WeaponDataSO> stateKeys = new List<WeaponDataSO>();
    private bool combatEnabled = true;
    private Transform GetWeaponCreationRoot()
    {
        if (weaponCreationRoot == null)
        {
            var root = new GameObject("WeaponCreationRoot");
            root.SetActive(false);
            root.transform.SetParent(weaponHolder, false);
            weaponCreationRoot = root.transform;
        }

        return weaponCreationRoot;
    }
    public bool AddWeapon(WeaponDataSO data)
    {
        if (data == null)
            return false;

        bool shouldFuse = GetLearnedLevel(data) == 0 && data.fusionWeaponData != null && data.fusionWeaponData.Count > 0;

        if (shouldFuse)
        {
            if (!TryPrepareFusion(data, out var changes))
                return false;

            CommitPreparedWeaponChanges(changes);
            RefreshHUD();
            return true;
        }

        WeaponState state = GetOrCreateState(data);
        int previousLevel = state.LearnedLevel;
        state.LearnedLevel++;

        if (!SyncWeapon(data, state))
        {
            state.LearnedLevel = previousLevel;

            if (state.LearnedLevel == 0 &&
                state.EquipmentLevel == 0 &&
                state.Weapon == null)
            {
                weaponStates.Remove(data);
            }

            return false;
        }

        RefreshHUD();
        return true;
    }
    private bool TryCreateWeapon(WeaponDataSO data, int level, out WeaponBase weapon)
    {
        weapon = null;
        if (data == null || level < 1 || weaponHolder == null ||
            data.weaponPrefab == null ||
            data.weaponPrefab.GetComponent<WeaponBase>() == null)
        {
            Debug.LogError("Weapon creation references are invalid.", this);
            return false;
        }

        GameObject instance = null;
        try
        {
            instance = Instantiate(
                data.weaponPrefab, GetWeaponCreationRoot(), false);
            instance.SetActive(false);
            instance.transform.SetParent(weaponHolder, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            var createdWeapon = instance.GetComponent<WeaponBase>();
            if (!createdWeapon.enabled ||
                !createdWeapon.TryInitialize(data) ||
                !createdWeapon.SetLevel(level))
            {
                Destroy(instance);
                return false;
            }

            weapon = createdWeapon;
            return true;
        }
        catch (System.Exception exception)
        {
            if (instance != null)
            {
                instance.SetActive(false);
                Destroy(instance);
            }
            Debug.LogException(exception, this);
            return false;
        }
    }
    private void CommitPreparedWeaponChanges(List<PreparedWeaponChange> changes)
    {
        foreach (PreparedWeaponChange change in changes)
        {
            if (!weaponStates.TryGetValue(change.Data, out var state))
                continue;

            WeaponBase oldWeapon = state.Weapon;
            activeWeapons.Remove(oldWeapon);
            WeaponsFromEquipment.Remove(oldWeapon);

            if (oldWeapon != null)
            {
                oldWeapon.gameObject.SetActive(false);
                Destroy(oldWeapon.gameObject);
            }
        }

        foreach (PreparedWeaponChange change in changes)
        {
            if (change.LearnedLevel == 0 && change.EquipmentLevel == 0)
            {
                weaponStates.Remove(change.Data);
                continue;
            }

            WeaponState state = GetOrCreateState(change.Data);
            state.LearnedLevel = change.LearnedLevel;
            state.EquipmentLevel = change.EquipmentLevel;
            state.Weapon = change.Weapon;

            List<WeaponBase> target = state.LearnedLevel > 0
                ? activeWeapons : WeaponsFromEquipment;
            target.Add(state.Weapon);
        }

        foreach (PreparedWeaponChange change in changes)
        {
            if (change.Weapon != null)
                change.Weapon.gameObject.SetActive(combatEnabled);
        }

        changes.Clear();
    }
    private bool TryPrepareFusion(WeaponDataSO data, out List<PreparedWeaponChange> changes)
    {
        changes = new List<PreparedWeaponChange>();

        if (data == null || data.fusionWeaponData == null ||
            data.fusionWeaponData.Count == 0)
            return false;

        var seen = new HashSet<WeaponDataSO> { data };
        foreach (WeaponDataSO ingredient in data.fusionWeaponData)
        {
            if (ingredient == null || !seen.Add(ingredient))
            {
                Debug.LogError(
                    $"Invalid fusion ingredients: {data.name}", this);
                return false;
            }
        }

        bool prepared = false;
        try
        {
            if (!TryPrepareWeaponChange(data, GetLearnedLevel(data) + 1, GetEquipmentLevel(data), changes))
                return false;

            foreach (WeaponDataSO ingredient in data.fusionWeaponData)
            {
                if (!weaponStates.TryGetValue(ingredient, out var state) ||
                    state.LearnedLevel == 0)
                    continue;

                if (!TryPrepareWeaponChange(ingredient, 0, state.EquipmentLevel, changes))
                    return false;
            }

            prepared = true;
            return true;
        }
        finally
        {
            if (!prepared)
                DiscardPreparedWeapons(changes);
        }
    }
    private bool TryPrepareWeaponChange(WeaponDataSO data, int learnedLevel, int equipmentLevel, List<PreparedWeaponChange> changes)
    {
        if (data == null || learnedLevel < 0 || equipmentLevel < 0)
            return false;

        int totalLevel = learnedLevel + equipmentLevel;
        WeaponBase preparedWeapon = null;

        if (totalLevel > 0 && !TryCreateWeapon(data, totalLevel, out preparedWeapon))
            return false;

        changes.Add(new PreparedWeaponChange
        {
            Data = data,
            LearnedLevel = learnedLevel,
            EquipmentLevel = equipmentLevel,
            Weapon = preparedWeapon
        });
        return true;
    }
    private void DiscardPreparedWeapons(List<PreparedWeaponChange> changes)
    {
        foreach (PreparedWeaponChange change in changes)
        {
            if (change.Weapon == null)
                continue;

            change.Weapon.gameObject.SetActive(false);
            Destroy(change.Weapon.gameObject);
        }
        changes.Clear();
    }
    private void LateUpdate()
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.CanProcessCombat)
            return;

        foreach (WeaponBase weapon in activeWeapons)
            if (weapon != null && weapon.isActiveAndEnabled) weapon.OnUpdate();
        foreach (WeaponBase weapon in WeaponsFromEquipment)
            if (weapon != null && weapon.isActiveAndEnabled) weapon.OnUpdate();
    }

    public void ToggleCombatMode(bool isCombat)
    {
        combatEnabled = isCombat;
        foreach (WeaponBase weapon in activeWeapons)
            if (weapon != null) weapon.gameObject.SetActive(isCombat);
        foreach (WeaponBase weapon in WeaponsFromEquipment)
            if (weapon != null) weapon.gameObject.SetActive(isCombat);
    }

    public int GetLearnedLevel(WeaponDataSO data)
    {
        return data != null && weaponStates.TryGetValue(data, out WeaponState state) ? state.LearnedLevel : 0;
    }

    public int GetEquipmentLevel(WeaponDataSO data)
    {
        return data != null && weaponStates.TryGetValue(data, out WeaponState state) ? state.EquipmentLevel : 0;
    }

    public bool SetEquipmentSkills(IReadOnlyList<WeaponDataSO> skills)
    {
        requestedEquipmentLevels.Clear();
        if (skills != null)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                WeaponDataSO data = skills[i];
                if (data == null) continue;
                requestedEquipmentLevels.TryGetValue(data, out int count);
                requestedEquipmentLevels[data] = count + 1;
                GetOrCreateState(data);
            }
        }

        // Snapshot keys because removing the last level also removes its state.
        stateKeys.Clear();
        stateKeys.AddRange(weaponStates.Keys);
        bool changed = false;
        bool allSucceeded = true;

        foreach (WeaponDataSO data in stateKeys)
        {
            WeaponState state = weaponStates[data];
            requestedEquipmentLevels.TryGetValue(data, out int level);

            if (state.EquipmentLevel == level && state.Weapon != null)
                continue;

            int previousLevel = state.EquipmentLevel;
            state.EquipmentLevel = level;

            if (!SyncWeapon(data, state))
            {
                state.EquipmentLevel = previousLevel;
                allSucceeded = false;

                if (state.LearnedLevel == 0 &&
                    state.EquipmentLevel == 0 &&
                    state.Weapon == null)
                {
                    weaponStates.Remove(data);
                }

                Debug.LogWarning(
                    $"Equipment skill could not be applied: {data.name}", this);
                continue;
            }

            changed = true;
        }

        if (changed)
            RefreshHUD();

        return allSucceeded;
    }

    private WeaponState GetOrCreateState(WeaponDataSO data)
    {
        if (!weaponStates.TryGetValue(data, out WeaponState state))
        {
            state = new WeaponState();
            weaponStates.Add(data, state);
        }
        return state;
    }

    private bool SyncWeapon(WeaponDataSO data, WeaponState state)
    {
        int totalLevel = state.LearnedLevel + state.EquipmentLevel;
        WeaponBase weapon = state.Weapon;
        if (totalLevel == 0)
        {
            activeWeapons.Remove(weapon);
            WeaponsFromEquipment.Remove(weapon);
            if (weapon != null)
            {
                weapon.gameObject.SetActive(false);
                Destroy(weapon.gameObject);
            }
            weaponStates.Remove(data);
            return true;
        }

        bool isNewWeapon = weapon == null;

        if (isNewWeapon)
        {
            if (!TryCreateWeapon(data, totalLevel, out weapon))
                return false;

            state.Weapon = weapon;        
        }
        else
        {
            if (!weapon.SetLevel(totalLevel))
                return false;
        }

        List<WeaponBase> target = state.LearnedLevel > 0 ? activeWeapons : WeaponsFromEquipment;
        List<WeaponBase> other = state.LearnedLevel > 0 ? WeaponsFromEquipment : activeWeapons;
        other.Remove(weapon);
        if (!target.Contains(weapon))
            target.Add(weapon);

        if (isNewWeapon)
            weapon.gameObject.SetActive(combatEnabled);

        return true;
    }

    private void RefreshHUD()
    {
        if (playerHUD != null) playerHUD.RefreshWeaponIcons();
    }

    public WeaponBase GetWeaponById(string weaponId)
    {
        TryGetWeaponById(weaponId, out WeaponBase weapon);
        return weapon;
    }

    public WeaponBase GetWeapon(WeaponDataSO data)
    {
        TryGetWeapon(data, out WeaponBase weapon);
        return weapon;
    }

    public bool TryGetWeapon(WeaponDataSO data, out WeaponBase weapon, bool includeEquipmentWeapons = false)
    {
        weapon = null;

        if (data == null)
            return false;

        for (int i = activeWeapons.Count - 1; i >= 0; i--)
        {
            if (activeWeapons[i] != null && activeWeapons[i].myData == data)
            {
                weapon = activeWeapons[i];
                return true;
            }
        }

        if (includeEquipmentWeapons)
        {
            for (int i = WeaponsFromEquipment.Count - 1; i >= 0; i--)
            {
                if (WeaponsFromEquipment[i] != null && WeaponsFromEquipment[i].myData == data)
                {
                    weapon = WeaponsFromEquipment[i];
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(data.WeaponId))
            return TryGetWeaponById(data.WeaponId, out weapon, includeEquipmentWeapons);

        return false;
    }

    public bool TryGetWeaponById(string weaponId, out WeaponBase weapon, bool includeEquipmentWeapons = false)
    {
        weapon = null;

        if (string.IsNullOrEmpty(weaponId))
            return false;

        for (int i = activeWeapons.Count - 1; i >= 0; i--)
        {
            if (activeWeapons[i] != null && activeWeapons[i].WeaponID == weaponId)
            {
                weapon = activeWeapons[i];
                return true;
            }
        }

        if (includeEquipmentWeapons)
        {
            for (int i = WeaponsFromEquipment.Count - 1; i >= 0; i--)
            {
                if (WeaponsFromEquipment[i] != null && WeaponsFromEquipment[i].WeaponID == weaponId)
                {
                    weapon = WeaponsFromEquipment[i];
                    return true;
                }
            }
        }

        return false;
    }
}
