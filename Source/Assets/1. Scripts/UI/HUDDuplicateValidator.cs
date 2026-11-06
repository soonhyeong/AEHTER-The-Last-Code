using System.Collections.Generic;
using UnityEngine;

public class HUDDuplicateValidator : MonoBehaviour
{
    [SerializeField] private bool validateOnStart = true;
    [SerializeField] private bool validateEverySceneLoad = true;

    private static readonly string[] RequiredUniqueNames =
    {
        AetherModernHUD.RootName,
        "ObjectiveHUD",
        "CompassHUD",
        "AetherLevelHUD",
        "PlayerStatusHUD",
        "AmmoHUD"
    };

    private void OnEnable()
    {
        if (validateEverySceneLoad)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (validateEverySceneLoad)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        if (validateOnStart)
            Validate();
    }

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Validate();
    }

    public void Validate()
    {
        for (int i = 0; i < RequiredUniqueNames.Length; i++)
            ValidateName(RequiredUniqueNames[i]);
    }

    private static void ValidateName(string objectName)
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        List<string> matches = new List<string>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform target = allTransforms[i];
            if (target != null && target.name == objectName)
                matches.Add(GetPath(target));
        }

        if (matches.Count <= 1)
            return;

        Debug.LogWarning("[HUDDuplicateValidator] Duplicate HUD object '" + objectName + "' found: " + string.Join(" | ", matches));
    }

    private static string GetPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
