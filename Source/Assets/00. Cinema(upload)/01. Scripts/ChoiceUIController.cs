using UnityEngine;

public class ChoiceUIController : MonoBehaviour
{
    public GameObject choiceCanvas;

    public void ShowChoiceUI()
    {
        if (choiceCanvas != null)
            choiceCanvas.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (choiceCanvas != null && choiceCanvas.activeSelf)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}