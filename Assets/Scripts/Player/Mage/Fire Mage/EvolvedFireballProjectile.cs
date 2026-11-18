using UnityEngine;

public class EvolvedFireballProjectile : SkillProjectileBase
{
    private Enemy target;
    private uint targetSpawnVersion;

    private bool HasValidTarget =>
        target != null &&
        target.isActiveAndEnabled &&
        target.currentNormalState != Enemy.EnemyState.Dead &&
        target.SpawnVersion == targetSpawnVersion;
    private float speed;
    
    // 장판 관련 데이터
    private GameObject trailPrefab;
    private float trailDamage;
    private float trailDuration;
    private float spawnDistanceThreshold;

    private Vector3 lastSpawnPosition; // 마지막으로 장판을 깐 위치
    public void SetupEvo(Enemy newTarget, float newSpeed, GameObject trail, long directDmg, float tDamage, float tDuration, float tSpawnDist , string weaponID)
    {
        target = newTarget;
        targetSpawnVersion = target != null ? target.SpawnVersion : 0;
        speed = newSpeed;
        damage = directDmg;
        hitRadius = 0.5f; // 알바생(Job)이 쓸 투사체 직격 크기
        maxHits = 1;      // 관통 불가

        this.WeaponID = weaponID;

        trailPrefab = trail;
        trailDamage = tDamage;
        trailDuration = tDuration;
        spawnDistanceThreshold = tSpawnDist;

        lastSpawnPosition = transform.position;

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
    
    private void RotateTowardsTarget(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
    public override void OnHit(Enemy hitEnemy)
    {
        RunStatisticsManager.Instance.RecordWeaponDamage(WeaponID, damage);
    }
}