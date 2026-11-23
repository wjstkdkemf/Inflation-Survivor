using TMPro;
using UnityEngine;

public class LoadFailureNotice : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    public void Show(string message)
    {
        messageText.text = message;
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}