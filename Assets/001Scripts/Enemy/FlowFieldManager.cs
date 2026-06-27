using System.Collections.Generic;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance;

    [Header("Grid Settings")]
    // Tăng kích thước lưới lên 100x100 để bao phủ trọn vẹn khu vực spawn quái vật
    public Vector2Int gridSize = new Vector2Int(100, 100);
    public float cellRadius = 0.5f;
    public LayerMask obstacleLayer; // Nhớ tạo Layer "Obstacles" và gán cho các bức tường

    [Header("Target")]
    public Transform playerTransform;

    private Cell[,] grid;
    private float cellDiameter;
    private Cell targetCell;

    void Awake()
    {
        Instance = this;
        cellDiameter = cellRadius * 2f;
        CreateGrid();
    }

    void Update()
    {
        if (playerTransform == null) return;

        if (Vector3.Distance(transform.position, playerTransform.position) > 15f)
        {
            transform.position = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);

            CreateGrid();
            targetCell = null; 
        }

        Cell currentTargetCell = GetCellFromWorldPos(playerTransform.position);

        // Chỉ cập nhật lại các mũi tên khi Player bước sang ô đất khác
        if (targetCell != currentTargetCell)
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
        // Tính điểm bắt đầu của lưới dựa theo vị trí thực tế của FlowFieldManager
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridSize.x / 2 * cellDiameter - Vector3.forward * gridSize.y / 2 * cellDiameter;

        // Tính toán phần trăm vị trí chính xác
        float percentX = Mathf.Clamp01((worldPos.x - worldBottomLeft.x) / (gridSize.x * cellDiameter));
        float percentY = Mathf.Clamp01((worldPos.z - worldBottomLeft.z) / (gridSize.y * cellDiameter));

        int x = Mathf.RoundToInt((gridSize.x - 1) * percentX);
        int y = Mathf.RoundToInt((gridSize.y - 1) * percentY);
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