#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ThirdZoneSceneSetupAutoRun
{
    private const string SessionKey = "AETHER_THIRD_ZONE_SETUP_AUTORUN_DONE";
    private const string DoneMarkerPath = "Temp/ThirdZoneSetupAutoRun.done";
    // Default-deny, per Editor session. The existing manual setup menu remains available.
    private const string OptInSessionKey = "AETHER_THIRD_ZONE_SETUP_AUTORUN_EXPLICIT_SESSION_OPT_IN";

    static ThirdZoneSceneSetupAutoRun()
    {
        if (SessionState.GetBool(OptInSessionKey, false))
            EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (!SessionState.GetBool(OptInSessionKey, false))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                return;
        if (EditorPrefs.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunOnce;
            return;
        }

        EditorPrefs.SetBool(SessionKey, true);

        try
        {
            ThirdZoneSceneSetupEditor.SetupThirdZoneCombat();
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DoneMarkerPath, "Third zone combat setup completed.");
            Debug.Log("[ThirdZoneSetupAutoRun] Completed scene placement for 11.3구역_찐.");
        }
        catch (System.Exception exception)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(DoneMarkerPath, "FAILED: " + exception);
            Debug.LogError("[ThirdZoneSetupAutoRun] Failed: " + exception);
        }
    }
}
#endif
