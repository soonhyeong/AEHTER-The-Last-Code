using UnityEngine;

public class TutorialCompleteUI : MonoBehaviour
{
    [Header("튜토리얼 완료 후 활성화할 UI들")]
    public GameObject[] uiToActivate;

    // TutorialController에서 호출할 함수
    public void OnTutorialCompleted()
    {
        foreach (var ui in uiToActivate)
        {
            if (ui != null)
                ui.SetActive(true);
        }
    }
}
