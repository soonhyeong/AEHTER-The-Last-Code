using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TileWeight
{
    public Tile tilePrefab;
    public float weight = 1f;
}

public class TileSpawner : MonoBehaviour
{
    [Header("Grid 설정")]
    public int width = 5;
    public int height = 5;

    [Header("SpawnPoint (TileSpawn 부모)")]
    public Transform spawnRoot;

    [Header("Start / End 타일 프리팹")]
    public Tile startPrefab;
    public Tile endPrefab;

    [Header("일반 타일 (가중치 랜덤)")]
    public TileWeight[] weightedTiles;

    private Transform[] spawnPoints;
    private TileManager tileManager;

    void Awake()
    {
        tileManager = GetComponent<TileManager>(); // 같은 오브젝트에 붙어 있음
        CacheSpawnPoints();
    }

    private bool CacheSpawnPoints()
    {
        if (spawnRoot == null || width <= 0 || height <= 0)
            return false;

        List<Transform> list = new List<Transform>();
        foreach (Transform child in spawnRoot)
            list.Add(child);
        spawnPoints = list.ToArray();

        if ((long)width * height != spawnPoints.Length || spawnPoints.Length < 2)
        {
            Debug.LogWarning(
                $"TileSpawner: spawnPoints 개수({spawnPoints.Length})가 width*height({width * height})와 다릅니다."
            );
            return false;
        }
        return true;
    }

    // GameManager에서만 호출
    public void RegenerateTiles()
    {
        TryRegenerateTiles();
    }

    public bool TryRegenerateTiles()
    {
        if (tileManager == null)
            tileManager = GetComponent<TileManager>();
        if (tileManager == null || !CacheSpawnPoints() || startPrefab == null || endPrefab == null
            || spawnPoints.Length > 2 && !HasValidWeightedCatalog())
        {
            Debug.LogError("TileSpawner: invalid grid, spawn references or weighted catalog; existing board retained.");
            return false;
        }
        Tile[] definitions = null;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Tile[] candidate = new Tile[spawnPoints.Length];
            candidate[0] = startPrefab;
            candidate[candidate.Length - 1] = endPrefab;
            for (int i = 1; i < candidate.Length - 1; i++) candidate[i] = GetWeightedRandomPrefab();
            if (TileManager.HasRotationSolution(candidate, width, height))
            {
                definitions = candidate;
                break;
            }
        }
        if (definitions == null)
        {
            Debug.LogError("TileSpawner: no solvable board within bounded generation attempts; existing board retained.");
            return false;
        }
        ClearSpawnedTiles();
        // Destroy is deferred. Publish only this generation's ordered instances,
        // never scan a hierarchy that can still contain the previous board.
        tileManager.tileList = SpawnAllTiles(definitions);
        tileManager.width = width;
        tileManager.height = height;

        if (!EnsureUnsolvedStart())
        {
            ClearSpawnedTiles();
            tileManager.tileList = new Tile[0];
            Debug.LogError("TileSpawner: every tested scramble remains solved; refusing auto-clear board.");
            return false;
        }
        tileManager.UpdateConnections();
        return true;
    }

    private bool EnsureUnsolvedStart()
    {
        if (!tileManager.HasConnectedPath()) return true;
        // Keep random rotations. Only an accidentally solved start needs repair.
        foreach (Tile tile in tileManager.tileList)
        {
            if (tile == null || tile.kind != TileKind.Normal) continue;
            int original = tile.rotation;
            for (int turn = 1; turn < 4; turn++)
            {
                tile.rotation = (original + turn * 90) % 360;
                tile.ApplyRotation();
                if (!tileManager.HasConnectedPath()) return true;
            }
            tile.rotation = original;
            tile.ApplyRotation();
        }
        return false;
    }

    private Tile[] SpawnAllTiles(Tile[] definitions)
    {
        int count = spawnPoints.Length;
        Tile[] tiles = new Tile[count];

        for (int i = 0; i < count; i++)
        {
            Transform sp = spawnPoints[i];
            Tile prefab = definitions[i];

            if (prefab == null)
                continue;

            Tile tile = Instantiate(prefab, sp);
            tiles[i] = tile;
            tile.transform.localPosition = Vector3.zero;

            if (tile.kind == TileKind.Normal)
            {
                int rot = Random.Range(0, 4) * 90;
                tile.rotation = rot;
                tile.ApplyRotation();
            }
        }
        return tiles;
    }

    private void ClearSpawnedTiles()
    {
        foreach (Transform sp in spawnPoints)
        {
            for (int i = sp.childCount - 1; i >= 0; i--)
            {
                sp.GetChild(i).gameObject.SetActive(false);
                Destroy(sp.GetChild(i).gameObject);
            }
        }
    }

    private bool HasValidWeightedCatalog()
    {
        if (weightedTiles == null) return false;
        float total = 0f;
        foreach (var entry in weightedTiles)
            if (entry != null && entry.tilePrefab != null && entry.weight > 0f
                && !float.IsNaN(entry.weight) && !float.IsInfinity(entry.weight))
                total += entry.weight;
        return total > 0f && !float.IsInfinity(total);
    }

    private Tile GetWeightedRandomPrefab()
    {
        if (weightedTiles == null || weightedTiles.Length == 0)
        {
            Debug.LogError("TileSpawner: weightedTiles가 비어 있습니다.");
            return null;
        }

        float total = 0f;
        foreach (var wt in weightedTiles)
            if (wt != null && wt.tilePrefab != null && wt.weight > 0f
                && !float.IsNaN(wt.weight) && !float.IsInfinity(wt.weight))
                total += wt.weight;

        if (total <= 0f)
        {
            Debug.LogError("TileSpawner: 모든 weight가 0 이하입니다.");
            return null;
        }

        float rand = Random.value * total;
        float cur = 0f;
        Tile lastValid = null;

        foreach (var wt in weightedTiles)
        {
            if (wt == null || wt.tilePrefab == null || wt.weight <= 0f
                || float.IsNaN(wt.weight) || float.IsInfinity(wt.weight))
                continue;

            cur += wt.weight;
            lastValid = wt.tilePrefab;
            if (rand <= cur)
                return wt.tilePrefab;
        }

        return lastValid;
    }
}
