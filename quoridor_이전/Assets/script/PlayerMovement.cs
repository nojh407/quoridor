using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("📝 Player Setup")]
    public int playerNumber = 1; // 1: WASD, 2: 방향키 (AI일 경우 자동 제어)

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
    public GameObject ghostWall;       // 유령 벽 프리팹 (또는 씬 오브젝트)
    public Material blueTransparent;   // 설치 가능 재질
    public Material redTransparent;    // 설치 불가 재질
    public LayerMask obstacleLayer;    // 'Wall' 레이어 (플레이어도 감지되도록 설정 필수)

    [Header("🎨 Visual Stock Settings")]
    public Vector3[] initialWallPositions;
    public Vector3[] initialWallRotations;

    private List<GameObject> stockWalls = new List<GameObject>();

    // --- 내부 변수들 ---
    private Vector3 targetPosition;
    private bool isWallMode = false;
    private Renderer ghostRenderer;

    // AI 관련 변수
    private bool isAIThinking = false;

    void Start()
    {
        targetPosition = transform.position;
        remainingWalls = maxWalls;

        // 🛠️ Ghost Wall 자동 설정
        if (ghostWall != null)
        {
            // Prefab인 경우 Scene에 생성
            if (ghostWall.scene.rootCount == 0)
            {
                GameObject ghostInstance = Instantiate(ghostWall);
                ghostInstance.name = $"GhostWall_Player{playerNumber}";
                ghostWall = ghostInstance;
            }

            // 렌더러 확보 및 초기화
            ghostRenderer = ghostWall.GetComponentInChildren<Renderer>();
            if (ghostRenderer != null) ghostRenderer.enabled = true;

            ghostWall.SetActive(false);

            // 유령 벽의 콜라이더 제거 (자체 충돌 방지)
            Collider[] ghostCols = ghostWall.GetComponentsInChildren<Collider>();
            foreach (var col in ghostCols) Destroy(col);
        }
        else
        {
            Debug.LogError($"❌ [Player {playerNumber}] Ghost Wall이 연결되지 않았습니다!");
        }

        // 재고 벽 생성
        SpawnStockWalls();
    }

    void Update()
    {
        // GameManager에서 게임 종료 여부 확인
        if (GameManager.Instance.isTimeOver) return;

        // 내 턴인지 확인
        bool isMyTurn = false;
        if (playerNumber == 1 && GameManager.Instance.currentTurn == GameManager.TurnState.Player1) isMyTurn = true;
        if (playerNumber == 2 && GameManager.Instance.currentTurn == GameManager.TurnState.Player2) isMyTurn = true;

        if (!isMyTurn)
        {
            MovePlayerSmoothly();
            FixRotation();
            return;
        }

        // 🤖 PVE 모드이고 플레이어 2(AI)인 경우
        if (GameManager.Instance.gameMode == GameManager.GameMode.PVE && playerNumber == 2)
        {
            if (!isAIThinking)
            {
                StartCoroutine(AI_ThinkAndAct());
            }
            MovePlayerSmoothly();
            FixRotation();
            return; // 사용자 입력 차단
        }

        // 🎮 사람 플레이어 입력 처리
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

    void FixRotation()
    {
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    // =========================================================
    // 🖥️ UI 표시 (OnGUI)
    // =========================================================
    void OnGUI()
    {
        // 폰트 스타일 설정
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 25;
        style.fontStyle = FontStyle.Bold;

        // 시간은 GameManager에서 가져옴
        float myTimer = (playerNumber == 1) ? GameManager.Instance.p1Timer : GameManager.Instance.p2Timer;
        string timeStr = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(myTimer / 60), Mathf.FloorToInt(myTimer % 60));

        string infoText = $"Player {playerNumber}\n⏳ {timeStr}\n🧱 Walls: {remainingWalls}";

        // 플레이어별 위치 및 색상 설정 (둘 다 하얀색으로 통일)
        if (playerNumber == 1)
        {
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(30, 30, 300, 100), infoText, style);
        }
        else if (playerNumber == 2)
        {
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width - 200, 30, 300, 100), infoText, style);
        }
    }

    // =========================================================
    // 🧠 AI Logic (Minimax + Pathfinding)
    // =========================================================
    IEnumerator AI_ThinkAndAct()
    {
        isAIThinking = true;
        yield return new WaitForSeconds(1.0f); // 생각하는 척 딜레이 (1초)

        // 1. 상태 평가: 나와 상대방의 거리 계산
        PlayerMovement opponent = FindOpponent();
        int oppDist = GetBFSPathDistance(opponent.transform.position, opponent.playerNumber);

        // 2. 전략 수립: 상대가 너무 가까우면(3칸 이내) 확률적으로 벽 설치 시도
        bool tryWall = false;
        if (remainingWalls > 0 && oppDist <= 3 && Random.value > 0.3f)
        {
            tryWall = true;
        }

        // 3. 행동 실행
        bool actionDone = false;

        if (tryWall)
        {
            // 상대방 방해를 위한 벽 설치 시도
            actionDone = TryAIPlaceWall(opponent);
        }

        // 벽 설치를 안 했거나 실패했으면 최적의 이동 (Minimax 기반)
        if (!actionDone)
        {
            Vector3 bestMove = GetBestMove_Minimax();
            if (bestMove != Vector3.zero)
            {
                targetPosition = transform.position + bestMove;

                // 승리 체크 및 턴 넘기기
                CheckWinCondition();
                if (!GameManager.Instance.isTimeOver) // 게임이 안 끝났다면
                    GameManager.Instance.SwitchTurn();
            }
            else
            {
                // 움직일 곳이 없으면 턴 넘기기
                GameManager.Instance.SwitchTurn();
            }
        }

        isAIThinking = false;
    }

    // Minimax를 이용한 최적 이동 방향 찾기 (Depth 1~2 수준의 탐욕 알고리즘)
    Vector3 GetBestMove_Minimax()
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        Vector3 bestDir = Vector3.zero;
        int minScore = int.MaxValue; // 점수(거리)가 낮을수록 좋음

        foreach (Vector3 dir in dirs)
        {
            if (IsValidMove(transform.position, dir))
            {
                // 가상의 내 위치
                Vector3 nextPos = transform.position + (dir * moveDistance);
                int dist = GetBFSPathDistance(nextPos, playerNumber);

                // 내 목표까지의 거리가 가장 짧아지는 방향 선택
                if (dist < minScore)
                {
                    minScore = dist;
                    bestDir = dir * moveDistance;
                }
            }
        }
        return bestDir;
    }

    // AI 벽 설치 시도 (수정됨: 그리드 스내핑 적용)
    bool TryAIPlaceWall(PlayerMovement opponent)
    {
        isWallMode = true;
        ghostWall.SetActive(true);
        if (ghostRenderer != null) ghostRenderer.enabled = true; // 강제 렌더링 활성화

        // 1. 기준점 설정: 올바른 위치에 있는 '재고 벽(Stock Wall)'을 기준으로 삼습니다.
        Vector3 gridReferencePos = Vector3.zero;
        if (stockWalls.Count > 0)
        {
            gridReferencePos = stockWalls[0].transform.position;
        }
        else
        {
            // 재고가 없을 경우의 안전장치 (현재 위치 등)
            gridReferencePos = ghostWall.transform.position;
        }

        // 2. 상대방 위치까지의 거리 계산 및 그리드 스내핑
        Vector3 dirToOpponent = opponent.transform.position - gridReferencePos;

        int stepsX = Mathf.RoundToInt(dirToOpponent.x / moveDistance);
        int stepsZ = Mathf.RoundToInt(dirToOpponent.z / moveDistance);

        Vector3 basePos = gridReferencePos + new Vector3(stepsX * moveDistance, 0, stepsZ * moveDistance);

        // 랜덤하게 가로/세로 설정
        if (Random.value > 0.5f) ghostWall.transform.rotation = Quaternion.Euler(90, 0, 0);
        else ghostWall.transform.rotation = Quaternion.Euler(90, 90, 0);

        // 3. 상대방 주변 4방향 + 랜덤 오프셋으로 설치 시도
        for (int i = 0; i < 5; i++)
        {
            Vector3 tryPos = basePos + (new Vector3(Random.Range(-1, 2), 0, Random.Range(-1, 2)) * moveDistance);

            // 맵 밖으로 나가는지 체크
            if (tryPos.x < -48 || tryPos.x > 48 || tryPos.z < -48 || tryPos.z > 48) continue;

            ghostWall.transform.position = tryPos;

            // 설치 가능하고 길을 막지 않는다면 설치
            if (IsValidPosition() && !DoesWallBlockPath())
            {
                PlaceWall(); // 성공 시 PlaceWall 내부에서 턴 종료됨
                return true;
            }

            // 실패하면 회전 바꿔서 한 번 더 시도
            ghostWall.transform.rotation *= Quaternion.Euler(0, 90, 0); // 90도 회전
            if (IsValidPosition() && !DoesWallBlockPath())
            {
                PlaceWall();
                return true;
            }
        }

        // 실패 시 정리
        ghostWall.SetActive(false);
        isWallMode = false;
        return false;
    }

    // BFS를 이용한 목표까지의 최단 거리 계산
    int GetBFSPathDistance(Vector3 startPos, int pNum)
    {
        Vector2Int startNode = new Vector2Int(Mathf.RoundToInt(startPos.x), Mathf.RoundToInt(startPos.z));
        Queue<KeyValuePair<Vector2Int, int>> queue = new Queue<KeyValuePair<Vector2Int, int>>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(new KeyValuePair<Vector2Int, int>(startNode, 0));
        visited.Add(startNode);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            Vector2Int pos = current.Key;
            int dist = current.Value;

            // 목표 도달 체크
            if (pNum == 1 && pos.y >= 48) return dist;
            if (pNum == 2 && pos.y <= -48) return dist;

            foreach (var d in dirs)
            {
                Vector3 checkDir = new Vector3(d.x, 0, d.y);
                // 이동 가능 여부 체크 (벽 확인)
                if (IsValidMove(new Vector3(pos.x, 0, pos.y), checkDir))
                {
                    Vector2Int nextPos = pos + (d * (int)moveDistance);
                    if (!visited.Contains(nextPos))
                    {
                        visited.Add(nextPos);
                        queue.Enqueue(new KeyValuePair<Vector2Int, int>(nextPos, dist + 1));
                    }
                }
            }
        }
        return 999; // 길 없음
    }

    bool IsValidMove(Vector3 currentPos, Vector3 direction)
    {
        // 맵 경계 체크
        Vector3 nextPos = currentPos + (direction * moveDistance);
        if (nextPos.x < -48 || nextPos.x > 48 || nextPos.z < -48 || nextPos.z > 48) return false;

        // Raycast로 벽 확인
        if (Physics.Raycast(currentPos, direction, moveDistance, obstacleLayer)) return false;

        return true;
    }

    PlayerMovement FindOpponent()
    {
        PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
        foreach (var p in players)
        {
            if (p.playerNumber != this.playerNumber) return p;
        }
        return null; // 못 찾음 (에러)
    }

    // =========================================================
    // 🧱 재고 벽(Visual Stock) 관리 (기존 유지)
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

            // 재고 벽의 Collider 끄기 (AI 경로 계산 등 방해 금지)
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
    // 🎮 모드 전환 (기존 유지)
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
    // 🏃 플레이어 이동 로직 (기존 유지)
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
                                GameManager.Instance.SwitchTurn();
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
                        GameManager.Instance.SwitchTurn();
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
                            GameManager.Instance.SwitchTurn();
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
            GameManager.Instance.EndGame("Player 1", "목표 도달");
        else if (playerNumber == 2 && targetPosition.z <= -48f)
            GameManager.Instance.EndGame("Player 2", "목표 도달");
    }

    void MovePlayerSmoothly()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    // =========================================================
    // 🧱 벽 설치 로직 (기존 유지)
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
            if (GetBFSPathDistance(p.transform.position, p.playerNumber) >= 999) // 길 없음
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
            GameManager.Instance.SwitchTurn();
        }
    }

    // BFS (위에서 정의한 GetBFSPathDistance와 유사하지만, 여기선 bool 반환 목적)
    bool HasPathToGoal(PlayerMovement p)
    {
        return GetBFSPathDistance(p.transform.position, p.playerNumber) < 999;
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
}