using System.Collections.Generic;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance;

    [Header("Grid Settings")]
    [Tooltip("Số ô của flow field. 160x160 với cell diameter 1 bao phủ 80m về mỗi phía.")]
    public Vector2Int gridSize = new Vector2Int(160, 160);
    [Min(0.1f)]
    public float cellRadius = 0.5f;

    [Tooltip("Khoảng cách Player đi khỏi tâm lưới trước khi lưới được đặt lại theo Player.")]
    [Min(1f)] public float recenterDistance = 30f;
    public LayerMask obstacleLayer; // Nhớ tạo Layer "Obstacles" và gán cho các bức tường

    [Header("Target")]
    public Transform playerTransform;

    [SerializeField, Min(0.02f)] private float flowFieldUpdateInterval = 0.1f;
    [SerializeField, Min(1)] private int minimumTargetCellDelta = 2;
    [SerializeField, Min(4)] private int navigationChunkSize = 32;
    [SerializeField] private bool cacheStaticNavigation = true;

    [Header("Editor Debug")]
    [SerializeField] private bool drawFlowFieldGizmos;

    private Cell[,] grid;
    private float cellDiameter;
    private Cell targetCell;
    private Vector2Int gridOriginCell;
    private Vector2Int bufferOrigin;
    private Vector2Int lastSolvedTargetWorldCell;
    private bool hasSolvedTargetCell;
    private bool fieldDirty = true;
    private float nextFlowFieldUpdateTime;
    private Queue<Cell> integrationQueue;
    private HashSet<Cell> visitedCells;
    private readonly Dictionary<Vector2Int, NavigationChunk> navigationChunks = new Dictionary<Vector2Int, NavigationChunk>();
    private int cachedNavigationChunkSize;
    private int cachedObstacleLayerValue;

    private sealed class NavigationChunk
    {
        public readonly byte[] costs;

        public NavigationChunk(int size)
        {
            costs = new byte[size * size];
        }
    }

    private void Awake()
    {
        Instance = this;
        ClampSettings();
        cellDiameter = cellRadius * 2f;
        integrationQueue = new Queue<Cell>(gridSize.x * gridSize.y);
        visitedCells = new HashSet<Cell>();
        cachedNavigationChunkSize = navigationChunkSize;
        cachedObstacleLayerValue = obstacleLayer.value;
        CreateGrid();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            return;
        }

        if (NeedsGridShift(playerTransform.position))
        {
            ShiftGridToPlayer();
        }

        Vector2Int currentTargetWorldCell = WorldToCell(playerTransform.position);
        if (!IsWorldCellInsideGrid(currentTargetWorldCell))
        {
            ShiftGridToPlayer();
            currentTargetWorldCell = WorldToCell(playerTransform.position);
        }

        bool targetCellChanged = !hasSolvedTargetCell || currentTargetWorldCell != lastSolvedTargetWorldCell;
        if (!targetCellChanged && !fieldDirty)
        {
            return;
        }

        if (Time.time < nextFlowFieldUpdateTime)
        {
            return;
        }

        if (hasSolvedTargetCell && GetCellDistance(lastSolvedTargetWorldCell, currentTargetWorldCell) < minimumTargetCellDelta)
        {
            return;
        }

        Cell currentTargetCell = GetCellAtWorldCell(currentTargetWorldCell);
        if (currentTargetCell == null)
        {
            return;
        }

        targetCell = currentTargetCell;
        GenerateIntegrationField(targetCell);
        GenerateFlowField();
        lastSolvedTargetWorldCell = currentTargetWorldCell;
        hasSolvedTargetCell = true;
        fieldDirty = false;
        nextFlowFieldUpdateTime = Time.time + flowFieldUpdateInterval;
    }

    private void CreateGrid()
    {
        EnsureGridBuffer();

        Vector3 worldBottomLeft = transform.position
            - Vector3.right * gridSize.x * 0.5f * cellDiameter
            - Vector3.forward * gridSize.y * 0.5f * cellDiameter;
        gridOriginCell = WorldToCell(worldBottomLeft);
        bufferOrigin = Vector2Int.zero;
        SetTransformToGridCenter();
        RefreshAllCells();
        targetCell = null;
        hasSolvedTargetCell = false;
        fieldDirty = true;
        nextFlowFieldUpdateTime = 0f;
    }

    private void EnsureGridBuffer()
    {
        if (grid != null &&
            grid.GetLength(0) == gridSize.x &&
            grid.GetLength(1) == gridSize.y)
        {
            return;
        }

        grid = new Cell[gridSize.x, gridSize.y];
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                grid[x, y] = new Cell(Vector3.zero, Vector2Int.zero);
            }
        }
    }

    private void RefreshAllCells()
    {
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                RefreshCellAtLogicalIndex(x, y);
            }
        }
    }

    private void RefreshCellAtLogicalIndex(int logicalX, int logicalY)
    {
        Cell cell = GetCellAtLogicalIndex(logicalX, logicalY);
        if (cell == null)
        {
            return;
        }

        Vector2Int worldCell = gridOriginCell + new Vector2Int(logicalX, logicalY);
        cell.worldPos = GetWorldPositionForCell(worldCell);
        cell.gridIndex = new Vector2Int(logicalX, logicalY);
        cell.cost = GetStaticCellCost(worldCell);
        cell.bestCost = ushort.MaxValue;
        cell.bestDirection = Vector3.zero;
    }

    private bool NeedsGridShift(Vector3 playerPosition)
    {
        Vector3 offsetFromCenter = playerPosition - transform.position;
        offsetFromCenter.y = 0f;
        if (offsetFromCenter.sqrMagnitude > recenterDistance * recenterDistance)
        {
            return true;
        }

        return !IsWorldCellInsideGrid(WorldToCell(playerPosition));
    }

    private void ShiftGridToPlayer()
    {
        Vector2Int playerCell = WorldToCell(playerTransform.position);
        Vector2Int desiredOrigin = playerCell - new Vector2Int(gridSize.x / 2, gridSize.y / 2);
        Vector2Int shift = desiredOrigin - gridOriginCell;
        if (shift == Vector2Int.zero)
        {
            return;
        }

        targetCell = null;
        hasSolvedTargetCell = false;
        fieldDirty = true;
        nextFlowFieldUpdateTime = Time.time;

        if (Mathf.Abs(shift.x) >= gridSize.x || Mathf.Abs(shift.y) >= gridSize.y)
        {
            RebuildGridAtOrigin(desiredOrigin);
            return;
        }

        ShiftRingBuffer(shift);
        SetTransformToGridCenter();
    }

    private void RebuildGridAtOrigin(Vector2Int newOrigin)
    {
        EnsureGridBuffer();
        gridOriginCell = newOrigin;
        bufferOrigin = Vector2Int.zero;
        SetTransformToGridCenter();
        RefreshAllCells();
    }

    private void ShiftRingBuffer(Vector2Int shift)
    {
        gridOriginCell += shift;
        bufferOrigin = new Vector2Int(
            PositiveModulo(bufferOrigin.x + shift.x, gridSize.x),
            PositiveModulo(bufferOrigin.y + shift.y, gridSize.y));

        int newColumnStart = 0;
        int newColumnEnd = -1;
        if (shift.x > 0)
        {
            newColumnStart = gridSize.x - shift.x;
            newColumnEnd = gridSize.x - 1;
        }
        else if (shift.x < 0)
        {
            newColumnStart = 0;
            newColumnEnd = -shift.x - 1;
        }

        if (shift.x != 0)
        {
            for (int x = newColumnStart; x <= newColumnEnd; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    RefreshCellAtLogicalIndex(x, y);
                }
            }
        }

        int newRowStart = 0;
        int newRowEnd = -1;
        if (shift.y > 0)
        {
            newRowStart = gridSize.y - shift.y;
            newRowEnd = gridSize.y - 1;
        }
        else if (shift.y < 0)
        {
            newRowStart = 0;
            newRowEnd = -shift.y - 1;
        }

        if (shift.y != 0)
        {
            for (int y = newRowStart; y <= newRowEnd; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    if (shift.x != 0 && x >= newColumnStart && x <= newColumnEnd)
                    {
                        continue;
                    }

                    RefreshCellAtLogicalIndex(x, y);
                }
            }
        }
    }

    private void SetTransformToGridCenter()
    {
        Vector3 position = transform.position;
        position.x = (gridOriginCell.x + gridSize.x * 0.5f) * cellDiameter;
        position.z = (gridOriginCell.y + gridSize.y * 0.5f) * cellDiameter;
        transform.position = position;
    }

    private void GenerateIntegrationField(Cell destinationCell)
    {
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                grid[x, y].bestCost = ushort.MaxValue;
            }
        }

        destinationCell.bestCost = 0;
        integrationQueue.Clear();
        visitedCells.Clear();
        integrationQueue.Enqueue(destinationCell);
        visitedCells.Add(destinationCell);

        while (integrationQueue.Count > 0)
        {
            Cell currentCell = integrationQueue.Dequeue();
            int currentX = currentCell.gridIndex.x;
            int currentY = currentCell.gridIndex.y;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    Cell neighbor = GetCellAtLogicalIndex(currentX + offsetX, currentY + offsetY);
                    if (neighbor == null)
                    {
                        continue;
                    }

                    ushort newCost = (ushort)(neighbor.cost + currentCell.bestCost);
                    if (newCost < neighbor.bestCost)
                    {
                        neighbor.bestCost = newCost;

                        if (!visitedCells.Contains(neighbor))
                        {
                            integrationQueue.Enqueue(neighbor);
                            visitedCells.Add(neighbor);
                        }
                    }
                }
            }
        }
    }

    private void GenerateFlowField()
    {
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Cell cell = GetCellAtLogicalIndex(x, y);
                cell.bestDirection = Vector3.zero;
                ushort bestCost = cell.bestCost;

                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        Cell neighbor = GetCellAtLogicalIndex(x + offsetX, y + offsetY);
                        if (neighbor == null || neighbor.bestCost >= bestCost)
                        {
                            continue;
                        }

                        bestCost = neighbor.bestCost;
                        cell.bestDirection = (neighbor.worldPos - cell.worldPos).normalized;
                    }
                }
            }
        }
    }

    public Cell GetCellFromWorldPos(Vector3 worldPos)
    {
        return GetCellAtWorldCell(WorldToCell(worldPos));
    }

    private Cell GetCellAtWorldCell(Vector2Int worldCell)
    {
        if (!IsWorldCellInsideGrid(worldCell))
        {
            return null;
        }

        return GetCellAtLogicalIndex(
            worldCell.x - gridOriginCell.x,
            worldCell.y - gridOriginCell.y);
    }

    private Cell GetCellAtLogicalIndex(int logicalX, int logicalY)
    {
        if (grid == null || logicalX < 0 || logicalX >= gridSize.x || logicalY < 0 || logicalY >= gridSize.y)
        {
            return null;
        }

        int bufferX = PositiveModulo(bufferOrigin.x + logicalX, gridSize.x);
        int bufferY = PositiveModulo(bufferOrigin.y + logicalY, gridSize.y);
        return grid[bufferX, bufferY];
    }

    private bool IsWorldCellInsideGrid(Vector2Int worldCell)
    {
        int logicalX = worldCell.x - gridOriginCell.x;
        int logicalY = worldCell.y - gridOriginCell.y;
        return logicalX >= 0 && logicalX < gridSize.x && logicalY >= 0 && logicalY < gridSize.y;
    }

    private Vector2Int WorldToCell(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / cellDiameter),
            Mathf.FloorToInt(worldPosition.z / cellDiameter));
    }

    private Vector3 GetWorldPositionForCell(Vector2Int worldCell)
    {
        return new Vector3(
            worldCell.x * cellDiameter + cellRadius,
            transform.position.y,
            worldCell.y * cellDiameter + cellRadius);
    }

    private byte GetStaticCellCost(Vector2Int worldCell)
    {
        if (!cacheStaticNavigation)
        {
            return SampleStaticCellCost(worldCell);
        }

        if (cachedNavigationChunkSize != navigationChunkSize || cachedObstacleLayerValue != obstacleLayer.value)
        {
            navigationChunks.Clear();
            cachedNavigationChunkSize = navigationChunkSize;
            cachedObstacleLayerValue = obstacleLayer.value;
        }

        Vector2Int chunkCoordinate = new Vector2Int(
            FloorDivide(worldCell.x, navigationChunkSize),
            FloorDivide(worldCell.y, navigationChunkSize));
        int localX = worldCell.x - chunkCoordinate.x * navigationChunkSize;
        int localY = worldCell.y - chunkCoordinate.y * navigationChunkSize;
        int localIndex = localX + localY * navigationChunkSize;

        if (!navigationChunks.TryGetValue(chunkCoordinate, out NavigationChunk chunk))
        {
            chunk = new NavigationChunk(navigationChunkSize);
            navigationChunks.Add(chunkCoordinate, chunk);
        }

        if (chunk.costs[localIndex] == 0)
        {
            chunk.costs[localIndex] = SampleStaticCellCost(worldCell);
        }

        return chunk.costs[localIndex];
    }

    private byte SampleStaticCellCost(Vector2Int worldCell)
    {
        Vector3 worldPoint = GetWorldPositionForCell(worldCell);
        return Physics.CheckSphere(worldPoint, cellRadius - 0.1f, obstacleLayer) ? (byte)2 : (byte)1;
    }

    public void InvalidateStaticNavigationCache()
    {
        navigationChunks.Clear();
        cachedNavigationChunkSize = navigationChunkSize;
        cachedObstacleLayerValue = obstacleLayer.value;

        if (grid == null)
        {
            return;
        }

        RebuildGridAtOrigin(gridOriginCell);
        targetCell = null;
        hasSolvedTargetCell = false;
        fieldDirty = true;
        nextFlowFieldUpdateTime = Time.time;
    }

    private static int FloorDivide(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static int GetCellDistance(Vector2Int first, Vector2Int second)
    {
        return Mathf.Max(Mathf.Abs(first.x - second.x), Mathf.Abs(first.y - second.y));
    }

    // Hàm này sẽ vẽ các ô lưới và mũi tên ra màn hình Scene (chỉ hiển thị trong Editor)
    void OnDrawGizmosSelected()
    {
        if (!drawFlowFieldGizmos || grid == null) return;

        foreach (Cell c in grid)
        {
            // Đã sửa lại thành c.cost == 2 để Gizmos nhận diện đúng tường màu Đỏ
            Gizmos.color = (c.cost == 2) ? new Color(1, 0, 0, 0.3f) : new Color(1, 1, 1, 0.1f);
            Gizmos.DrawWireCube(c.worldPos, new Vector3(cellDiameter - 0.1f, 0.1f, cellDiameter - 0.1f));

            // Vẽ mũi tên chỉ đường màu xanh lá
            if (c.bestDirection != Vector3.zero)
            {
                Gizmos.color = Color.green;
                // Vẽ đường thẳng từ tâm ô lưới hướng về phía Player
                Gizmos.DrawLine(c.worldPos, c.worldPos + c.bestDirection * cellRadius);
            }
        }
    }


    private void OnValidate()
    {
        ClampSettings();
    }

    private void ClampSettings()
    {
        gridSize.x = Mathf.Max(1, gridSize.x);
        gridSize.y = Mathf.Max(1, gridSize.y);
        cellRadius = Mathf.Max(0.1f, cellRadius);
        recenterDistance = Mathf.Max(1f, recenterDistance);
        flowFieldUpdateInterval = Mathf.Max(0.02f, flowFieldUpdateInterval);
        minimumTargetCellDelta = Mathf.Max(1, minimumTargetCellDelta);
        navigationChunkSize = Mathf.Max(4, navigationChunkSize);
    }
}
