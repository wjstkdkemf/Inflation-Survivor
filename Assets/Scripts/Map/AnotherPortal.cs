using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class AnotherPortal : MonoBehaviour
{
    public string MapName;
    private bool isChangingScene = false;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isChangingScene)
        {
            // 2. 코루틴을 시작
            StartCoroutine(ChangeSceneAfterSaving());
        }
    }
    private IEnumerator ChangeSceneAfterSaving()
    {
        if (isChangingScene)
            yield break;
        if (string.IsNullOrEmpty(MapName))
        {
            Debug.LogWarning("[AnotherPortal] MapName is empty. Scene change canceled.");
            yield break;
        }
        // 3. 씬 전환을 시작했다고 플래그를 설정 (중복 방지)
        isChangingScene = true;



        Debug.Log("인벤토리 저장을 시작합니다...");

        // 4. 이전에 만든 저장 함수를 호출해 모든 인벤토리를 저장
        var inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null || !inventoryManager.TrySaveAllInventories("Current.json"))
        {
            isChangingScene = false;

            const string message = "인벤토리를 저장하지 못해 던전에 입장하지 않았습니다.";
            Debug.LogWarning($"[AnotherPortal] {message}");

            if (MenuButtonController.Instance != null)
                MenuButtonController.Instance.ScreenMessage(message);

            yield break;
        }

        // 5. 저장이 완료될 때까지 잠시 대기 (다음 프레임까지)
        // File.WriteAllText는 동기 방식이라 즉시 완료되지만, 안전을 위해 한 프레임 대기합니다.
        yield return null;

        Debug.Log("저장 완료! " + MapName + " 씬으로 전환합니다.");

        // 6. 모든 과정이 끝난 후 씬을 전환
        var loader = LoadingManager.Instance;
        if (loader == null || !loader.TryLoadScene(MapName))
        {
            isChangingScene = false;
            Debug.LogWarning($"[AnotherPortal] 씬 이동 요청이 접수되지 않았습니다: {MapName}");
            yield break;
        }
    }
      
    public void StartButton()
    {
        StartCoroutine(ChangeSceneAfterSaving());
    }
}
