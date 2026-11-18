using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElementalFireballWeapon : WeaponBase
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
        if (data is ElementalFireballSO evoData)
        {
            if (evoData.fireballPrefab == null ||
                !evoData.fireballPrefab.TryGetComponent<ElementalFireballProjectile>(out _))
            {
                Debug.LogError("Elemental fireball requires an ElementalFireballProjectile prefab.", this);
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

            fireballPrefab = evoData.fireballPrefab;
            trailPrefab = evoData.trailPrefab;
            LastFireBoomPrefab = evoData.lastFireBoomPrefab;

            attackMotion = evoData.attackMotion;
        }
        else
        {
            Debug.LogError("잘못된 데이터! ElementalFireballDataSO가 필요합니다.");
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
        else // 연사(Volley) 처리
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
        if (!fireball.TryGetComponent<ElementalFireballProjectile>(out var evoScript))
        {
            pool.ReturnObjectToPool(fireball);
            return;
        }

        long directDamage = GetDamage();
        float finalTrailDamage = directDamage * trailDamageScaling;
        float boomDamage = GetFireBoomDamage();
        evoScript.Setup(target, projectileSpeed, trailPrefab, finalTrailDamage, trailDuration, trailSpawnDistance,
                        LastFireBoomPrefab, boomDamage, lastFireBoomSize , WeaponID);
        evoScript.damage = directDamage; // 직격 딜 세팅

        pendingBullets--;

        if (pendingBullets > 0) volleyTimer = 0.1f; // 0.1초 딜레이
        else cooldownTimer = GetCooldown(fireRate);
    }

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
        if (!(myData is ElementalFireballSO data)) return false;
        currentDamage = CalculateLevelDamage(data.baseDamage, CurrentLevel);
        trailDamageScaling = CalculateLevelStat(SkillStatType.TrailDamageScaling, data.trailDamageScaling, CurrentLevel);
        DevLog.Log($"[Evo Fireball Level Up] 직격뎀: {currentDamage}, 장판계수: {trailDamageScaling}");
        return true;
    }
}
