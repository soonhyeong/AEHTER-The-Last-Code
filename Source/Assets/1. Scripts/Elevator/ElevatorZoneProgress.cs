using UnityEngine;

public static class ElevatorZoneProgress
{
    private const string Zone1ClearedKey = "AETHER_ZONE_1_CLEARED";
    private const string Zone2ClearedKey = "AETHER_ZONE_2_CLEARED";

    public static bool IsZoneUnlocked(int zoneNumber)
    {
        switch (zoneNumber)
        {
            case 1:
                return true;
            case 2:
                return IsZoneCleared(1);
            case 3:
                return IsZoneCleared(2);
            default:
                return false;
        }
    }

    public static bool IsZoneCleared(int zoneNumber)
    {
        switch (zoneNumber)
        {
            case 1:
                return PlayerPrefs.GetInt(Zone1ClearedKey, 0) == 1;
            case 2:
                return PlayerPrefs.GetInt(Zone2ClearedKey, 0) == 1;
            default:
                return false;
        }
    }

    public static void MarkZoneCleared(int zoneNumber)
    {
        switch (zoneNumber)
        {
            case 1:
                PlayerPrefs.SetInt(Zone1ClearedKey, 1);
                break;
            case 2:
                PlayerPrefs.SetInt(Zone2ClearedKey, 1);
                break;
            default:
                Debug.LogWarning($"[ElevatorZoneProgress] Unsupported zone clear request: {zoneNumber}");
                return;
        }

        PlayerPrefs.Save();
        Debug.Log($"[ElevatorZoneProgress] Zone {zoneNumber} cleared.");
    }

    public static void ResetProgressForDebug()
    {
        PlayerPrefs.DeleteKey(Zone1ClearedKey);
        PlayerPrefs.DeleteKey(Zone2ClearedKey);
        PlayerPrefs.Save();
    }
}
