using UnityEngine;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private GameManager gm;

    [Header("📝 Player Setup")]
    public int playerNumber = 1;

    [Header("📏 Movement Settings")]
    public float moveDistance = 12f;
    public float moveSpeed = 20f;

    [Header("🧱 Wall Settings")]
    public int maxWalls = 10;
    [HideInInspector] public int remainingWalls;

    public GameObject wallPrefab;
    public GameObject ghostWall;
    public Material blueTransparent;
    public Material redTransparent;
    public LayerMask obstacleLayer;

    // --- 내부 변수들 ---
    private Vector3 targetPosition;
    private bool isWallMode = false;
    private Renderer ghostRenderer;
    private List<GameObject> stockWalls = new List<GameObject>();
    private float wallLength = 22f; // Wall Size for OverlapBox
    private float wallThickness = 2f; // Wall Size for OverlapBox

    [Header("🎨 Visual Stock Settings")]
    public Vector3[] initialWallPositions;
    public Vector3[] initialWallRotations;

    void Start()
    {
        gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("GameManager.Instance를 찾을 수 없습니다!"); enabled = false; return; }

        targetPosition = transform.position;
        remainingWalls = maxWalls;

        SetupGhostWall();
        // SpawnStockWalls(); // 시각화 로직
    }

    void Update()
    {
        if (gm.IsGameEnded) return;

        if (playerNumber != (gm.currentTurn == GameManager.TurnState.Player1 ? 1 : 2))
        {
            MovePlayerSmoothly();
            FixRotation();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab)) ToggleMode();

        if (isWallMode)
        {
            HandleWallMode();
        }
        else
        {
            HandlePlayerMode();
        }

        MovePlayerSmoothly();
        FixRotation();
    }

    void SetupGhostWall()
    {
        if (ghostWall != null)
        {
            // ... (고스트 벽 초기화 로직 유지) ...
            if (ghostWall.scene.rootCount == 0 || ghostWall.GetComponentInParent<PlayerMovement>() == null)
            {
                GameObject ghostInstance = Instantiate(ghostWall);
                ghostInstance.name = $"GhostWall_Player{playerNumber}";
                ghostWall = ghostInstance;
            }
            ghostRenderer = ghostWall.GetComponentInChildren<Renderer>();
            if (ghostRenderer != null) ghostRenderer.enabled = true;
            ghostWall.SetActive(false);
            Collider[] ghostCols = ghostWall.GetComponentsInChildren<Collider>();
            foreach (var col in ghostCols) Destroy(col);
        }
        else
        {
            Debug.LogError($"❌ [Player {playerNumber}] Ghost Wall이 연결되지 않았습니다!");
        }
    }

    void FixRotation()
    {
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    // --- Game Logic ---

    void HandlePlayerMode()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;
            Vector3 inputDir = GetInputDirection();

            if (inputDir != Vector3.zero)
            {
                // **복잡한 쿼리도 이동 로직** (Raycast, Jump, Diagonal Jump 포함)
                // 이 로직은 이전 코드에서 가져와야 합니다. 여기서는 간략화합니다.

                Vector3 nextPos = transform.position + (inputDir * moveDistance);

                if (!IsWallBlocking(transform.position, nextPos))
                {
                    targetPosition = nextPos;
                    CheckWinCondition();
                    if (!gm.IsGameEnded) gm.SwitchTurn();
                }
            }
        }
    }

    bool IsWallBlocking(Vector3 start, Vector3 end)
    {
        return Physics.Raycast(start, (end - start).normalized, moveDistance, obstacleLayer);
    }

    void CheckWinCondition()
    {
        bool won = (playerNumber == 1 && targetPosition.z >= 48f) ||
                   (playerNumber == 2 && targetPosition.z <= -48f);

        if (won)
        {
            gm.CheckAndEndGame(playerNumber);
        }
    }

    void MovePlayerSmoothly()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    // --- Wall Mode ---

    void ToggleMode()
    {
        // ... (ToggleMode 로직 유지) ...
        if (ghostWall == null) return;

        if (!isWallMode)
        {
            if (remainingWalls <= 0) return;
            isWallMode = true;
            ghostWall.SetActive(true);
            // ... (고스트 벽 위치/회전 초기화 로직 유지) ...
            UpdateGhostWallColor();
        }
        else
        {
            isWallMode = false;
            ghostWall.SetActive(false);
        }
    }

    void HandleWallMode()
    {
        if (ghostWall == null) return;
        Vector3 inputDir = GetInputDirection();
        // ... (고스트 벽 이동/회전 로직 유지) ...

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsValidPlacement()) PlaceWall();
        }
    }

    void UpdateGhostWallColor()
    {
        if (ghostRenderer == null) return;
        bool physicallyValid = IsValidPosition();

        // ⚠️ PathFinder를 사용하여 경로 차단 확인
        bool pathValid = physicallyValid && !PathFinder.IsWallBlockingGoal(ghostWall.transform.position, ghostWall.transform.rotation);

        ghostRenderer.material = (physicallyValid && pathValid) ? blueTransparent : redTransparent;
    }

    bool IsValidPosition()
    {
        // ... (IsValidPosition OverlapBox 로직 유지) ...
        Vector3 checkSize = new Vector3(wallThickness * 0.7f, 3f, wallLength * 0.85f);
        Collider[] hitColliders = Physics.OverlapBox(ghostWall.transform.position, checkSize / 2, ghostWall.transform.rotation, obstacleLayer);

        foreach (Collider col in hitColliders)
        {
            if (col.gameObject == ghostWall || col.transform.root.gameObject == ghostWall) continue;
            if (stockWalls.Contains(col.gameObject)) continue;
            return false;
        }
        return true;
    }

    bool IsValidPlacement()
    {
        if (remainingWalls <= 0) return false;
        if (!IsValidPosition()) return false;

        return !PathFinder.IsWallBlockingGoal(ghostWall.transform.position, ghostWall.transform.rotation);
    }

    void PlaceWall()
    {
        if (remainingWalls > 0)
        {
            GameObject newWall = Instantiate(wallPrefab, ghostWall.transform.position, ghostWall.transform.rotation);

            gm.activeWalls.Add(newWall);

            remainingWalls--;
            // RemoveOneStockWall(); // 재고 벽 시각화 제거 로직
            isWallMode = false;
            ghostWall.SetActive(false);

            gm.SwitchTurn();
        }
    }

    // --- Input Helper ---

    Vector3 GetInputDirection()
    {
        Vector3 dir = Vector3.zero;

        if (playerNumber == 1) // WASD
        {
            if (Input.GetKeyDown(KeyCode.W)) dir = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.S)) dir = Vector3.back;
            else if (Input.GetKeyDown(KeyCode.A)) dir = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D)) dir = Vector3.right;
        }
        else if (playerNumber == 2) // 화살표
        {
            // ⚠️ P2는 방향키 기준 반전 로직이었으나, AI 호환성을 위해 직관적으로 변경하거나 유지해야 함
            // 여기서는 기존 코드대로 화살표 입력을 사용하되, P2의 움직임이 반전되는 것이 자연스러운지 확인 필요
            if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector3.back;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector3.right;
        }
        return dir;
    }

    // ExecuteAIAction은 AIManager에서 호출됩니다.
    public void ExecuteAIAction(AIMoveDefinitions.AIAction action)
    {
        // AIAction을 받아 PlayerMovement를 실행하는 로직 구현
        // 1. Move
        if (action.Type == AIMoveDefinitions.AIActionType.Move)
        {
            // ... (이동 로직 실행) ...
        }
        // 2. Wall
        else if (action.Type == AIMoveDefinitions.AIActionType.Wall)
        {
            // ... (벽 설치 로직 실행) ...
        }

        // 실행 후 턴 종료
        gm.SwitchTurn();
    }
}