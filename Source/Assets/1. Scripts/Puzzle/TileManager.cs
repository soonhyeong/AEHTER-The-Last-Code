using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [Header("Grid 설정")]
    public int width = 5;
    public int height = 5;

    [Header("타일 참조 (Spawn 후 Spawner가 채움)")]
    public Tile[] tileList;

    private Tile[,] tiles2D;

    // Tile에서 회전이 일어날 때 호출
    public void OnTileRotated(Tile tile)
    {
        UpdateConnections();
    }

    public void UpdateConnections()
    {
        // tileList is public and is replaced on each generation. Dimensions alone
        // cannot prove the cached array still refers to the current live tiles.
        Build2DArray();

        if (tiles2D == null)
            return;

        // 모든 타일 연결 비주얼 초기화
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (tiles2D[x, y] != null)
                    tiles2D[x, y].ResetConnectionVisual();
            }
        }

        // Start / End 위치 찾기
        if (!TryGetStartEnd(out Vector2Int startPos, out Vector2Int endPos))
            return;

        // BFS로 경로 찾기
        if (!TryFindPath(startPos, endPos, out List<Vector2Int> path))
            return;

        // 인접 좌표 쌍마다 해당 방향 세그먼트만 연결 표시
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2Int cur = path[i];
            Vector2Int next = path[i + 1];

            Vector2Int delta = next - cur;

            Direction dirFromCurToNext;
            if (delta == new Vector2Int(0, -1))      dirFromCurToNext = Direction.Up;
            else if (delta == new Vector2Int(1, 0))  dirFromCurToNext = Direction.Right;
            else if (delta == new Vector2Int(0, 1))  dirFromCurToNext = Direction.Down;
            else if (delta == new Vector2Int(-1, 0)) dirFromCurToNext = Direction.Left;
            else
                continue;

            Direction dirFromNextToCur = GetOpposite(dirFromCurToNext);

            Tile curTile = tiles2D[cur.x, cur.y];
            Tile nextTile = tiles2D[next.x, next.y];

            if (curTile != null)
                curTile.SetSegmentConnected(dirFromCurToNext, true);

            if (nextTile != null)
                nextTile.SetSegmentConnected(dirFromNextToCur, true);
        }

        var gm = FindObjectOfType<PuzzleManager>();
        if (gm != null)
            gm.OnPuzzleCleared();
    }

    #region 내부 유틸

    internal bool HasConnectedPath()
    {
        Build2DArray();
        return tiles2D != null && TryGetStartEnd(out Vector2Int start, out Vector2Int end)
            && TryFindPath(start, end, out _);
    }

    // Generation validation only. A simple path assigns each normal tile one
    // consistent rotation; repeated visits cannot promise contradictory ports.
    internal static bool HasRotationSolution(Tile[] grid, int columns, int rows)
    {
        if (columns <= 0 || rows <= 0 || grid == null || (long)columns * rows != grid.Length
            || grid.Length < 3 || grid.Length > 1024 || grid[0] == null || grid[grid.Length - 1] == null
            || grid[0].kind != TileKind.Start || grid[grid.Length - 1].kind != TileKind.End)
            return false;
        for (int i = 0; i < grid.Length; i++)
            if (grid[i] == null || grid[i].basePorts == null || grid[i].basePorts.Length != 4
                || i > 0 && i < grid.Length - 1 && grid[i].kind != TileKind.Normal)
                return false;
        bool[] visited = new bool[grid.Length];
        int budget = 4096;
        return SearchRotationPath(grid, columns, 0, -1, visited, ref budget);
    }

    private static bool SearchRotationPath(Tile[] grid, int columns, int index, int entry,
        bool[] visited, ref int budget)
    {
        if (--budget < 0 || visited[index]) return false;
        Tile tile = grid[index];
        if (index == grid.Length - 1)
            return entry >= 0 && tile.HasPort((Direction)entry);
        visited[index] = true;
        for (int exit = 0; exit < 4; exit++)
        {
            if (exit == entry || !CanUseRotation(tile, entry, exit)) continue;
            int x = index % columns, y = index / columns;
            int nx = x + (exit == 1 ? 1 : exit == 3 ? -1 : 0);
            int ny = y + (exit == 2 ? 1 : exit == 0 ? -1 : 0);
            if (nx < 0 || nx >= columns || ny < 0 || ny >= grid.Length / columns) continue;
            int next = ny * columns + nx;
            if (!visited[next] && SearchRotationPath(grid, columns, next, (exit + 2) % 4, visited, ref budget))
            {
                visited[index] = false;
                return true;
            }
        }
        visited[index] = false;
        return false;
    }

    private static bool CanUseRotation(Tile tile, int entry, int exit)
    {
        if (tile.kind != TileKind.Normal)
            return tile.HasPort((Direction)exit) && (entry < 0 || tile.HasPort((Direction)entry));
        for (int rotation = 0; rotation < 4; rotation++)
            if (tile.basePorts[(exit - rotation + 4) % 4]
                && (entry < 0 || tile.basePorts[(entry - rotation + 4) % 4]))
                return true;
        return false;
    }

    private void Build2DArray()
    {
        if (width <= 0 || height <= 0 || tileList == null || tileList.Length == 0
            || (long)width * height != tileList.Length)
        {
            tiles2D = null;
            return;
        }

        tiles2D = new Tile[width, height];

        for (int i = 0; i < tileList.Length; i++)
        {
            int x = i % width;
            int y = i / width;

            if (x < width && y < height)
                tiles2D[x, y] = tileList[i];
        }
    }

    private bool TryGetStartEnd(out Vector2Int startPos, out Vector2Int endPos)
    {
        startPos = new Vector2Int(-1, -1);
        endPos   = new Vector2Int(-1, -1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Tile t = tiles2D[x, y];
                if (t == null) continue;

                if (t.kind == TileKind.Start)
                    startPos = new Vector2Int(x, y);
                else if (t.kind == TileKind.End)
                    endPos = new Vector2Int(x, y);
            }
        }

        if (startPos.x < 0 || endPos.x < 0)
        {
            Debug.LogWarning("TileManager: Start 또는 End 타일을 찾지 못했습니다.");
            return false;
        }

        return true;
    }

    private bool TryFindPath(Vector2Int startPos, Vector2Int endPos, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

        bool[,] visited = new bool[width, height];
        Vector2Int[,] parent = new Vector2Int[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                parent[x, y] = new Vector2Int(-1, -1);

        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(startPos);
        visited[startPos.x, startPos.y] = true;

        Direction[] dirs =
        {
            Direction.Up, Direction.Right, Direction.Down, Direction.Left
        };
        Vector2Int[] offsets =
        {
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0)
        };

        bool found = false;

        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            if (cur == endPos)
            {
                found = true;
                break;
            }

            Tile curTile = tiles2D[cur.x, cur.y];
            if (curTile == null) continue;

            for (int i = 0; i < 4; i++)
            {
                Direction dir = dirs[i];
                Vector2Int offset = offsets[i];

                int nx = cur.x + offset.x;
                int ny = cur.y + offset.y;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                if (visited[nx, ny])
                    continue;

                Tile nextTile = tiles2D[nx, ny];
                if (nextTile == null)
                    continue;

                if (!curTile.HasPort(dir))
                    continue;

                Direction opposite = GetOpposite(dir);
                if (!nextTile.HasPort(opposite))
                    continue;

                visited[nx, ny] = true;
                parent[nx, ny] = cur;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }

        if (!found)
            return false;

        Vector2Int p = endPos;
        while (p.x >= 0 && p.y >= 0)
        {
            path.Add(p);
            if (p == startPos)
                break;

            p = parent[p.x, p.y];
        }

        path.Reverse();
        return true;
    }

    private Direction GetOpposite(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up:    return Direction.Down;
            case Direction.Down:  return Direction.Up;
            case Direction.Left:  return Direction.Right;
            case Direction.Right: return Direction.Left;
        }
        return dir;
    }

    #endregion
}
