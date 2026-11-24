using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class JobTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    public void Bind(JobDataSO job, bool discovered, System.Action<JobDataSO> onSelected)
    {
        label.text = discovered ? job.jobName : "??";
        button.interactable = discovered;
        button.onClick.AddListener(() => onSelected(job));
    }
}