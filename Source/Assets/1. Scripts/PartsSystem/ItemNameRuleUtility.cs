using UnityEngine;

public static class ItemNameRuleUtility
{
    public static string NormalizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        return rawName
            .Replace("(Clone)", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    public static bool IsModuleItem(string name)
    {
        return NormalizeName(name).Contains("_module");
    }

    public static bool IsPaperObject(string name)
    {
        return NormalizeName(name).Contains("_paper");
    }

    public static bool IsModuleObject(GameObject obj, ItemData itemData)
    {
        return IsModuleItem(GetBestItemName(obj, itemData))
            || (obj != null && IsModuleItem(obj.name));
    }

    public static bool IsPaperObject(GameObject obj, ItemData itemData)
    {
        return IsPaperObject(GetBestItemName(obj, itemData))
            || (obj != null && IsPaperObject(obj.name));
    }

    public static string GetBestItemName(GameObject obj, ItemData itemData)
    {
        if (itemData != null)
        {
            if (!string.IsNullOrWhiteSpace(itemData.id))
                return itemData.id;

            if (!string.IsNullOrWhiteSpace(itemData.displayName))
                return itemData.displayName;
        }

        return obj != null ? obj.name : string.Empty;
    }

    public static string GetPickupDisplayName(GameObject obj, ItemData itemData)
    {
        string normalized = NormalizeName(GetBestItemName(obj, itemData));

        if (normalized.Contains("cable"))
            return "Cable";

        if (normalized.Contains("gear"))
            return "Gear";

        if (normalized.Contains("crank"))
            return "Crank";

        if (normalized.Contains("battery"))
            return "Battery";

        if (IsPaperObject(normalized))
            return "Paper";

        if (itemData != null && !string.IsNullOrWhiteSpace(itemData.displayName))
            return itemData.displayName;

        string rawName = obj != null ? obj.name.Replace("(Clone)", string.Empty).Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(rawName))
            return "Item";

        return rawName
            .Replace("_module", string.Empty)
            .Replace("_paper", string.Empty)
            .Replace("_", " ")
            .Trim();
    }

    public static string GetBestItemId(ItemData itemData)
    {
        if (itemData == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(itemData.id))
            return NormalizeName(itemData.id);

        return NormalizeName(itemData.displayName);
    }

    public static bool ItemMatchesId(ItemData itemData, string itemId)
    {
        return NormalizeName(itemId) == GetBestItemId(itemData);
    }
}
