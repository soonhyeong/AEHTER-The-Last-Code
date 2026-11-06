using System.Collections;
using TMPro;
using UnityEngine;

public class AetherTyping : MonoBehaviour
{
    public TextMeshProUGUI textUI;
    public float typingSpeed = 0.05f;

    public void StartTyping(string message)
    {
        StopAllCoroutines();
        StartCoroutine(TypeText(message));
    }

    IEnumerator TypeText(string message)
    {
        textUI.text = "";

        foreach (char c in message)
        {
            textUI.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}