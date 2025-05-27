using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;

/// <summary>
/// 게임 진행, 타이머, 승리 조건 등을 관리하는 클래스
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameManager : UdonSharpBehaviour
{
    // 팀 상수 정의
    private const int TEAM_HUNTER = 0;  // 사냥꾼 팀
    private const int TEAM_BUG = 1;     // 벌레 팀

    // 게임 상태 상수
    private const int GAME_STATE_WAITING = 0;     // 대기 중
    private const int GAME_STATE_PLAYING = 1;     // 게임 중
    private const int GAME_STATE_FINISHED = 2;    // 게임 종료

    // 승리 팀 상수
    private const int WINNER_NONE = -1;    // 승자 없음
    private const int WINNER_HUNTER = 0;   // 사냥꾼 승리
    private const int WINNER_BUG = 1;      // 벌레 승리

    [Header("게임 설정")]
    [SerializeField] private float gameDuration = 180f; // 게임 시간 (초, 기본 3분)

    [Header("UI 요소")]
    [SerializeField] private Canvas gameUICanvas; // 게임 진행 UI 캔버스
    [SerializeField] private TextMeshProUGUI timerText; // 타이머 텍스트
    [SerializeField] private TextMeshProUGUI gameStatusText; // 게임 상태 텍스트
    [SerializeField] private GameObject winnerPanel; // 승리 패널
    [SerializeField] private TextMeshProUGUI winnerText; // 승리 팀 텍스트

    [Header("참조")]
    [SerializeField] private CheckPlayer checkPlayer; // CheckPlayer 참조
    [SerializeField] private GameStarter gameStarter; // GameStarter 참조

    // 게임 상태 변수
    [UdonSynced] private int gameState = GAME_STATE_WAITING;
    [UdonSynced] private float gameTimer = 0f;
    [UdonSynced] private int winnerTeam = WINNER_NONE;
    [UdonSynced] private int aliveBugCount = 0; // 살아있는 벌레 수

    // 로컬 변수
    private VRCPlayerApi localPlayer;
    private bool isInitialized = false;

    void Start()
    {
        // 로컬 플레이어 정보 초기화
        localPlayer = Networking.LocalPlayer;

        // 참조 확인
        if (checkPlayer == null)
        {
            Debug.LogError("GameManager: CheckPlayer 참조가 설정되지 않았습니다.");
        }

        if (gameStarter == null)
        {
            Debug.LogError("GameManager: GameStarter 참조가 설정되지 않았습니다.");
        }

        // UI 초기화
        if (gameUICanvas != null)
        {
            gameUICanvas.gameObject.SetActive(false);
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(false);
        }

        isInitialized = true;
    }

    /// <summary>
    /// 게임 시작 (CheckAvater에서 호출)
    /// </summary>
    public void StartGame()
    {
        if (!Utilities.IsValid(localPlayer) || !isInitialized) return;

        // 마스터 클라이언트만 게임 상태 설정
        if (localPlayer.isMaster)
        {
            // 벌레 팀 플레이어 수 계산
            aliveBugCount = CountBugPlayers();

            // 게임 상태 설정
            gameState = GAME_STATE_PLAYING;
            gameTimer = gameDuration;
            winnerTeam = WINNER_NONE;

            // 네트워크 동기화
            RequestSerialization();

            Debug.Log("게임 시작: 벌레 팀 " + aliveBugCount + "명, 제한 시간 " + gameDuration + "초");
        }

        // UI 활성화
        if (gameUICanvas != null)
        {
            gameUICanvas.gameObject.SetActive(true);
        }

        // 게임 상태 텍스트 업데이트
        UpdateGameStatusText();
    }

    /// <summary>
    /// 네트워크 직렬화 후 처리
    /// </summary>
    public override void OnDeserialization()
    {
        // 게임 상태에 따른 UI 업데이트
        if (gameState == GAME_STATE_PLAYING)
        {
            if (gameUICanvas != null && !gameUICanvas.gameObject.activeSelf)
            {
                gameUICanvas.gameObject.SetActive(true);
            }
        }
        else if (gameState == GAME_STATE_FINISHED)
        {
            ShowGameResult();
        }
    }

    /// <summary>
    /// 매 프레임마다 호출
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;

        // 게임 진행 중일 때만 타이머 업데이트
        if (gameState == GAME_STATE_PLAYING)
        {
            // 마스터 클라이언트만 타이머 업데이트
            if (localPlayer.isMaster)
            {
                // 타이머 감소
                gameTimer -= Time.deltaTime;

                // 타이머가 0 이하면 게임 종료 (벌레 팀 승리)
                if (gameTimer <= 0)
                {
                    gameTimer = 0;
                    EndGame(WINNER_BUG);
                }

                // 네트워크 동기화 (1초에 한 번)
                if (Mathf.FloorToInt(gameTimer) != Mathf.FloorToInt(gameTimer + Time.deltaTime))
                {
                    RequestSerialization();
                }
            }

            // 타이머 텍스트 업데이트
            UpdateTimerText();
        }
    }

    /// <summary>
    /// 타이머 텍스트 업데이트
    /// </summary>
    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(gameTimer / 60);
            int seconds = Mathf.FloorToInt(gameTimer % 60);

            // 시간 형식: MM:SS
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

            // 남은 시간이 30초 이하면 빨간색으로 표시
            if (gameTimer <= 30)
            {
                timerText.color = Color.red;
            }
            else
            {
                timerText.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 게임 상태 텍스트 업데이트
    /// </summary>
    private void UpdateGameStatusText()
    {
        if (gameStatusText != null)
        {
            if (gameState == GAME_STATE_PLAYING)
            {
                // 플레이어 팀에 따라 다른 메시지 표시
                int playerTeam = checkPlayer.GetLocalPlayerTeam();

                if (playerTeam == TEAM_HUNTER)
                {
                    gameStatusText.text = "벌레를 모두 잡으세요!";
                    gameStatusText.color = new Color(1f, 0.5f, 0f); // 주황색
                }
                else if (playerTeam == TEAM_BUG)
                {
                    gameStatusText.text = "사냥꾼을 피해 숨으세요!";
                    gameStatusText.color = new Color(0.2f, 0.8f, 0.2f); // 초록색
                }
                else
                {
                    gameStatusText.text = "게임 진행 중...";
                    gameStatusText.color = Color.white;
                }
            }
            else if (gameState == GAME_STATE_WAITING)
            {
                gameStatusText.text = "게임 준비 중...";
                gameStatusText.color = Color.white;
            }
            else if (gameState == GAME_STATE_FINISHED)
            {
                gameStatusText.text = "게임 종료!";
                gameStatusText.color = Color.yellow;
            }
        }
    }

    /// <summary>
    /// 벌레 팀 플레이어 수 계산
    /// </summary>
    private int CountBugPlayers()
    {
        int count = 0;

        // 모든 플레이어 가져오기
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);

        // 벌레 팀 플레이어 수 계산
        foreach (var player in players)
        {
            if (!Utilities.IsValid(player)) continue;

            if (checkPlayer.GetPlayerTeam(player.playerId) == TEAM_BUG)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 플레이어 탈락 처리 (HammerController에서 호출)
    /// </summary>
    public void OnPlayerEliminated(int playerId)
    {
        if (!localPlayer.isMaster) return;

        // 탈락한 플레이어가 벌레 팀인지 확인
        if (checkPlayer.GetPlayerTeam(playerId) == TEAM_BUG)
        {
            // 살아있는 벌레 수 감소
            aliveBugCount--;

            // 벌레가 모두 탈락하면 사냥꾼 팀 승리
            if (aliveBugCount <= 0)
            {
                EndGame(WINNER_HUNTER);
            }

            // 네트워크 동기화
            RequestSerialization();

            // 전체 플레이어에게 상태 알림
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "UpdateGameStatusText");

            Debug.Log("벌레 팀 플레이어 탈락: 남은 벌레 " + aliveBugCount + "명");
        }
    }

    /// <summary>
    /// 게임 종료 처리
    /// </summary>
    private void EndGame(int winner)
    {
        if (!localPlayer.isMaster) return;

        gameState = GAME_STATE_FINISHED;
        winnerTeam = winner;

        // 네트워크 동기화
        RequestSerialization();

        // 게임 결과 표시
        ShowGameResult();

        Debug.Log("게임 종료: 승리 팀 - " + (winner == WINNER_HUNTER ? "사냥꾼" : "벌레"));
    }

    /// <summary>
    /// 게임 결과 표시
    /// </summary>
    private void ShowGameResult()
    {
        // 승리 패널 활성화
        if (winnerPanel != null)
        {
            winnerPanel.SetActive(true);
        }

        // 승리 팀 텍스트 설정
        if (winnerText != null)
        {
            if (winnerTeam == WINNER_HUNTER)
            {
                winnerText.text = "사냥꾼 팀 승리!\n모든 벌레를 잡았습니다.";
                winnerText.color = new Color(1f, 0.5f, 0f); // 주황색
            }
            else if (winnerTeam == WINNER_BUG)
            {
                winnerText.text = "벌레 팀 승리!\n시간 내에 살아남았습니다.";
                winnerText.color = new Color(0.2f, 0.8f, 0.2f); // 초록색
            }
        }

        // 게임 상태 텍스트 업데이트
        UpdateGameStatusText();

        // 5초 후 로비로 돌아가기
        SendCustomEventDelayedSeconds("ReturnToLobby", 5.0f);
    }

    /// <summary>
    /// 로비로 돌아가기
    /// </summary>
    public void ReturnToLobby()
    {
        // UI 비활성화
        if (gameUICanvas != null)
        {
            gameUICanvas.gameObject.SetActive(false);
        }

        if (winnerPanel != null)
        {
            winnerPanel.SetActive(false);
        }

        // GameStarter에 게임 재시작 요청
        if (gameStarter != null)
        {
            gameStarter.ResetGame();
        }

        // 마스터 클라이언트만 게임 상태 초기화
        if (localPlayer.isMaster)
        {
            gameState = GAME_STATE_WAITING;
            gameTimer = 0f;
            winnerTeam = WINNER_NONE;

            // 네트워크 동기화
            RequestSerialization();
        }
    }
}