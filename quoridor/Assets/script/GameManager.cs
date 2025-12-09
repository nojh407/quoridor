using UnityEngine;
using UnityEngine.UI; // UI 제어를 위해 필요
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // 싱글톤 패턴

    // 게임 모드 정의
    public enum GameMode
    {
        PVP, // 플레이어 vs 플레이어 (화면 분할)
        PVE  // 플레이어 vs AI (전체 화면)
    }

    // [추가됨] 메인 메뉴에서 선택한 모드를 저장하는 전역 변수 (씬이 바껴도 유지됨)
    public static GameMode SelectMode = GameMode.PVP;

    [Header("Game Mode Settings")]
    public GameMode gameMode = GameMode.PVP; // 현재 게임의 모드

    [Header("Player Settings")]
    public Transform player1; // 플레이어 1 오브젝트
    public Transform player2; // 플레이어 2 오브젝트

    // 현재 턴 상태 (Player1 또는 Player2)
    public enum TurnState { Player1, Player2 }
    public TurnState currentTurn;

    [Header("Timer Settings (Seconds)")]
    public float maxTime = 300f; // 제한시간 5분 (300초)
    public float p1Timer;
    public float p2Timer;
    public bool isTimeOver = false;

    [Header("Camera Settings (Split Screen)")]
    public Camera cameraP1; // 플레이어 1 전용 카메라
    public Camera cameraP2; // 플레이어 2 전용 카메라

    [Header("UI References")]
    public Text timerTextP1;
    public Text timerTextP2;
    public Text messageText; // 시스템 메시지 출력용

    private bool isGameEnded = false;

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeGame();
    }

    void Update()
    {
        if (isGameEnded) return;

        // 턴에 따른 타이머 감소 로직
        UpdateTimer();
    }

    // 게임 초기화
    void InitializeGame()
    {
        // [중요 수정] 메인 메뉴에서 선택했던 모드를 현재 게임 모드로 적용
        gameMode = SelectMode;

        p1Timer = maxTime;
        p2Timer = maxTime;
        isGameEnded = false;
        isTimeOver = false;

        // 선공 결정 (임시로 Player1 선공)
        currentTurn = TurnState.Player1;

        // 화면 분할 설정 적용
        SetupCameras();

        Debug.Log($"Game Started! Mode: {gameMode}");

        UpdateMessage("Player 1의 차례입니다.");
    }

    // 화면 분할 로직 (PVE: 전체화면, PVP: 분할)
    void SetupCameras()
    {
        switch (gameMode)
        {
            case GameMode.PVE: // 1인 전체 화면 (AI 모드)
                if (cameraP1 != null)
                {
                    cameraP1.rect = new Rect(0f, 0f, 1f, 1f); // 전체 화면
                }
                if (cameraP2 != null)
                {
                    cameraP2.gameObject.SetActive(false); // P2 카메라 끄기
                }
                break;

            case GameMode.PVP: // 2인 화면 분할
                if (cameraP1 != null)
                {
                    cameraP1.rect = new Rect(0f, 0f, 0.5f, 1f); // 왼쪽 절반
                }
                if (cameraP2 != null)
                {
                    cameraP2.gameObject.SetActive(true); // P2 카메라 켜기
                    cameraP2.rect = new Rect(0.5f, 0f, 0.5f, 1f); // 오른쪽 절반
                }
                break;
        }
    }

    // 타이머 업데이트
    void UpdateTimer()
    {
        if (currentTurn == TurnState.Player1)
        {
            p1Timer -= Time.deltaTime;
            if (p1Timer <= 0)
            {
                p1Timer = 0;
                EndGame(winner: "Player 2", reason: "시간 초과");
            }
        }
        else
        {
            p2Timer -= Time.deltaTime;
            if (p2Timer <= 0)
            {
                p2Timer = 0;
                EndGame(winner: "Player 1", reason: "시간 초과");
            }
        }

        if (timerTextP1 != null) timerTextP1.text = FormatTime(p1Timer);
        if (timerTextP2 != null) timerTextP2.text = FormatTime(p2Timer);
    }

    // 턴 넘기기
    public void SwitchTurn()
    {
        if (isGameEnded) return;

        if (currentTurn == TurnState.Player1)
        {
            currentTurn = TurnState.Player2;
            UpdateMessage("Player 2의 차례입니다.");
        }
        else
        {
            currentTurn = TurnState.Player1;
            UpdateMessage("Player 1의 차례입니다.");
        }
    }

    // 게임 종료 처리
    public void EndGame(string winner, string reason)
    {
        isGameEnded = true;
        isTimeOver = true; // 타이머 정지용

        // 종료 메시지 출력
        UpdateMessage($"{winner} 승리! ({reason})");
        Debug.Log($"Game Over. Winner: {winner}");
    }

    // 메시지 업데이트 함수
    void UpdateMessage(string msg)
    {
        if (messageText != null)
        {
            messageText.text = msg;
        }
    }

    // 시간 포맷팅 함수 (분:초)
    string FormatTime(float time)
    {
        if (time < 0) time = 0;

        float minutes = Mathf.FloorToInt(time / 60); // 분 계산
        float seconds = Mathf.FloorToInt(time % 60); // 초 계산

        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}