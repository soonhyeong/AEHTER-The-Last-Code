using UnityEngine;

/// <summary>
/// 여러 DialogueLine을 담는 대화 데이터(ScriptableObject).
///
/// 기존에는 대사가 Timeline 시그널 인자에 문자열로 박혀 있었지만(Control Room 씬),
/// 이 에셋을 쓰면 대사를 데이터로 분리해 관리할 수 있다.
///
/// 생성: Project 창 우클릭 → Create → Game → Dialogue Sequence
/// </summary>
[CreateAssetMenu(menuName = "Game/Dialogue Sequence", fileName = "NewDialogueSequence")]
public class DialogueSequence : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("로그/디버깅용 식별자 (선택)")]
    public string sequenceId;

    [Header("대사 (위에서 아래 순서로 재생)")]
    public DialogueLine[] lines;

    [Header("종료 처리")]
    [Tooltip("마지막 대사가 끝난 뒤 O/X 선택 UI를 띄울지 여부")]
    public bool showChoiceAtEnd = false;

    [Header("사운드 (AudioManager 문자열 key)")]
    [Tooltip("대화 시작 시 재생할 BGM key. 비우면 BGM을 바꾸지 않는다.")]
    public string bgmKey = "";

    [Tooltip("대사를 넘길 때 재생할 SFX key. 비우면 무음.")]
    public string advanceSfxKey = "btn_Click";

    public int LineCount
    {
        get { return lines != null ? lines.Length : 0; }
    }

    /// <summary>
    /// index 위치의 대사를 가져온다. 범위를 벗어나거나 비어 있으면 false.
    /// </summary>
    public bool TryGetLine(int index, out DialogueLine line)
    {
        if (lines == null || index < 0 || index >= lines.Length)
        {
            line = null;
            return false;
        }

        line = lines[index];
        return line != null;
    }
}
