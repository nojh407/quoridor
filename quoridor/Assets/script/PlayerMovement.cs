using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("📝 Player Setup")]
    public int playerNumber = 1; // 1: WASD, 2: 방향키 (상하좌우 반전)

    [Header("⏳ Turn Management")]
    public static int currentTurn = 1;
    public static bool isGameOver = false;
    public static int winner = 0; // 0: 진행중, 1: P1승리, 2: P2승리

    [Header("⏰ Timer Settings")]
    public float initialTime = 300f; // 초기 시간 5분 (300초)
    public float currentTimer;       // 현재 남은 시간

    [Header("📏 Movement Settings")]
    public float moveDistance = 12f; // 한 칸 이동 거리 (타일 10 + 간격 2)
    public float moveSpeed = 20f;    // 이동 속도

    [Header("🧱 Wall Settings")]
    public int maxWalls = 10;
    public int remainingWalls;

    // 벽 사이즈 설정 (Scale 2, 4, 22 기준)
    private float wallLength = 22f;
    private float wallThickness = 2f;

    public GameObject wallPrefab;      // 실제 벽 프리팹
    public GameObject ghostWall;       // 유령 벽 프리팹
    public Material blueTransparent;   // 설치 가능 재질
    public Material redTransparent;    // 설치 불가 재질
    public LayerMask obstacleLayer;    // 'Wall' 레이어

    [Header("🎨 Visual Stock Settings")]
    public Vector3[] initialWallPositions;
    public Vector3[] initialWallRotations;

    private List<GameObject> stockWalls = new List<GameObject>();

    // --- 내부 변수들 ---
    private Vector3 targetPosition;
    private bool isWallMode = false;
    private Renderer ghostRenderer;

    void Start()
    {
        targetPosition = transform.position;
        remainingWalls = maxWalls;

        // 타이머 초기화
        currentTimer = initialTime;

        // 게임 시작/재시작 시 초기화
        if (playerNumber == 1)
        {
            currentTurn = 1;
            isGameOver = false;
            winner = 0;
        }

        // 🛠️ Ghost Wall 자동 설정
        if (ghostWall != null)
        {
            if (ghostWall.scene.rootCount == 0)
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

        SpawnStockWalls();
    }

    void Update()
    {
        if (isGameOver) return;

        // ⏰ 타이머 로직
        if (playerNumber == currentTurn)
        {
            currentTimer -= Time.deltaTime;

            if (currentTimer <= 0)
            {
                currentTimer = 0;
                isGameOver = true;
                winner = (playerNumber == 1) ? 2 : 1; // 내 시간이 다 되면 상대방 승리
                Debug.Log($"⏰ Time Over! Player {winner} WIN!");
            }
        }
        else
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

    // =========================================================
    // 🖥️ UI 표시 (OnGUI) - 별도 설정 없이 화면에 그리기
    // =========================================================
    void OnGUI()
    {
        // 폰트 스타일 설정
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 25;
        style.fontStyle = FontStyle.Bold;

        // 시간 포맷 (00:00)
        string timeStr = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(currentTimer / 60), Mathf.FloorToInt(currentTimer % 60));
        string infoText = $"Player {playerNumber}\n⏳ {timeStr}\n🧱 Walls: {remainingWalls}";

        // 플레이어별 위치 및 색상 설정
        if (playerNumber == 1)
        {
            style.normal.textColor = Color.white; // 플레이어 1 정보 하얀색
            GUI.Label(new Rect(30, 30, 300, 100), infoText, style);
        }
        else if (playerNumber == 2)
        {
            style.normal.textColor = Color.white; // 플레이어 2 정보 하얀색
            // 화면 오른쪽 정렬
            GUI.Label(new Rect(Screen.width - 200, 30, 300, 100), infoText, style);
        }

        // 중앙 상태 표시 (Player 1이 대표로 그림) - 기존 색상 유지
        if (playerNumber == 1)
        {
            GUIStyle centerStyle = new GUIStyle(GUI.skin.label);
            centerStyle.fontSize = 40;
            centerStyle.fontStyle = FontStyle.Bold;
            centerStyle.alignment = TextAnchor.UpperCenter;
            centerStyle.normal.textColor = Color.black;

            string centerText = "";
            if (isGameOver)
            {
                centerStyle.normal.textColor = (winner == 1) ? Color.blue : Color.red;
                centerText = $"🏆 Player {winner} WIN! 🏆";
            }
            else
            {
                centerText = $"Turn: Player {currentTurn}";
            }

            GUI.Label(new Rect(Screen.width / 2 - 200, 30, 400, 100), centerText, centerStyle);
        }
    }

    void FixRotation()
    {
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    // =========================================================
    // 🧱 재고 벽 관리
    // =========================================================
    void SpawnStockWalls()
    {
        if (initialWallPositions == null || wallPrefab == null) return;

        int count = Mathf.Min(initialWallPositions.Length, maxWalls);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = initialWallPositions[i];
            Vector3 spawnRotEuler = new Vector3(90, 0, 0);

            if (initialWallRotations != null && i < initialWallRotations.Length)
            {
                spawnRotEuler = initialWallRotations[i];
            }

            GameObject stockObj = Instantiate(wallPrefab, spawnPos, Quaternion.Euler(spawnRotEuler));
            Collider[] cols = stockObj.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;

            stockWalls.Add(stockObj);
        }
    }

    void RemoveOneStockWall()
    {
        if (stockWalls.Count > 0)
        {
            GameObject wallToRemove = stockWalls[0];
            stockWalls.RemoveAt(0);
            Destroy(wallToRemove);
        }
    }

    // =========================================================
    // 🎮 모드 전환
    // =========================================================
    void ToggleMode()
    {
        if (ghostWall == null) return;

        if (!isWallMode)
        {
            if (remainingWalls <= 0) return;

            isWallMode = true;
            ghostWall.SetActive(true);
            if (ghostRenderer != null) ghostRenderer.enabled = true;

            if (stockWalls.Count > 0)
            {
                ghostWall.transform.position = stockWalls[0].transform.position;
                ghostWall.transform.rotation = stockWalls[0].transform.rotation;
            }
            else
            {
                ghostWall.transform.position = new Vector3(Mathf.Round(transform.position.x), 0, Mathf.Round(transform.position.z));
                ghostWall.transform.rotation = Quaternion.Euler(90, 0, 0);
            }

            UpdateGhostWallColor();
        }
        else
        {
            isWallMode = false;
            ghostWall.SetActive(false);
        }
    }

    // =========================================================
    // 🏃 플레이어 이동
    // =========================================================
    void HandlePlayerMode()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;
            Vector3 inputDir = GetInputDirection();

            if (inputDir != Vector3.zero)
            {
                if (TryDiagonalJump(inputDir)) return;

                if (Physics.Raycast(transform.position, inputDir, out RaycastHit hit, moveDistance, obstacleLayer))
                {
                    PlayerMovement otherPlayer = hit.collider.GetComponent<PlayerMovement>();

                    if (otherPlayer != null)
                    {
                        if (!Physics.Raycast(otherPlayer.transform.position, inputDir, moveDistance, obstacleLayer))
                        {
                            Vector3 jumpPos = transform.position + (inputDir * moveDistance * 2);
                            if (IsValidMapPosition(jumpPos))
                            {
                                targetPosition = jumpPos;
                                CheckWinCondition();
                                if (!isGameOver) EndTurn();
                            }
                        }
                    }
                }
                else
                {
                    Vector3 nextPos = transform.position + (inputDir * moveDistance);
                    if (IsValidMapPosition(nextPos))
                    {
                        targetPosition = nextPos;
                        CheckWinCondition();
                        if (!isGameOver) EndTurn();
                    }
                }
            }
        }
    }

    bool TryDiagonalJump(Vector3 inputDir)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        foreach (Vector3 neighborDir in dirs)
        {
            if (Mathf.Abs(Vector3.Dot(inputDir.normalized, neighborDir.normalized)) > 0.1f) continue;

            if (Physics.Raycast(transform.position, neighborDir, out RaycastHit hit, moveDistance, obstacleLayer))
            {
                PlayerMovement otherPlayer = hit.collider.GetComponent<PlayerMovement>();
                if (otherPlayer == null) continue;

                if (Physics.Raycast(otherPlayer.transform.position, neighborDir, moveDistance, obstacleLayer))
                {
                    if (!Physics.Raycast(otherPlayer.transform.position, inputDir, moveDistance, obstacleLayer))
                    {
                        Vector3 diagTarget = otherPlayer.transform.position + (inputDir * moveDistance);
                        if (IsValidMapPosition(diagTarget))
                        {
                            targetPosition = diagTarget;
                            CheckWinCondition();
                            if (!isGameOver) EndTurn();
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    bool IsValidMapPosition(Vector3 pos)
    {
        if (pos.x < -48f || pos.x > 48f) return false;
        if (pos.z < -48f || pos.z > 48f) return false;
        return true;
    }

    void CheckWinCondition()
    {
        if (playerNumber == 1 && targetPosition.z >= 48f)
        {
            winner = 1;
            isGameOver = true;
        }
        else if (playerNumber == 2 && targetPosition.z <= -48f)
        {
            winner = 2;
            isGameOver = true;
        }
    }

    void MovePlayerSmoothly()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    // =========================================================
    // 🧱 벽 설치 로직
    // =========================================================
    void HandleWallMode()
    {
        if (ghostWall == null) return;

        Vector3 inputDir = GetInputDirection();

        if (inputDir != Vector3.zero)
        {
            float currentY = ghostWall.transform.eulerAngles.y;
            bool isRotated90 = Mathf.Abs(Mathf.DeltaAngle(currentY, 90)) < 5f;

            bool shouldMove = false;

            if (inputDir.z != 0)
            {
                if (isRotated90) ghostWall.transform.rotation = Quaternion.Euler(90, 0, 0);
                else shouldMove = true;
            }
            else if (inputDir.x != 0)
            {
                if (!isRotated90) ghostWall.transform.rotation = Quaternion.Euler(90, 90, 0);
                else shouldMove = true;
            }

            if (shouldMove)
            {
                ghostWall.transform.position += inputDir * moveDistance;
            }

            UpdateGhostWallColor();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsValidPosition()) PlaceWall();
        }
    }

    void UpdateGhostWallColor()
    {
        if (ghostRenderer == null) return;
        bool physicallyValid = IsValidPosition();
        bool pathValid = physicallyValid && !DoesWallBlockPath();
        ghostRenderer.material = (physicallyValid && pathValid) ? blueTransparent : redTransparent;
    }

    bool IsValidPosition()
    {
        Vector3 checkSize = new Vector3(wallThickness * 0.7f, 3f, wallLength * 0.85f);
        Collider[] hitColliders = Physics.OverlapBox(ghostWall.transform.position, checkSize / 2, ghostWall.transform.rotation, obstacleLayer);

        foreach (Collider col in hitColliders)
        {
            if (col.gameObject == ghostWall) continue;
            if (col.transform.root.gameObject == ghostWall) continue;
            if (stockWalls.Contains(col.gameObject)) continue;
            return false;
        }
        return true;
    }

    bool DoesWallBlockPath()
    {
        BoxCollider tempCol = ghostWall.AddComponent<BoxCollider>();
        Physics.SyncTransforms();

        bool isBlocked = false;
        PlayerMovement[] allPlayers = FindObjectsOfType<PlayerMovement>();

        foreach (var p in allPlayers)
        {
            if (!HasPathToGoal(p))
            {
                isBlocked = true;
                break;
            }
        }
        DestroyImmediate(tempCol);
        return isBlocked;
    }

    void PlaceWall()
    {
        if (remainingWalls > 0)
        {
            if (DoesWallBlockPath()) return;

            GameObject newWall = Instantiate(wallPrefab, ghostWall.transform.position, ghostWall.transform.rotation);
            int layerId = LayerMask.NameToLayer("Wall");
            if (layerId != -1)
            {
                newWall.layer = layerId;
                foreach (Transform t in newWall.transform) t.gameObject.layer = layerId;
            }

            remainingWalls--;
            RemoveOneStockWall();
            isWallMode = false;
            ghostWall.SetActive(false);
            EndTurn();
        }
    }

    bool HasPathToGoal(PlayerMovement p)
    {
        Vector2Int start = new Vector2Int(Mathf.RoundToInt(p.transform.position.x), Mathf.RoundToInt(p.transform.position.z));
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (p.playerNumber == 1 && current.y >= 48) return true;
            if (p.playerNumber == 2 && current.y <= -48) return true;

            foreach (var d in directions)
            {
                Vector2Int neighbor = current + (d * (int)moveDistance);

                if (neighbor.x < -48 || neighbor.x > 48 || neighbor.y < -48 || neighbor.y > 48) continue;
                if (visited.Contains(neighbor)) continue;

                Vector3 currentWorld = new Vector3(current.x, 0, current.y);
                Vector3 dirWorld = Vector3.zero;
                if (d == Vector2Int.up) dirWorld = Vector3.forward;
                if (d == Vector2Int.down) dirWorld = Vector3.back;
                if (d == Vector2Int.left) dirWorld = Vector3.left;
                if (d == Vector2Int.right) dirWorld = Vector3.right;

                bool blocked = false;
                RaycastHit[] hits = Physics.RaycastAll(currentWorld, dirWorld, moveDistance, obstacleLayer);
                foreach (var hit in hits)
                {
                    if (hit.collider.GetComponent<PlayerMovement>() != null) continue;
                    blocked = true;
                    break;
                }

                if (!blocked)
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return false;
    }

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
            if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector3.back;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector3.right;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector3.left;
        }
        return dir;
    }

    void EndTurn()
    {
        currentTurn = (currentTurn == 1) ? 2 : 1;
    }
}