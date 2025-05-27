using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using System;

/// <summary>
/// 플레이어 정보를 관리하고 게임 팀을 배정하는 클래스
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CheckPlayer : UdonSharpBehaviour
{
    // 팀 상수 정의
    public const int TEAM_HUNTER = 0;  // 사냥꾼 팀
    public const int TEAM_BUG = 1;     // 벌레 팀

    // 플레이어 정보 저장 배열
    [UdonSynced] private int[] playerTeams = new int[16]; // 최대 16명까지 지원
    [UdonSynced] private bool[] playerReady = new bool[16]; // 플레이어 준비 상태
    [UdonSynced] private int playerCount = 0;
    [UdonSynced] private int readyPlayerCount = 0; // 준비한 플레이어 수
    [UdonSynced] private bool gameStarted = false;

    // 로컬 플레이어 정보
    private VRCPlayerApi localPlayer;
    private int localPlayerId;
    private int localPlayerTeam = -1; // 초기값 -1 (팀 미배정)

    // 게임 설정
    [Header("게임 설정")]
    [SerializeField] private int maxBugPlayers = 4;
    [SerializeField] private int maxHunterPlayers = 1;
    [SerializeField] private int minTotalPlayers = 1; // 최소 필요 인원

    // UI 관련 요소
    [Header("UI 요소")]
    [SerializeField] private TextMeshProUGUI playerNamesText; // 플레이어 이름 목록
    [SerializeField] private TextMeshProUGUI bugTeamText; // 벌레 팀 이름 목록
    [SerializeField] private TextMeshProUGUI hunterTeamText; // 사냥꾼 팀 이름 목록
    [SerializeField] private GameObject startGameButton; // 게임 시작 버튼
    [SerializeField] private GameObject readyButton; // 플레이어 준비 버튼
    [SerializeField] private TextMeshProUGUI readyPlayersText; // 준비한 플레이어 표시


    // 디버그용 텍스트
    [Header("디버그")]
    [SerializeField] private TextMeshProUGUI debugText;
    
    // 스폰 위치 설정
    [Header("스폰 위치")]
    [SerializeField] private Transform spectatorPosition; // 관전자 영역 위치

    // GameStarter 참조 변수 추가
    [SerializeField] private GameStarter gameStarter;

    void Start()
    {
        // 로컬 플레이어 정보 초기화
        localPlayer = Networking.LocalPlayer;

        if (Utilities.IsValid(localPlayer))
        {
            localPlayerId = localPlayer.playerId;

            // 디버그 텍스트 업데이트
            UpdateDebugText();

            // 마스터 클라이언트인 경우 초기 설정
            if (localPlayer.isMaster)
            {
                InitializeGame();
            }

            // 준비 버튼 활성화
            if (readyButton != null)
            {
                readyButton.SetActive(true);
            }

            // 시작 버튼 비활성화 (마스터만 사용 가능)
            if (startGameButton != null)
            {
                startGameButton.SetActive(false);
            }

            // UI 업데이트
            UpdatePlayerCountUI();
        }
    }

    /// <summary>
    /// 게임 초기 설정
    /// </summary>
    public void InitializeGame()
    {
        // 마스터 클라이언트만 실행
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        // 플레이어 목록 초기화
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        players = VRCPlayerApi.GetPlayers(players);
        playerCount = players.Length;

        // 팀 배정 초기화
        for (int i = 0; i < playerTeams.Length; i++)
        {
            playerTeams[i] = -1; // 미배정 상태로 초기화
            playerReady[i] = false; // 준비 상태 초기화
        }

        // 준비 플레이어 수 초기화
        readyPlayerCount = 0;

        // 게임 시작 상태 초기화
        gameStarted = false;

        // 변경사항 네트워크 동기화
        RequestSerialization();
    }

    /// <summary>
    /// 플레이어 팀 배정 (마스터 클라이언트에서만 호출)
    /// </summary>
    private void AssignTeams(VRCPlayerApi[] players)
    {
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        int hunterCount = 0;
        int bugCount = 0;

        // 플레이어 수에 따라 팀 배정 (준비한 플레이어만 배정)
        for (int i = 0; i < players.Length; i++)
        {
            if (!Utilities.IsValid(players[i])) continue;

            int playerId = players[i].playerId;
            int playerIndex = GetPlayerIndexById(playerId);

            // 준비하지 않은 플레이어는 팀 배정에서 제외
            if (!playerReady[playerIndex])
            {
                playerTeams[playerIndex] = -1; // 미배정 상태
                continue;
            }

            // 사냥꾼 팀 우선 배정 (1명)
            if (hunterCount < maxHunterPlayers)
            {
                playerTeams[playerIndex] = TEAM_HUNTER;
                hunterCount++;
            }
            // 나머지는 벌레 팀으로 배정
            else if (bugCount < maxBugPlayers)
            {
                playerTeams[playerIndex] = TEAM_BUG;
                bugCount++;
            }
            // 초과 인원은 관전자로 설정
            else
            {
                playerTeams[playerIndex] = -1;
            }


        }
    }

    /// <summary>
    /// 플레이어 ID로 배열 인덱스 찾기
    /// </summary>
    private int GetPlayerIndexById(int playerId)
    {
        // 간단한 해시 함수로 인덱스 계산 (충돌 가능성 있음)
        return playerId % playerTeams.Length;
    }

    /// <summary>
    /// 네트워크 직렬화 후 처리
    /// </summary>
    public override void OnDeserialization()
    {
        // 로컬 플레이어 팀 정보 업데이트
        if (Utilities.IsValid(localPlayer))
        {
            localPlayerTeam = playerTeams[GetPlayerIndexById(localPlayerId)];
            UpdatePlayerCountUI();
            UpdateDebugText();

            // 게임이 시작되었으면 게임 시작 처리
            if (gameStarted)
            {
                OnGameStart();
            }

        }
    }

    /// <summary>
    /// 플레이어 입장 시 호출
    /// </summary>
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return;

        // 마스터 클라이언트만 처리
        if (Utilities.IsValid(localPlayer) && localPlayer.isMaster)
        {
            // 새 플레이어 정보 업데이트
            int newPlayerId = player.playerId;
            int playerIndex = GetPlayerIndexById(newPlayerId);

            // 기본적으로 팀 미배정 상태
            playerTeams[playerIndex] = -1;

            // 기본적으로 준비 안됨 상태
            playerReady[playerIndex] = false;

            // 플레이어 카운트 업데이트
            playerCount = VRCPlayerApi.GetPlayerCount();

            // 변경사항 네트워크 동기화
            RequestSerialization();
        }

        UpdatePlayerCountUI();
        UpdateDebugText();
    }

    /// <summary>
    /// 플레이어 퇴장 시 호출
    /// </summary>
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return;

        // 마스터 클라이언트만 처리
        if (Utilities.IsValid(localPlayer) && localPlayer.isMaster)
        {
            // 퇴장한 플레이어 정보 업데이트
            int leftPlayerId = player.playerId;
            int playerIndex = GetPlayerIndexById(leftPlayerId);

            // 팀에서 제거
            playerTeams[playerIndex] = -1;

            // 준비 상태가 있었다면 준비 플레이어 수 감소
            if (playerReady[playerIndex])
            {
                readyPlayerCount--;
                if (readyPlayerCount < 0) readyPlayerCount = 0; // 안전장치
            }

            // 준비 상태 초기화
            playerReady[playerIndex] = false;

            // 플레이어 카운트 업데이트
            playerCount = VRCPlayerApi.GetPlayerCount();

            // 변경사항 네트워크 동기화
            RequestSerialization();
        }

        UpdatePlayerCountUI();
        UpdateDebugText();
    }

    /// <summary>
    /// 현재 인원 UI 업데이트
    /// </summary>
    private void UpdatePlayerCountUI()
    {
        int currentPlayers = VRCPlayerApi.GetPlayerCount();
        int requiredPlayers = maxBugPlayers + maxHunterPlayers;



        // 준비한 플레이어 수 표시
        if (readyPlayersText != null)
        {
            readyPlayersText.text = string.Format("준비 인원: {0}/{1}", readyPlayerCount, currentPlayers);
        }

        // 플레이어 이름 목록 표시
        if (playerNamesText != null)
        {
            // 플레이어 목록 가져오기
            VRCPlayerApi[] players = new VRCPlayerApi[currentPlayers];
            players = VRCPlayerApi.GetPlayers(players);

            // 플레이어 이름 목록 생성
            string playerNames = "현재 접속자:";
            for (int i = 0; i < players.Length; i++)
            {
                if (Utilities.IsValid(players[i]))
                {
                    int playerIndex = GetPlayerIndexById(players[i].playerId);
                    string readyStatus = playerReady[playerIndex] ? "[준비완료]" : "[대기중]";
                    playerNames += "\n- " + players[i].displayName + " " + readyStatus;
                }
            }

            playerNamesText.text = playerNames;
        }

        // 게임 시작 버튼 활성화/비활성화 (마스터만 사용 가능)
        if (startGameButton != null && localPlayer.isMaster)
        {
            // 마스터 클라이언트이고 준비한 플레이어가 최소 인원 이상일 때만 활성화
            bool canStart = readyPlayerCount >= minTotalPlayers && !gameStarted;
            startGameButton.SetActive(canStart);
        }
    }

    /// <summary>
    /// 게임 시작 버튼 클릭 처리
    /// </summary>
    public void OnStartGameButtonClick()
    {
        // 마스터 클라이언트만 실행
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        // 준비한 플레이어 수 체크
        if (readyPlayerCount < minTotalPlayers)
        {
            // 준비한 인원이 부족하면 시작하지 않음
            Debug.Log("준비한 인원이 부족합니다: " + readyPlayerCount + "/" + minTotalPlayers);
            return;
        }

        // 플레이어 목록 가져오기
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        players = VRCPlayerApi.GetPlayers(players);

        // 팀 배정 (준비한 플레이어만 배정)
        AssignTeams(players);

        // 팀 배정 확인 (1명일 경우 배정 유지 - 벌레로 배정되도록 수정)
        if (readyPlayerCount == 1)
        {
            // 준비한 플레이어 찾기
            for (int i = 0; i < players.Length; i++)
            {
                if (!Utilities.IsValid(players[i])) continue;

                int playerIndex = GetPlayerIndexById(players[i].playerId);
                if (playerReady[playerIndex])
                {
                    // 기존 배정 유지 (사냥꾼으로 강제 배정하지 않음)
                    playerTeams[playerIndex] = TEAM_HUNTER; // 한 명일 경우 무조건 사냥꾼으로 배정
                    // 벌레로 배정되도록 수정
                    // playerTeams[playerIndex] = TEAM_BUG;
                    Debug.Log("한 명 플레이어: " + (playerTeams[playerIndex] == TEAM_BUG ? "벌레" : "사냥꾼") + "으로 배정 - " + players[i].displayName);
                    break;
                }
            }
        }

        // 로컬 플레이어 팀 정보 갱신
        for (int i = 0; i < players.Length; i++)
        {
            if (Utilities.IsValid(players[i]) && players[i].playerId == localPlayerId)
            {
                int playerIndex = GetPlayerIndexById(localPlayerId);
                localPlayerTeam = playerTeams[playerIndex];
                Debug.Log("로컬 플레이어 팀 업데이트: " + (localPlayerTeam == TEAM_HUNTER ? "사냥꾼" : "벌레"));
                break;
            }
        }

        // 게임 시작 상태 설정
        gameStarted = true;

        // 디버그 텍스트 업데이트
        UpdateDebugText();

        // 팀 표시 업데이트
        UpdateTeamDisplay();

        // 로컬에서 게임 시작 처리
        OnGameStart();

        // 변경사항 네트워크 동기화
        RequestSerialization();
        if (gameStarter != null)
        {
            gameStarter.StartGame();
        }
    }

    /// <summary>
    /// 게임 시작 처리
    /// </summary>
    private void OnGameStart()
    {
        // 게임 시작 시 UI 업데이트
        if (startGameButton != null)
        {
            startGameButton.SetActive(false);
        }

        // 준비 버튼 비활성화
        if (readyButton != null)
        {
            readyButton.SetActive(false);
        }

        // 팀 구성 표시 업데이트
        UpdateTeamDisplay();

        // 여기에 게임 시작 시 필요한 처리 추가
        Debug.Log("게임이 시작되었습니다!");
    }

    /// <summary>
    /// 팀별 플레이어 이름 표시 업데이트
    /// </summary>
    private void UpdateTeamDisplay()
    {
        // 플레이어 목록 가져오기
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        players = VRCPlayerApi.GetPlayers(players);

        // 팀별 플레이어 이름 목록 생성
        string bugTeamNames = "벌레 팀:";
        string hunterTeamNames = "사냥꾼 팀:";

        // 플레이어 팀별로 분류
        for (int i = 0; i < players.Length; i++)
        {
            if (!Utilities.IsValid(players[i])) continue;

            int playerId = players[i].playerId;
            int playerIndex = GetPlayerIndexById(playerId);
            int team = playerTeams[playerIndex];

            // 팀에 따라 이름 추가
            if (team == TEAM_BUG)
            {
                bugTeamNames += "\n- " + players[i].displayName;
            }
            else if (team == TEAM_HUNTER)
            {
                hunterTeamNames += "\n- " + players[i].displayName;
            }
        }

        // UI 업데이트
        if (bugTeamText != null)
        {
            bugTeamText.text = bugTeamNames;
        }

        if (hunterTeamText != null)
        {
            hunterTeamText.text = hunterTeamNames;
        }
    }

    /// <summary>
    /// 디버그 텍스트 업데이트
    /// </summary>
    private void UpdateDebugText()
    {
        if (debugText == null || !Utilities.IsValid(localPlayer)) return;

        string teamName = "미배정";
        if (localPlayerTeam == TEAM_HUNTER) teamName = "사냥꾼";
        else if (localPlayerTeam == TEAM_BUG) teamName = "벌레";

        string debugInfo = string.Format(
            "플레이어 ID: {0}\n" +
            "플레이어 이름: {1}\n" +
            "마스터: {2}\n" +
            "VR 사용: {3}\n" +
            "팀: {4}\n" +
            "총 플레이어 수: {5}\n" +
            "게임 시작: {6}",
            localPlayer.playerId,
            localPlayer.displayName,
            localPlayer.isMaster ? "예" : "아니오",
            localPlayer.IsUserInVR() ? "예" : "아니오",
            teamName,
            VRCPlayerApi.GetPlayerCount(),
            gameStarted ? "예" : "아니오"
        );

        debugText.text = debugInfo;
    }

    /// <summary>
    /// 로컬 플레이어의 팀 정보 반환
    /// </summary>
    public int GetLocalPlayerTeam()
    {
        return localPlayerTeam;
    }

    /// <summary>
    /// 특정 플레이어의 팀 정보 반환
    /// </summary>
    public int GetPlayerTeam(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) return -1;

        int playerId = player.playerId;
        int playerIndex = GetPlayerIndexById(playerId);

        return playerTeams[playerIndex];
    }

    /// <summary>
    /// 플레이어가 사냥꾼인지 확인
    /// </summary>
    public bool IsPlayerHunter(VRCPlayerApi player)
    {
        return GetPlayerTeam(player) == TEAM_HUNTER;
    }

    /// <summary>
    /// 플레이어가 벌레인지 확인
    /// </summary>
    public bool IsPlayerBug(VRCPlayerApi player)
    {
        return GetPlayerTeam(player) == TEAM_BUG;
    }

    /// <summary>
    /// 플레이어 준비 버튼 클릭 처리
    /// </summary>
    public void OnReadyButtonClick()
    {
        if (!Utilities.IsValid(localPlayer) || gameStarted) return;

        // 로컬 플레이어 준비 상태 변경
        int playerIndex = GetPlayerIndexById(localPlayerId);

        // 마스터 클라이언트인 경우 직접 변경
        if (localPlayer.isMaster)
        {
            // 준비 상태 토글
            bool newReadyState = !playerReady[playerIndex];
            playerReady[playerIndex] = newReadyState;

            // 준비 플레이어 수 업데이트
            if (newReadyState)
            {
                readyPlayerCount++;
            }
            else
            {
                readyPlayerCount--;
                if (readyPlayerCount < 0) readyPlayerCount = 0; // 안전장치
            }

            // 네트워크 동기화
            RequestSerialization();

            Debug.Log("로컬 플레이어 준비 상태 변경: " + (newReadyState ? "준비완료" : "준비취소"));
        }
        // 마스터가 아닌 경우 마스터에게 요청
        else
        {
            // 이벤트를 통해 마스터에게 준비 상태 변경 요청
            SendCustomNetworkEvent(NetworkEventTarget.Owner, "RequestReadyStateChange");
            Debug.Log("마스터에게 준비 상태 변경 요청 전송");
        }

        // UI 업데이트
        UpdatePlayerCountUI();
        UpdateDebugText();
    }

    /// <summary>
    /// 마스터가 아닌 플레이어의 준비 상태 변경 요청 처리
    /// </summary>
    public void RequestReadyStateChange()
    {
        // 마스터만 처리
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        // 요청을 보낸 플레이어 찾기
        VRCPlayerApi sender = Networking.GetOwner(gameObject);
        if (!Utilities.IsValid(sender)) return;

        int senderPlayerId = sender.playerId;
        int playerIndex = GetPlayerIndexById(senderPlayerId);

        // 준비 상태 토글
        bool newReadyState = !playerReady[playerIndex];
        playerReady[playerIndex] = newReadyState;

        // 준비 플레이어 수 업데이트
        if (newReadyState)
        {
            readyPlayerCount++;
        }
        else
        {
            readyPlayerCount--;
            if (readyPlayerCount < 0) readyPlayerCount = 0; // 안전장치
        }

        // 네트워크 동기화
        RequestSerialization();

        Debug.Log("플레이어 준비 상태 변경: " + sender.displayName + " - " + (newReadyState ? "준비완료" : "준비취소"));

        // UI 업데이트
        UpdatePlayerCountUI();
    }


    // 플레이어 팀 확인 메서드
    public int GetPlayerTeam(int playerId)
    {
        if (playerId >= 0 && playerId < playerTeams.Length)
        {
            return playerTeams[playerId];
        }
        return -1; // 유효하지 않은 플레이어 ID
    }

    /// <summary>
    /// 플레이어 탈락 처리 (망치로 타격되었을 때 호출)
    /// </summary>
    public void EliminatePlayer(int playerId)
    {
        if (!Networking.IsMaster) return; // 마스터 클라이언트만 실행

        // 플레이어 탈락 처리 로직
        Debug.Log($"플레이어 {playerId}가 탈락했습니다.");

        // 플레이어 정보 가져오기
        VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
        
        if (Utilities.IsValid(player))
        {
            // 플레이어 팀 확인
            int playerTeam = GetPlayerTeam(playerId);
            
            // 탈락 플레이어가 벌레 팀인 경우
            if (playerTeam == TEAM_BUG)
            {
                // 탈락된 플레이어 리스폰
                player.Respawn();
                
                // 관전자 영역으로 텔레포트 (spectatorPosition이 있는 경우)
                if (spectatorPosition != null)
                {
                    player.TeleportTo(
                        spectatorPosition.position,
                        spectatorPosition.rotation,
                        VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                        true
                    );
                }
                
                // 탈락 플레이어가 로컬 플레이어인 경우 탈락 메시지 표시
                if (player.isLocal)
                {
                    Debug.Log("당신은 탈락되었습니다!");
                    // 탈락 효과나 UI 표시 추가 가능
                }
            }
        }
    }
}
