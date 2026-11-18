using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElementalFireballProjectile : SkillProjectileBase
{
    private Enemy target;

    private uint targetSpawnVersion;

    private bool HasValidTarget =>
        target != null &&
        target.isActiveAndEnabled &&
        target.currentNormalState != Enemy.EnemyState.Dead &&
        target.SpawnVersion == targetSpawnVersion;
    
    // 장판 관련 데이터
    private GameObject trailPrefab;
    private float trailDamage;
    private float trailDuration;
    private float spawnDistanceThreshold;
    private Vector3 lastSpawnPosition; // 마지막으로 장판을 깐 위치

    private GameObject lastFireBoom;
    private float fireBoomDamage;
    private float fireBoomSize;

    private List<int> enemiesHitIndices = new List<int>(50);
    private float speed;

    // 무기에서 호출하여 데이터 초기화
    public void Setup(Enemy newTarget, float newSpeed, GameObject trail, float tDamage, float tDuration, float tSpawnDist, 
                      GameObject boomPrefab, float boomDamage, float boomSize , string WeaponID)
    {
        target = newTarget;
        targetSpawnVersion = target != null ? target.SpawnVersion : 0;
        speed = newSpeed;
        hitRadius = 0.5f;
        maxHits = 1;
       
        trailPrefab = trail;
        trailDamage = tDamage;
        trailDuration = tDuration;
        spawnDistanceThreshold = tSpawnDist;
    
        lastFireBoom = boomPrefab;
        fireBoomDamage = boomDamage;
        fireBoomSize = boomSize;

        lastSpawnPosition = transform.position;

        this.WeaponID = WeaponID;

        if (HasValidTarget)
            RotateTowardsTarget(target.transform.position);
    }
    void Update()
    {
        Vector3 moveDirection = transform.right;

        if (HasValidTarget)
        {
            Vector3 targetPosition = target.transform.position;
            moveDirection = (targetPosition - transform.position).normalized;
            RotateTowardsTarget(targetPosition);
        }
        else
        {
            target = null;
        }
        transform.position += moveDirection * speed * Time.deltaTime;

        if (Vector3.Distance(transform.position, lastSpawnPosition) >= spawnDistanceThreshold)
        {
            SpawnTrail();
            lastSpawnPosition = transform.position;
        }
    }

    void SpawnTrail()
    {
        if (trailPrefab == null) return;

        var pool = ObjectPoolingManager.Instance;
        if (pool == null) return;

        GameObject trail = pool.spawnGameObject(
            trailPrefab, transform.position, Quaternion.identity);

        if (trail == null) return;

        if (!trail.TryGetComponent<AuraZone>(out var trailSkill))
        {
            pool.ReturnObjectToPool(trail);
            return;
        }

        trailSkill.SetupAura(0.5f, (long)trailDamage, 1.5f,false, 0f, WeaponID, trailDuration);
    }
    public override void OnHit(Enemy hitEnemy)
    {
        if (lastFireBoom != null)
        {
            ObjectPoolingManager.Instance.spawnGameObject(lastFireBoom, transform.position, Quaternion.identity);
        }
        RunStatisticsManager.Instance.RecordWeaponDamage(WeaponID, damage);

        EnemySwarmSystem.Instance.GetEnemiesInRadius(transform.position, fireBoomSize, enemiesHitIndices);

        for (int i = 0; i < enemiesHitIndices.Count; i++)
        {
            int idx = enemiesHitIndices[i];
            
            if (idx >= EnemySwarmSystem.Instance.activeEnemies.Count) continue;
            Enemy target = EnemySwarmSystem.Instance.TryGetTarget(idx);
            if (target == null ) continue;

            if (Time.time >= EnemySwarmSystem.Instance.nextHitTimes[idx])
            {
                target.TakeDamage(fireBoomDamage);

                RunStatisticsManager.Instance.RecordWeaponDamage(
                    WeaponID, (long)fireBoomDamage);

                EnemySwarmSystem.Instance.nextHitTimes[idx] = Time.time + 0.1f;
            }
        }

        ObjectPoolingManager.Instance.ReturnObjectToPool(gameObject);
    }
    
    private void RotateTowardsTarget(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
