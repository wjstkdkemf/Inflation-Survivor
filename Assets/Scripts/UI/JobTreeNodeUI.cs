using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class JobTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public void Bind(JobDataSO job, bool discovered, System.Action<JobDataSO> onSelected)
    {
        RefreshLabel(job, discovered);
        button.interactable = discovered;
        button.onClick.AddListener(() => onSelected(job));
    }

    public void RefreshLabel(JobDataSO job, bool discovered)
    {
        label.text = discovered ? job.LocalizedName : UpgradeLocalization.GetUI("job.unknown", "??");
    }
}
