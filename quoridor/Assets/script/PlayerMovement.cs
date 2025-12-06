using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("📝 Player Setup")]
    // 1 또는 2로 설정 (1: WASD 이동, 2: 방향키 이동)
    public int playerNumber = 1;

    [Header("⏳ Turn Management")]
    // 모든 플레이어가 공유하는 현재 턴 변수 (1부터 시작)
    public static int currentTurn = 1;
    // 게임 종료 상태 공유
    public static bool isGameOver = false;

    [Header("📏 Movement Settings")]
    public float moveDistance = 12f; // 한 칸 이동 거리
    public float moveSpeed = 20f;    // 이동 속도

    [Header("🧱 Wall Settings")]
    public int maxWalls = 10;          // 최대 벽 개수
    public int remainingWalls;         // 현재 남은 벽 개수 (게임 중 감소)

    public GameObject wallPrefab;      // 실제로 설치될 벽 프리팹 (Collider 포함)
    public GameObject ghostWall;       // 반투명 미리보기 벽 (Scene 오브젝트 또는 Prefab 연결)
    public Material blueTransparent;   // 설치 가능할 때 재질
    public Material redTransparent;    // 설치 불가능할 때 재질
    public LayerMask obstacleLayer;    // 벽과 플레이어를 감지할 레이어

    [Header("🎨 Visual Stock Settings")]
    // 벽 10개의 초기 위치를 직접 입력받는 배열 (플레이어마다 다르게 설정하세요!)
    public Vector3[] initialWallPositions;

    // 벽 10개의 초기 회전값을 직접 입력받는 배열 (기본값: 90, 0, 0)
    public Vector3[] initialWallRotations;

    // 생성된 재고 벽들을 관리하는 리스트 (FIFO 방식)
    private List<GameObject> stockWalls = new List<GameObject>();

    // --- 내부 변수들 ---
    private Vector3 targetPosition;    // 플레이어 이동 목표 지점
    private bool isWallMode = false;   // 벽 설치 모드 여부
    private Renderer ghostRenderer;    // 유령 벽의 색상을 바꾸기 위한 렌더러

    void Start()
    {
        // 변수 초기화
        targetPosition = transform.position;
        remainingWalls = maxWalls; // 벽 개수 10개로 초기화

        // 게임 시작 시 1번 플레이어부터 시작하도록 초기화 (P1 스크립트에서만 수행)
        if (playerNumber == 1)
        {
            currentTurn = 1;
            isGameOver = false; // 게임 재시작 시 상태 초기화
        }

        // 🛠️ Ghost Wall 자동 생성 및 안전장치
        if (ghostWall != null)
        {
            // [핵심 수정] 할당된 ghostWall이 Prefab일 수 있으므로, 강제로 Scene에 생성(Instantiate)합니다.
            GameObject ghostInstance = Instantiate(ghostWall);
            ghostInstance.name = $"GhostWall_Player{playerNumber}"; // 이름 변경하여 찾기 쉽게 함
            ghostWall = ghostInstance; // 변수가 이제 실제 Scene 객체를 가리키도록 갱신

            // 렌더러 찾기 (자식 포함)
            ghostRenderer = ghostWall.GetComponentInChildren<Renderer>();

            if (ghostRenderer == null)
            {
                Debug.LogWarning($"⚠️ [Player {playerNumber}] Ghost Wall에 Renderer가 없습니다! 벽이 보이지 않을 수 있습니다.");
            }
            else
            {
                // 시작할 때 렌더러는 켜두되, 오브젝트를 끕니다.
                ghostRenderer.enabled = true;
            }

            ghostWall.SetActive(false); // 처음엔 숨김

            // [중요] 유령 벽이 스스로를 장애물로 인식하지 않도록 콜라이더 모두 제거
            Collider[] ghostCols = ghostWall.GetComponentsInChildren<Collider>();
            foreach (var col in ghostCols)
            {
                Destroy(col);
            }
            Debug.Log($"🔧 [Player {playerNumber}] 유령 벽({ghostWall.name})이 생성되고 설정되었습니다.");
        }
        else
        {
            Debug.LogError($"❌ [Player {playerNumber}] 오류: Inspector창에서 Ghost Wall을 연결해주세요!");
        }

        // 🟢 초기 재고 벽 생성 (Visual Stock)
        SpawnStockWalls();
    }

    void Update()
    {
        // 0. 게임 오버 체크: 게임이 끝나면 아무 동작도 하지 않음
        if (isGameOver) return;

        // 1. 턴 체크: 내 차례가 아니면 아무 입력도 받지 않음
        if (playerNumber != currentTurn)
        {
            MovePlayerSmoothly(); // 이동 애니메이션은 계속 수행
            FixRotation();        // 회전 고정
            return;
        }

        // 2. 모드 전환 (Tab 키)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMode();
        }

        // 3. 현재 모드에 따른 동작 실행
        if (isWallMode)
        {
            HandleWallMode();

            // [디버깅] 유령 벽 위치 시각화 (Scene 뷰에서 빨간 선 확인 가능)
            if (ghostWall != null)
                Debug.DrawRay(ghostWall.transform.position, Vector3.up * 5, Color.red);
        }
        else
        {
            HandlePlayerMode();
        }

        // 4. 공통 처리
        MovePlayerSmoothly();
        FixRotation();
    }

    void FixRotation()
    {
        // 플레이어 넘어짐 방지 (항상 서 있도록)
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    // =========================================================
    // 🧱 시각적 재고 벽(Visual Stock) 관리
    // =========================================================
    void SpawnStockWalls()
    {
        if (initialWallPositions == null || wallPrefab == null) return;

        int count = Mathf.Min(initialWallPositions.Length, maxWalls);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = initialWallPositions[i];

            // 회전값 설정 (기본값: 90, 0, 0)
            Vector3 spawnRotEuler = new Vector3(90, 0, 0);
            if (initialWallRotations != null && i < initialWallRotations.Length)
            {
                spawnRotEuler = initialWallRotations[i];
            }

            // 벽 생성
            GameObject stockObj = Instantiate(wallPrefab, spawnPos, Quaternion.Euler(spawnRotEuler));

            // 재고 벽의 콜라이더 끄기 (설치 시 장애물로 인식되지 않게)
            Collider[] cols = stockObj.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;

            stockWalls.Add(stockObj);
        }
    }

    void RemoveOneStockWall()
    {
        // FIFO: 가장 앞에 있는(0번) 벽부터 사용 및 제거
        if (stockWalls.Count > 0)
        {
            GameObject wallToRemove = stockWalls[0];
            stockWalls.RemoveAt(0); // 리스트에서 제거 -> 다음 벽이 0번이 됨
            Destroy(wallToRemove);  // 화면에서 제거
        }
    }

    // =========================================================
    // 🎮 모드 전환 로직
    // =========================================================
    void ToggleMode()
    {
        if (ghostWall == null) return;

        if (!isWallMode)
        {
            // 벽 모드로 진입
            if (remainingWalls <= 0)
            {
                Debug.Log($"🚫 [Player {playerNumber}] 남은 벽이 없습니다!");
                return;
            }

            isWallMode = true;
            ghostWall.SetActive(true);
            if (ghostRenderer != null) ghostRenderer.enabled = true; // 렌더러 강제 활성화

            // ⭐️ 핵심 로직: 현재 사용 가능한 첫 번째 재고 벽(stockWalls[0]) 위치로 이동
            if (stockWalls.Count > 0)
            {
                ghostWall.transform.position = stockWalls[0].transform.position;
                ghostWall.transform.rotation = stockWalls[0].transform.rotation;

                Debug.Log($"🧱 [Player {playerNumber}] 벽 모드 시작! (위치: {ghostWall.transform.position})");
            }
            else
            {
                // 안전장치: 재고가 없다면 플레이어 위치에서 시작
                ghostWall.transform.position = new Vector3(Mathf.Round(transform.position.x), 0, Mathf.Round(transform.position.z));
                ghostWall.transform.rotation = Quaternion.Euler(90, 0, 0);
            }

            UpdateGhostWallColor();
        }
        else
        {
            // 이동 모드로 복귀
            isWallMode = false;
            ghostWall.SetActive(false);
            Debug.Log($"🏃 [Player {playerNumber}] 플레이어 이동 모드 복귀");
        }
    }

    // =========================================================
    // 🏃 플레이어 이동 로직
    // =========================================================
    void HandlePlayerMode()
    {
        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;
            Vector3 inputDir = GetInputDirection();

            if (inputDir != Vector3.zero)
            {
                // 🛑 이동 제한 로직 (맵 경계 및 승리 조건 체크)
                Vector3 nextPos = targetPosition + (inputDir * moveDistance);

                // 1. X축 범위 체크 (-48 ~ 48)
                if (nextPos.x < -48f || nextPos.x > 48f)
                {
                    Debug.Log("🚫 맵 밖으로 나갈 수 없습니다 (좌우 경계).");
                    return;
                }

                // 2. Z축 범위 체크 (-48 ~ 48) - 시작 지점 뒤로 나가는 것 방지
                // 승리 지점(48 또는 -48)에 도달하는 것은 허용해야 하므로 <=, >= 사용
                if (nextPos.z < -48f || nextPos.z > 48f)
                {
                    // 여기서 '승리'가 아닌 '맵 이탈'인 경우를 막아야 함.
                    // P1(승리목표 +48)이 -60으로 가려하거나, P2(승리목표 -48)가 +60으로 가려할 때 차단
                    // 사실상 -48 ~ 48 사이라면 유효한 보드 위임.
                    // 승리 판단은 이동 확정 후(아래)에서 처리
                    Debug.Log("🚫 맵 밖으로 나갈 수 없습니다 (상하 경계).");
                    return;
                }

                // 이동 경로에 벽이 있는지 감지 (Raycast)
                if (!Physics.Raycast(transform.position, inputDir, moveDistance, obstacleLayer))
                {
                    targetPosition += inputDir * moveDistance; // 이동 확정

                    // 🏆 승리 조건 체크
                    CheckWinCondition();

                    if (!isGameOver)
                    {
                        EndTurn(); // 게임이 안 끝났으면 턴 넘기기
                    }
                }
                else
                {
                    Debug.Log($"[P{playerNumber}] 앞에 벽이 있어 이동할 수 없습니다.");
                }
            }
        }
    }

    void CheckWinCondition()
    {
        // 플레이어 1: Z >= 48 이면 승리
        if (playerNumber == 1 && targetPosition.z >= 48f)
        {
            Debug.Log("🏆 Player 1 WIN! 게임 종료!");
            isGameOver = true;
        }
        // 플레이어 2: Z <= -48 이면 승리
        else if (playerNumber == 2 && targetPosition.z <= -48f)
        {
            Debug.Log("🏆 Player 2 WIN! 게임 종료!");
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
            // 90도 근처면 가로, 아니면 세로 (오차 범위 5도)
            bool isRotated90 = Mathf.Abs(Mathf.DeltaAngle(currentY, 90)) < 5f;

            bool shouldMove = false;

            // 상하 입력 (Z축)
            if (inputDir.z != 0)
            {
                if (isRotated90) ghostWall.transform.rotation = Quaternion.Euler(90, 0, 0); // 세로로 회전
                else shouldMove = true; // 이동
            }
            // 좌우 입력 (X축)
            else if (inputDir.x != 0)
            {
                if (!isRotated90) ghostWall.transform.rotation = Quaternion.Euler(90, 90, 0); // 가로로 회전
                else shouldMove = true; // 이동
            }

            if (shouldMove)
            {
                ghostWall.transform.position += inputDir * moveDistance;
            }

            UpdateGhostWallColor();
        }

        // 스페이스바: 설치 확정
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsValidPosition())
            {
                PlaceWall();
            }
            else
            {
                Debug.Log($"🚫 [Player {playerNumber}] 설치 불가: 위치가 겹칩니다.");
            }
        }
    }

    void UpdateGhostWallColor()
    {
        if (ghostRenderer == null) return;
        ghostRenderer.material = IsValidPosition() ? blueTransparent : redTransparent;
    }

    bool IsValidPosition()
    {
        Vector3 checkSize = new Vector3(moveDistance * 0.9f, 0.5f, 0.5f);
        Collider[] hitColliders = Physics.OverlapBox(
            ghostWall.transform.position,
            checkSize / 2,
            ghostWall.transform.rotation,
            obstacleLayer
        );

        // 유령 벽 자신이나 재고 벽은 장애물로 치지 않음
        foreach (Collider col in hitColliders)
        {
            if (col.gameObject == ghostWall) continue;
            if (stockWalls.Contains(col.gameObject)) continue;
            return false;
        }

        return true;
    }

    void PlaceWall()
    {
        if (remainingWalls > 0)
        {
            // 실제 벽 생성
            Instantiate(wallPrefab, ghostWall.transform.position, ghostWall.transform.rotation);
            remainingWalls--;

            // 사용한 재고 벽(현재 0번) 제거 -> 다음 벽이 0번이 됨
            RemoveOneStockWall();

            Debug.Log($"✅ [Player {playerNumber}] 벽 설치 완료! (남은 벽: {remainingWalls}개)");

            isWallMode = false;
            ghostWall.SetActive(false);
            EndTurn();
        }
    }

    // =========================================================
    // 🛠 유틸리티 함수 (입력 방향 - Player 2 반전 처리 포함)
    // =========================================================
    Vector3 GetInputDirection()
    {
        Vector3 dir = Vector3.zero;

        // Player 1: WASD (정방향)
        if (playerNumber == 1)
        {
            if (Input.GetKeyDown(KeyCode.W)) dir = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.S)) dir = Vector3.back;
            else if (Input.GetKeyDown(KeyCode.A)) dir = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.D)) dir = Vector3.right;
        }
        // Player 2: 화살표 (마주 보는 시점이므로 반대 방향 처리)
        else if (playerNumber == 2)
        {
            // Up 키 -> 월드 좌표 Back (내 기준 전진)
            if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector3.back;
            // Down 키 -> 월드 좌표 Forward (내 기준 후퇴)
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector3.forward;
            // Left 키 -> 월드 좌표 Right (내 기준 왼쪽)
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector3.right;
            // Right 키 -> 월드 좌표 Left (내 기준 오른쪽)
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector3.left;
        }

        return dir;
    }

    void EndTurn()
    {
        currentTurn = (currentTurn == 1) ? 2 : 1;
        Debug.Log($"🔄 턴 변경! 현재 턴: Player {currentTurn}");
    }
}