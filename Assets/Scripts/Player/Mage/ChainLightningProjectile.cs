using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public class ChainLightningProjectile : SkillProjectileBase
{
    private Enemy currentTarget;
    private float speed;
    private int remainingChains;
    private float chainRange;
    private float damageReduction;
    private readonly Dictionary<Enemy, uint> visitedTargets = new Dictionary<Enemy, uint>();//어떤 적의 몇 번째 생명을 타격했는지 기록.
    private readonly HashSet<Enemy> excludedTargets = new HashSet<Enemy>();//그중 현재도 같은 생명인 적만 탐색에서 제외.
    private uint currentTargetSpawnVersion;

    private bool HasValidCurrentTarget =>
        currentTarget != null &&
        currentTarget.isActiveAndEnabled &&
        currentTarget.currentNormalState != Enemy.EnemyState.Dead &&
        currentTarget.SpawnVersion == currentTargetSpawnVersion;
    
    private bool isActived = false;

    // 무기 스크립트에서 호출하여 정보 초기화
    public void Fire(Enemy initialTarget, long startDamage, float projSpeed, int chains, float range, float reduction , string weaponID)
    {
        currentTarget = initialTarget;
        currentTargetSpawnVersion = currentTarget != null ? currentTarget.SpawnVersion : 0;
        damage = startDamage;
        speed = projSpeed;
        remainingChains = chains;
        chainRange = range;
        damageReduction = reduction;
        this.WeaponID = weaponID;
        
        hitRadius = 0.5f; // 투사체 크기
        maxHits = 1;      // 1프레임당 1마리만 때림 (튕기기 위함)
        
        visitedTargets.Clear();
        excludedTargets.Clear();
        isActived = true;

        RotateTowardsTarget();
    }

    void Update()
    {
        if (!isActived) return;

        // 타겟이 내게 오기 전에 다른 공격에 맞아 죽었다면?
        if (!HasValidCurrentTarget)
        {
            FindNextTarget();
            if (!isActived) return;
        }

        // 타겟을 향해 이동 (직진이 아니라 유도탄 방식)
        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;
        
        RotateTowardsTarget();
    }
    private void RotateTowardsTarget()
    {
        if (!HasValidCurrentTarget) return;
        Vector3 direction = currentTarget.transform.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
    protected override void OnDisable()
    {
        base.OnDisable();

        isActived = false;
        visitedTargets.Clear();
        excludedTargets.Clear();
    }
    public override void OnHit(Enemy hitEnemy)
    {
        if (hitEnemy != null)
            visitedTargets[hitEnemy] = hitEnemy.SpawnVersion;
        RunStatisticsManager.Instance.RecordWeaponDamage(WeaponID, damage);

        remainingChains--;
        damage = (long)(damage * (1 - damageReduction));

        if (remainingChains > 0)
        {
            FindNextTarget();
        }
        else
        {
            SelfDestruct();
        }
    }

    private void FindNextTarget()
    {
        excludedTargets.Clear();

        foreach (var entry in visitedTargets)
        {
            Enemy enemy = entry.Key;

            if (enemy != null && enemy.SpawnVersion == entry.Value)
                excludedTargets.Add(enemy);
        }

        Enemy nextTarget = EnemySwarmSystem.Instance.GetClosestEnemy(transform.position, chainRange, excludedTargets);

        if (nextTarget != null)
        {
            currentTarget = nextTarget;
            currentTargetSpawnVersion = nextTarget.SpawnVersion;
            RotateTowardsTarget();
        }
        else
        {
            SelfDestruct(); 
        }
    }

    private void SelfDestruct()
    {
        isActived = false;
        ObjectPoolingManager.Instance.ReturnObjectToPool(gameObject);
    }
}