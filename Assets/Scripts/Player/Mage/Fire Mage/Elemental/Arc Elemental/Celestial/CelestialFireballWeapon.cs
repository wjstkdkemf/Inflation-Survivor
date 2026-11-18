using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CelestialFireballWeapon : WeaponBase
{
    private int bulletNumber;
    private float fireRate;
    private float range;
    private float projectileSpeed;
    private float lastFireBoomSize;
    private float lastFireBoomDamage;
    // 장판 관련
    private float trailDamageScaling;
    private float trailDuration;
    private float trailSpawnDistance;

    private int chains;
    private float chainRange;


    // [내부 변수]
    private GameObject fireballPrefab;
    private GameObject trailPrefab;
    private GameObject LastFireBoomPrefab;

    private float cooldownTimer;
    private float volleyTimer;
    private int pendingBullets = 0;
    private Enemy currentTarget;
    private uint currentTargetSpawnVersion;

    private bool HasValidCurrentTarget =>
        currentTarget != null &&
        currentTarget.isActiveAndEnabled &&
        currentTarget.currentNormalState != Enemy.EnemyState.Dead &&
        currentTarget.SpawnVersion == currentTargetSpawnVersion;
    protected override bool InitializeWeapon(WeaponDataSO data)
    {
        if (data is CelestialFireballSO evoData)
        {
            if (evoData.fireballPrefab == null ||
                !evoData.fireballPrefab.TryGetComponent<CelestialFireballProjectile>(out _))
            {
                Debug.LogError("Celestial fireball requires a CelestialFireballProjectile prefab.", this);
                return false;
            }
            bulletNumber = evoData.bulletNumber;
            fireRate = evoData.baseCooldown;
            range = evoData.range;
            projectileSpeed = evoData.projectileSpeed;
            currentDamage = evoData.baseDamage;

            lastFireBoomSize = evoData.FireBoomSize;
            
            // 장판 데이터
            trailDamageScaling = evoData.trailDamageScaling;
            trailDuration = evoData.trailDuration;
            trailSpawnDistance = evoData.trailSpawnDistance;

            chains = evoData.chainCount;
            chainRange = evoData.chainRange;

            fireballPrefab = evoData.fireballPrefab;
            trailPrefab = evoData.trailPrefab;
            LastFireBoomPrefab = evoData.lastFireBoomPrefab;
            attackMotion = evoData.attackMotion;
        }
        else
        {
            Debug.LogError("잘못된 데이터! CelestialFireballDataSO가 필요합니다.");
            return false;
        }
        cooldownTimer = 0f;
        return true;
    }

    public override void OnUpdate()
    {
        if (pendingBullets <= 0)
        {
            if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0f)
            {
                if (TryAcquireTarget())
                {
                    pendingBullets = bulletNumber;
                    volleyTimer = 0f;
                }
            }
        }
        else
        {
            volleyTimer -= Time.deltaTime;
            if (volleyTimer <= 0f)
            {
                FireProjectile();
            }
        }
    }

    void FireProjectile()
    {
            if (!HasValidCurrentTarget && !TryAcquireTarget())
            {
                pendingBullets = 0;
                cooldownTimer = GetCooldown(fireRate);
                return;
            }

            Enemy target = currentTarget;

        var pool = ObjectPoolingManager.Instance;
        if (pool == null) return;

        GameObject fireball = pool.spawnGameObject(fireballPrefab, transform.position, Quaternion.identity);
        if (fireball == null) return;
        if (!fireball.TryGetComponent<CelestialFireballProjectile>(out var evoScript))
        {
            pool.ReturnObjectToPool(fireball);
            return;
        }

        long directDamage = GetDamage();
        float finalTrailDamage = directDamage * trailDamageScaling;
        float boomDamage = GetFireBoomDamage();
        evoScript.Setup(
            target, projectileSpeed, trailPrefab, finalTrailDamage, trailDuration, trailSpawnDistance,
            LastFireBoomPrefab, boomDamage, lastFireBoomSize, chains, chainRange , WeaponID
        );
        evoScript.damage = directDamage; // SkillProjectileBase 직격 데미지

        pendingBullets--;
        if (pendingBullets > 0) volleyTimer = 0.1f;
        else cooldownTimer = GetCooldown(fireRate);
    }

    // 데미지 계산식
    public long GetDamage()
    {
        return CalculateDamage(currentDamage);
    }

    public long GetFireBoomDamage()
    {
        return CalculateDamage(currentDamage, 2d);
    }
    private bool TryAcquireTarget()
    {
        var swarm = EnemySwarmSystem.Instance;
        currentTarget = swarm != null
            ? swarm.GetClosestEnemy(transform.position, range)
            : null;

        currentTargetSpawnVersion =
            currentTarget != null ? currentTarget.SpawnVersion : 0;

        return HasValidCurrentTarget;
    }
    protected override bool RecalculateLevelStats()
    {
        if (!(myData is CelestialFireballSO data)) return false;
        currentDamage = CalculateLevelDamage(data.baseDamage, CurrentLevel);
        trailDamageScaling = CalculateLevelStat(SkillStatType.TrailDamageScaling, data.trailDamageScaling, CurrentLevel);
        DevLog.Log($"[Evo Fireball Level Up] 직격뎀: {currentDamage}, 장판계수: {trailDamageScaling}");
        return true;
    }
}
