using UnityEngine;

// [핵심 설명] FireTrailOrbWeapon을 상속받아 부모가 가진 장판 데이터 처리 로직을 재사용합니다.
public class TurretFireTrailOrbWeapon : FireTrailOrbWeapon
{
    private GameObject projectilePrefab;
    private float fireRate;
    private float fireRange;
    private float projectileDamageScaling;
    private float projectileSpeed;

    protected override bool InitializeWeapon(WeaponDataSO data)
    {
        if (!(data is TurretFireTrailOrbDataSO turretData))
        {
            Debug.LogError("TurretFireTrailOrbDataSO is required.", this);
            return false;
        }
        if (turretData.orbProjectilePrefab == null ||
            !turretData.orbProjectilePrefab.TryGetComponent<TurretFireTrailOrb>(out _) ||
            turretData.projectilePrefab == null ||
            !turretData.projectilePrefab.TryGetComponent<TurretBullet>(out _))
        {
            Debug.LogError("Turret orb requires TurretFireTrailOrb and TurretBullet prefabs.", this);
            return false;
        }

        // Populate derived settings before the base creates the orbs.
        projectilePrefab = turretData.projectilePrefab;
        fireRate = turretData.baseCooldown;
        fireRange = turretData.fireRange;
        projectileDamageScaling = turretData.projectileDamageScaling;
        projectileSpeed = turretData.projectileSpeed;
        return base.InitializeWeapon(data);
    }

    protected override void SetupSpawnedOrb(GameObject orb)
    {
        base.SetupSpawnedOrb(orb);

        if (orb.TryGetComponent<TurretFireTrailOrb>(out var turretOrbScript))
        {
            float finalProjectileDamage = GetDamage() * projectileDamageScaling;
            turretOrbScript.SetTurretInfo(projectilePrefab, fireRate, fireRange, finalProjectileDamage, projectileSpeed , WeaponID);
        }
    }

    private void UpdateTurretInfoForExistingOrbs()
    {
        float finalProjectileDamage = GetDamage() * projectileDamageScaling;
        foreach (var orbObj in spawnedOrbs)
        {
            if (orbObj != null && orbObj.TryGetComponent<TurretFireTrailOrb>(out var turretOrbScript))
            {
                turretOrbScript.SetTurretInfo(projectilePrefab, fireRate, fireRange, finalProjectileDamage , projectileSpeed , WeaponID);
            }
        }
    }

    protected override void RefreshDamage()
    {
        base.RefreshDamage();
        UpdateTurretInfoForExistingOrbs();
    }
}
