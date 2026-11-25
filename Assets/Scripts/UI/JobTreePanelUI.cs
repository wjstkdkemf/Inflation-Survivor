using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class JobTreePanelUI : MonoBehaviour
{
    [SerializeField] private Button rootButton;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;

    [SerializeField] private RectTransform nodeRoot;
    [SerializeField] private JobTreeNodeUI nodePrefab;
    [SerializeField] private RectTransform arrowRoot;
    [SerializeField] private Image arrowPrefab;

    private JobDataSO previewedJob;
    private System.Action<JobDataSO> onTargetConfirmed;
    private readonly Dictionary<JobDataSO, JobTreeNodeUI> nodes = new();
    private int characterId;

    private void OnEnable() => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += RefreshLocale;
    private void OnDisable() => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= RefreshLocale;

    private void RefreshLocale(UnityEngine.Localization.Locale locale)
    {
        var discoveries = JobDiscoveryManager.Instance;
        foreach (var pair in nodes)
            pair.Value.RefreshLabel(pair.Key, discoveries != null && discoveries.HasDiscovered(characterId, pair.Key.JobId));
        if (previewedJob != null)
            ShowJobDetails(previewedJob);
    }

    public void Open(CharacterScriptableObject character, System.Action<JobDataSO> onConfirm)
    {
        previewedJob = null;
        onTargetConfirmed = onConfirm;
        detailPanel.SetActive(false);
        foreach (var node in nodes.Values)
        {
            node.gameObject.SetActive(false);
            Destroy(node.gameObject);
        }
        nodes.Clear();
        foreach (Transform arrow in arrowRoot)
        {
            arrow.gameObject.SetActive(false);
            Destroy(arrow.gameObject);
        }
        detailPanel.SetActive(false);

        if (character == null || character.JobTreeRoot == null)
        {
            Close();
            return;
        }

        characterId = character.Id;
        CreateBranch(character.JobTreeRoot, characterId);
        DrawConnections();
        gameObject.SetActive(true);
    }
    public void ConfirmTarget()
    {
        if (previewedJob == null || onTargetConfirmed == null)
            return;

        onTargetConfirmed(previewedJob);
        Close();
    }

    private void CreateBranch(JobDataSO job, int characterId)
    {
        if (job == null || nodes.ContainsKey(job))
            return;

        var node = Instantiate(nodePrefab, nodeRoot);
        nodes.Add(job, node);

        var rect = (RectTransform)node.transform;
        rect.anchoredPosition = job.TreePosition;

        var discoveries = JobDiscoveryManager.Instance;
        bool discovered = discoveries != null &&
                        discoveries.HasDiscovered(characterId, job.JobId);

        node.Bind(job, discovered, ShowJobDetails);

        if (job.nextAbleJobs == null)
            return;

        foreach (var nextJob in job.nextAbleJobs)
            CreateBranch(nextJob, characterId);
    }
    private void ShowJobDetails(JobDataSO job)
    {
        previewedJob = job;
        detailName.text = job.LocalizedName;
        detailDescription.text = job.LocalizedDescription;
        detailPanel.SetActive(true);
    }
    private void DrawConnections()
    {
        foreach (var pair in nodes)
        {
            var children = pair.Key.nextAbleJobs;
            if (children == null) continue;

            var from = (RectTransform)pair.Value.transform;
            foreach (var child in children)
            {
                if (child == null || !nodes.TryGetValue(child, out var childNode))
                    continue;

                var to = (RectTransform)childNode.transform;
                Vector2 delta = to.anchoredPosition - from.anchoredPosition;
                if (delta.sqrMagnitude < 0.001f) continue;

                Vector2 dir = delta.normalized;
                float fromEdge = DistanceToEdge(from, dir);
                float toEdge = DistanceToEdge(to, dir);
                float length = delta.magnitude - fromEdge - toEdge;
                if (length <= 0f) continue;

                var arrow = Instantiate(arrowPrefab, arrowRoot);
                arrow.raycastTarget = false;

                var rect = arrow.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = from.anchoredPosition + dir * fromEdge;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, length);
                rect.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }
    }

    private static float DistanceToEdge(RectTransform node, Vector2 dir)
    {
        Vector2 half = node.rect.size * 0.5f;
        float x = Mathf.Abs(dir.x);
        float y = Mathf.Abs(dir.y);
        return Mathf.Min(x > 0f ? half.x / x : float.PositiveInfinity,
                        y > 0f ? half.y / y : float.PositiveInfinity);
    }

    public void Close() => gameObject.SetActive(false);
}
