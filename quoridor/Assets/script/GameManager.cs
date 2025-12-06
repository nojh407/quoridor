using UnityEngine;
using UnityEngine.UI; // UI 제어를 위해 필요
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance; // 싱글톤 패턴

    [Header("Game Mode Settings")]
    public bool isPvpMode = true; // PVP 모드 여부 (기본값 true)

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
        p1Timer = maxTime;
        p2Timer = maxTime;
        isGameEnded = false;
        isTimeOver = false;

        // 선공 결정 (임시로 Player1 선공)
        currentTurn = TurnState.Player1;

        // 화면 분할 설정 적용
        SetupCameras();

        Debug.Log("Game Started! Mode: " + (isPvpMode ? "PVP" : "AI"));

        // 오류가 났던 부분 해결: 아래에 UpdateMessage 함수가 정의되어 있어 정상 작동합니다.
        UpdateMessage("Player 1의 차례입니다.");
    }

    // 화면 분할 로직 (PVP: 분할, AI: 전체화면)
    void SetupCameras()
    {
        if (isPvpMode)
        {
            if (cameraP1 != null && cameraP2 != null)
            {
                // Player 1: 왼쪽 화면 (0 ~ 0.5)
                cameraP1.rect = new Rect(0f, 0f, 0.5f, 1f);

                // Player 2: 오른쪽 화면 (0.5 ~ 1.0)
                cameraP2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

                cameraP2.gameObject.SetActive(true);
            }
        }
        else
        {
            // AI 모드 등에서는 P1 카메라만 전체 화면 사용
            if (cameraP1 != null)
            {
                cameraP1.rect = new Rect(0f, 0f, 1f, 1f);
            }
            if (cameraP2 != null)
            {
                cameraP2.gameObject.SetActive(false);
            }
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

        // 오류가 났던 부분 해결: 아래에 FormatTime 함수가 정의되어 있어 정상 작동합니다.
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

    // ==========================================
    // [추가된 함수들] 아래 함수들이 없어서 오류가 발생했었습니다.
    // ==========================================

    // 1. 메시지 업데이트 함수 (화면 중앙 텍스트 변경)
    void UpdateMessage(string msg)
    {
        if (messageText != null)
        {
            messageText.text = msg;
        }
        // UI가 연결 안 되어 있을 때를 대비해 로그도 출력
        // Debug.Log("[System Message] " + msg); 
    }

    // 2. 시간 포맷팅 함수 (초 단위 float -> 분:초 string 변환)
    string FormatTime(float time)
    {
        if (time < 0) time = 0;

        float minutes = Mathf.FloorToInt(time / 60); // 분 계산
        float seconds = Mathf.FloorToInt(time % 60); // 초 계산

        // string.Format을 사용하여 "00:00" 형태로 반환
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}