using UnityEngine;

/// <summary>
/// 대사 한 줄. (화자 이름 + 대사 텍스트)
/// DialogueSequence 안에서 배열로 사용된다.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("화자 이름. 비우면 화자 텍스트를 숨긴다.")]
    public string speakerName;

    [TextArea(2, 5)]
    public string text;

    public DialogueLine()
    {
    }

    public DialogueLine(string speakerName, string text)
    {
        this.speakerName = speakerName;
        this.text = text;
    }
}
