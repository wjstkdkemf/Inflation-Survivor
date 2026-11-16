using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireTrailOrbWeapon : OrbWeapon
{
    private GameObject trailPrefab;
    private float spawnInterval;
    private float trailDuration;
    private float trailDamageScaling;

    protected override bool InitializeWeapon(WeaponDataSO data)
    {
        if (!(data is FireTrailOrbDataSO fireData))
        {
            Debug.LogError("FireTrailOrbDataSO is required.", this);
            return false;
        }
        if (fireData.orbProjectilePrefab == null ||
            !fireData.orbProjectilePrefab.TryGetComponent<FireTrailOrb>(out _))
        {
            Debug.LogError("Fire trail orb requires a FireTrailOrb prefab.", this);
            return false;
        }

        // Orb initialization calls the virtual SetupSpawnedOrb method.
        trailPrefab = fireData.trailPrefab;
        spawnInterval = fireData.spawnInterval;
        trailDuration = fireData.trailDuration;
        trailDamageScaling = fireData.trailDamageScaling;
        return base.InitializeWeapon(data);
    }

    protected override void SetupSpawnedOrb(GameObject orb)
    {
        base.SetupSpawnedOrb(orb);

        if (orb.TryGetComponent<FireTrailOrb>(out var fireOrbScript))
        {
            float finalTrailDamage = GetDamage() * trailDamageScaling;
            fireOrbScript.SetTrailInfo(trailPrefab, spawnInterval, trailDuration, finalTrailDamage , WeaponID);
        }
    }

    private void UpdateTrailInfoForExistingOrbs()
    {
        float finalTrailDamage = GetDamage() * trailDamageScaling;
        foreach (var orbObj in spawnedOrbs)
        {
            if (orbObj != null && orbObj.TryGetComponent<FireTrailOrb>(out var fireOrbScript))
            {
                fireOrbScript.SetTrailInfo(trailPrefab, spawnInterval, trailDuration, finalTrailDamage , WeaponID);
            }
        }
    }

    protected override void RefreshDamage()
    {
        base.RefreshDamage();
        UpdateTrailInfoForExistingOrbs();
    }
}
