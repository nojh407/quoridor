using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필수

public class MainMenuManager : MonoBehaviour
{
    // 게임 플레이 씬의 이름을 정확히 입력해주세요 (예: "GameScene", "PlayScene" 등)
    // 유니티 File > Build Settings에 등록된 씬 이름이어야 합니다.
    public string gameSceneName = "GameScene";

    // Solo Play (PVE) 버튼 클릭 시 호출
    public void OnClickPVE()
    {
        // GameManager의 정적 변수에 PVE 모드 설정
        GameManager.SelectMode = GameManager.GameMode.PVE;

        // 게임 씬 로드
        SceneManager.LoadScene(gameSceneName);
        Debug.Log("PVE 모드로 게임 시작!");
    }

    // Multi Play (PVP) 버튼 클릭 시 호출
    public void OnClickPVP()
    {
        // GameManager의 정적 변수에 PVP 모드 설정
        GameManager.SelectMode = GameManager.GameMode.PVP;

        // 게임 씬 로드
        SceneManager.LoadScene(gameSceneName);
        Debug.Log("PVP 모드로 게임 시작!");
    }

    // Quit 버튼 클릭 시 호출
    public void OnClickQuit()
    {
        Debug.Log("게임 종료");
        Application.Quit();
    }
}