using System.Collections.Generic;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance;

    [Header("Grid Settings")]
    [Tooltip("160x160 với cell diameter 1 bao phủ 80m về mỗi phía.")]
    public Vector2Int gridSize = new Vector2Int(160, 160);
    [Min(0.1f)]
    public float cellRadius = 0.5f;
    [Tooltip("Khoảng cách Player đi khỏi tâm lưới trước khi lưới được đặt lại.")]
    [Min(1f)] public float recenterDistance = 30f;
    public LayerMask obstacleLayer; // Nhớ tạo Layer "Obstacles" và gán cho các bức tường

    [Header("Target")]
    public Transform playerTransform;

    private Cell[,] grid;
    private float cellDiameter;
    private Cell targetCell;

    void Awake()
    {
        Instance = this;
        ClampSettings();
        cellDiameter = cellRadius * 2f;
        CreateGrid();
    }

    void Update()
    {
        if (playerTransform == null) return;

        Vector3 offsetFromCenter = playerTransform.position - transform.position;
        offsetFromCenter.y = 0f;
        if (offsetFromCenter.sqrMagnitude > recenterDistance * recenterDistance)
        {
            transform.position = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);

            CreateGrid();
            targetCell = null; 
        }

        Cell currentTargetCell = GetCellFromWorldPos(playerTransform.position);

        // Chỉ cập nhật lại các mũi tên khi Player bước sang ô đất khác
        if (currentTargetCell != null && targetCell != currentTargetCell)
        {
            targetCell = currentTargetCell;
            GenerateIntegrationField(targetCell);
            GenerateFlowField();
        }
    }

    void CreateGrid()
    {
        grid = new Cell[gridSize.x, gridSize.y];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridSize.x / 2 * cellDiameter - Vector3.forward * gridSize.y / 2 * cellDiameter;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * cellDiameter + cellRadius) + Vector3.forward * (y * cellDiameter + cellRadius);
                grid[x, y] = new Cell(worldPoint, new Vector2Int(x, y));

                // Quét tường: Nếu là tường, gán cost = 2 để quái vật ưu tiên leo thẳng qua thay vì đi vòng
                if (Physics.CheckSphere(worldPoint, cellRadius - 0.1f, obstacleLayer))
                {
                    grid[x, y].cost = 2;
                }
            }
        }
    }

    void GenerateIntegrationField(Cell destinationCell)
    {
        foreach (Cell c in grid) c.bestCost = ushort.MaxValue;

        destinationCell.bestCost = 0;

        Queue<Cell> cellsToCheck = new Queue<Cell>();
        cellsToCheck.Enqueue(destinationCell);

        // VÒNG LẶP BFS ĐÃ ĐƯỢC KHÔI PHỤC
        while (cellsToCheck.Count > 0)
        {
            Cell currentCell = cellsToCheck.Dequeue();
            List<Cell> neighbors = GetNeighborCells(currentCell.gridIndex);

            foreach (Cell neighbor in neighbors)
            {
                if (neighbor.cost + currentCell.bestCost < neighbor.bestCost)
                {
                    neighbor.bestCost = (ushort)(neighbor.cost + currentCell.bestCost);
                    cellsToCheck.Enqueue(neighbor);
                }
            }
        }
    }

    void GenerateFlowField()
    {
        foreach (Cell c in grid)
        {
            c.bestDirection = Vector3.zero;
            ushort bestCost = c.bestCost;
            List<Cell> neighbors = GetNeighborCells(c.gridIndex);

            foreach (Cell neighbor in neighbors)
            {
                if (neighbor.bestCost < bestCost)
                {
                    bestCost = neighbor.bestCost;
                    c.bestDirection = (neighbor.worldPos - c.worldPos).normalized;
                }
            }
        }
    }

    public Cell GetCellFromWorldPos(Vector3 worldPos)
    {
        if (grid == null) return null;

        Vector3 worldBottomLeft = transform.position
            - Vector3.right * gridSize.x * 0.5f * cellDiameter
            - Vector3.forward * gridSize.y * 0.5f * cellDiameter;
        float localX = worldPos.x - worldBottomLeft.x;
        float localZ = worldPos.z - worldBottomLeft.z;
        float gridWorldWidth = gridSize.x * cellDiameter;
        float gridWorldDepth = gridSize.y * cellDiameter;

        if (localX < 0f || localX >= gridWorldWidth || localZ < 0f || localZ >= gridWorldDepth)
            return null;

        int x = Mathf.Clamp(Mathf.FloorToInt(localX / cellDiameter), 0, gridSize.x - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(localZ / cellDiameter), 0, gridSize.y - 1);
        return grid[x, y];
    }

    List<Cell> GetNeighborCells(Vector2Int nodeIndex)
    {
        List<Cell> neighbors = new List<Cell>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                int checkX = nodeIndex.x + x;
                int checkY = nodeIndex.y + y;
                if (checkX >= 0 && checkX < gridSize.x && checkY >= 0 && checkY < gridSize.y)
                    neighbors.Add(grid[checkX, checkY]);
            }
        }
        return neighbors;
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
    }
    // Hàm này sẽ vẽ các ô lưới và mũi tên ra màn hình Scene (chỉ hiển thị trong Editor)
    void OnDrawGizmos()
    {
        if (grid == null) return;

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
}