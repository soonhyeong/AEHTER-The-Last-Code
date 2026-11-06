using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIInteractionRuntimeGuard
{

    public static void EnsureInteractive(GameObject uiRoot)
    {
        EnsureEventSystem();
        EnsureGraphicRaycasters(uiRoot);
        EnsureCanvasGroups(uiRoot);
    }

    public static void SetCursorVisibleRequest(string owner, bool visible)
    {
        CursorModalBroker.SetLegacyRequest(owner, visible);
    }

    public static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<BaseInputModule>() != null)
            return;

        Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            eventSystem.gameObject.AddComponent(inputSystemModuleType);
            return;
        }

        eventSystem.gameObject.AddComponent<StandaloneInputModule>();
    }

    private static void EnsureGraphicRaycasters(GameObject uiRoot)
    {
        if (uiRoot == null)
            return;

        Canvas[] parentCanvases = uiRoot.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < parentCanvases.Length; i++)
            EnsureGraphicRaycaster(parentCanvases[i]);

        Canvas[] childCanvases = uiRoot.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < childCanvases.Length; i++)
            EnsureGraphicRaycaster(childCanvases[i]);
    }

    private static void EnsureGraphicRaycaster(Canvas canvas)
    {
        if (canvas == null)
            return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private static void EnsureCanvasGroups(GameObject uiRoot)
    {
        if (uiRoot == null)
            return;

        CanvasGroup[] groups = uiRoot.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            if (group == null)
                continue;

            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

}
