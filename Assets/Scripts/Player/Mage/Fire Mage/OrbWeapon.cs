using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbWeapon : WeaponBase // 부모 클래스 변경
{
    private int currentOrbCount;
    private float currentRadius;
    private float currentRotationSpeed;
    private float currentScaling = 1.0f;

    private GameObject orbPrefab; // 작은 구체 프리팹
    protected List<GameObject> spawnedOrbs = new List<GameObject>();
    private float angleStep;

    private PlayerStatsCalculate attackEventSource;

    protected virtual void OnEnable()
    {
        if (!IsInitialized)
            return;

        UnsubscribeAttackChanged();

        attackEventSource = PlayerStatsCalculate.Instance;
        if (attackEventSource == null)
        {
            Debug.LogError("PlayerStatsCalculate is missing.", this);
            return;
        }

        attackEventSource.AttackChanged += RefreshDamage;
        RefreshDamage();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeAttackChanged();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeAttackChanged();
    }

    private void UnsubscribeAttackChanged()
    {
        if (attackEventSource != null)
            attackEventSource.AttackChanged -= RefreshDamage;

        attackEventSource = null;
    }
    protected override bool InitializeWeapon(WeaponDataSO data)
    {
        if (data is OrbWeaponDataSO orbData)
        {
            if (orbData.orbProjectilePrefab == null ||
                !orbData.orbProjectilePrefab.TryGetComponent<Orb>(out _))
            {
                Debug.LogError("Orb weapon requires an Orb prefab.", this);
                return false;
            }
            currentRadius = orbData.radius;
            currentRotationSpeed = orbData.rotationSpeed;
            currentDamage = orbData.baseDamage; // 부모 SO에 있는 데미지

            currentHitRadius = orbData.hitRadius;

            attackMotion = orbData.attackMotion;

            this.WeaponID = orbData.WeaponId;

            orbPrefab = orbData.orbProjectilePrefab;
        }
        else
        {
            Debug.LogError("잘못된 데이터가 들어왔습니다! OrbWeaponDataSO가 필요합니다.");
            return false;
        }

        return TryReplaceOrbs(orbData.orbCount);
    }
    private bool TryReplaceOrbs(int targetCount)
    {
        if (targetCount < 0 || orbPrefab == null ||
            !orbPrefab.TryGetComponent<Orb>(out _))
            return false;

        var pendingOrbs = new List<GameObject>(targetCount);
        float nextAngleStep = targetCount > 0 ? 360f / targetCount : 0f;
        var stagingRoot = new GameObject("OrbStaging");
        stagingRoot.SetActive(false);
        stagingRoot.transform.SetParent(transform, false);

        try
        {
            for (int i = 0; i < targetCount; i++)
            {
                float angle = i * nextAngleStep + Time.time * currentRotationSpeed;
                GameObject orb = Instantiate(orbPrefab, GetOrbPosition(angle),Quaternion.identity, stagingRoot.transform);

                orb.SetActive(false);
                SetupSpawnedOrb(orb);
                pendingOrbs.Add(orb);
            }
        }
        catch (System.Exception exception)
        {
            Destroy(stagingRoot);
            Debug.LogException(exception, this);
            return false;
        }

        foreach (GameObject orb in spawnedOrbs)
        {
            if (orb != null)
                orb.SetActive(false);
        }
        ClearOrbs();

        spawnedOrbs = pendingOrbs;
        currentOrbCount = targetCount;
        angleStep = nextAngleStep;

        foreach (GameObject orb in spawnedOrbs)
            orb.transform.SetParent(transform, true);

        Destroy(stagingRoot);

        foreach (GameObject orb in spawnedOrbs)
            orb.SetActive(true);

        return true;
    }

    // 플레이어 컨트롤러가 매 프레임 호출해줌
    public override void OnUpdate()
    {
        OrbitOrbs();
    }
    Vector3 GetOrbPosition(float angle)
    {
        float angleInRadians = angle * Mathf.Deg2Rad;
        float x = Mathf.Cos(angleInRadians) * currentRadius;
        float y = Mathf.Sin(angleInRadians) * currentRadius;

        return new Vector3(x, y, 0) + transform.position;
    }
    protected virtual void SetupSpawnedOrb(GameObject orb)
    {
        if (orb.TryGetComponent<Orb>(out var orbScript))
        {
            orbScript.SetInfo(GetDamage(), currentHitRadius, 0 , WeaponID);
        }
    }
    void OrbitOrbs()
    {
       for (int i = 0; i < spawnedOrbs.Count; i++)

        {
            if (spawnedOrbs[i] == null) continue;

            float angle = (i * angleStep) + Time.time * currentRotationSpeed;

            spawnedOrbs[i].transform.position = GetOrbPosition(angle);

        }
    }

    void ClearOrbs()
    {
        foreach (GameObject orb in spawnedOrbs)
        {
            if (orb != null) Destroy(orb);
        }
        spawnedOrbs.Clear();
    }
    public void UpgradeRadius(float amount)
    {
        currentRadius += amount;
    }

    public void UpgradeSpeed(float amount)
    {
        currentRotationSpeed += amount;
    }
    protected override bool RecalculateLevelStats()
    {
        if (!(myData is OrbWeaponDataSO data)) return false;
        int targetCount = CalculateCount(SkillStatType.ProjectileCount, data.orbCount, CurrentLevel);

        return TryReplaceOrbs(targetCount);
    }

    public override UpgradePreviewData GetUpgradePreview(UpgradeScriptableObject upgrade)
    {
        UpgradePreviewData preview = base.GetUpgradePreview(upgrade);
        if (!(myData is OrbWeaponDataSO data)) return preview;

        preview.Lines.Add(new UpgradePreviewLine(
            "upgrade.stat.orb_count",
            currentOrbCount.ToString(),
            CalculateCount(SkillStatType.ProjectileCount, data.orbCount, CurrentLevel + 1).ToString()
        ));

        preview.Lines.Add(new UpgradePreviewLine(
            "upgrade.stat.orbit_area",
            currentRadius.ToString("0.##"),
            currentRadius.ToString("0.##")
        ));

        return preview;
    }
    public long GetDamage()
    {
        return CalculateDamage(currentDamage, currentScaling);
    }
    protected override void RefreshDamage()
    {
        long damage = GetDamage();

        foreach (GameObject orb in spawnedOrbs)
        {
            if (orb != null && orb.TryGetComponent<Orb>(out var orbScript))
                orbScript.damage = damage;
        }
    }
}
