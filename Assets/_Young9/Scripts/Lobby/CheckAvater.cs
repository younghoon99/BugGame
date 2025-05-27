using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;
using System;
//  양팀 준비 완료시 텔레포트 하고 팀별 능력 설정 해야함
//SetTeamAbilities();


/// <summary>
/// 플레이어가 팀별 룸에 있을 때 준비 상태를 관리하는 클래스
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CheckAvater : UdonSharpBehaviour
{
    private Ability ability;
    // 팀 상수 정의
    private const int TEAM_HUNTER = 0;  // 사냥꾼 팀
    private const int TEAM_BUG = 1;     // 벌레 팀

    // UI 요소
    [Header("UI 요소")]
    [SerializeField] private GameObject hunterReadyButton; // 사냥꾼 팀 준비 버튼
    [SerializeField] private GameObject bugReadyButton; // 벌레 팀 준비 버튼
    [SerializeField] private TextMeshProUGUI teamReadyStatusText; // 팀 준비 현황 텍스트
    [SerializeField] private TextMeshProUGUI teamReadyStatusText2; // 팀 준비 현황 텍스트
    [SerializeField] private TextMeshProUGUI gameMessageText; // 게임 안내 메시지 텍스트

    // 게임 관리자 참조
    [Header("게임 관리자 참조")]
    [SerializeField] private UdonBehaviour checkPlayerBehaviour; // CheckPlayer 스크립트 참조
    [SerializeField] private UdonBehaviour gameStarterBehaviour; // GameStarter 스크립트 참조
    [SerializeField] private GameManager gameManager; // GameManager 참조

    // 텔레포트 관련 변수
    [Header("텔레포트 설정")]
    [SerializeField] private Transform gameMapSpawnPoint; // 게임 맵 스폰 위치
    [SerializeField] private float hunterDelayTime = 15.0f; // 사냥꾼 팀 딜레이 시간(초)
    [SerializeField] private int countdownDuration = 15; // 카운트다운 시간(초)

    // 로컬 변수
    private VRCPlayerApi localPlayer;
    [UdonSynced, FieldChangeCallback(nameof(TeamReady))] private bool[] _teamReady = new bool[2]; // 0: 사냥꾼 팀 준비, 1: 벌레 팀 준비
    private int currentCountdown; // 현재 카운트다운 값

    public bool[] TeamReady
    {
        get => _teamReady;
        set
        {
            _teamReady = value;
            UpdateTeamReadyStatusText();
        }
    }

    void Start()
    {
        ability = GameObject.Find("Ability").GetComponent<Ability>();
        gameStarterBehaviour = GameObject.Find("GameManager").GetComponent<UdonBehaviour>();
        checkPlayerBehaviour = GameObject.Find("GameManager").GetComponent<UdonBehaviour>();
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        // 로컬 플레이어 정보 초기화
        localPlayer = Networking.LocalPlayer;

        // 초기에는 준비 버튼 활성화
        if (hunterReadyButton != null)
        {
            hunterReadyButton.SetActive(true);
        }

        if (bugReadyButton != null)
        {
            bugReadyButton.SetActive(true);
        }

        // 팀 준비 상태 초기화
        _teamReady[TEAM_HUNTER] = false;
        _teamReady[TEAM_BUG] = false;

        // 준비 현황 텍스트 초기화
        UpdateTeamReadyStatusText();
    }

    // 트리거 존 관련 코드 제거

    /// <summary>
    /// 사냥꾼 팀 준비 버튼 클릭 처리
    /// </summary>
    public void OnHunterReadyButtonClick()
    {
        if (!Utilities.IsValid(localPlayer)) return;

        Debug.Log("사냥꾼 팀 준비 버튼 클릭됨!");

        // 마스터 클라이언트인 경우 직접 변경
        if (localPlayer.isMaster)
        {
            // 사냥꾼 팀 준비 상태 변경
            _teamReady[TEAM_HUNTER] = !_teamReady[TEAM_HUNTER];

            // 네트워크 동기화
            RequestSerialization();

            // UI 업데이트
            UpdateTeamReadyStatusText();

            // 모든 팀이 준비되었는지 확인
            AreAllTeamsReady();

            Debug.Log("사냥꾼 팀 준비 상태 변경: " + (_teamReady[TEAM_HUNTER] ? "준비완료" : "준비취소"));
        }
        // 마스터가 아닌 경우 마스터에게 요청
        else
        {
            // 이벤트를 통해 마스터에게 준비 상태 변경 요청
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "RequestHunterTeamReadyToggle");
            Debug.Log("마스터에게 사냥꾼 팀 준비 상태 변경 요청");
        }
    }

    /// <summary>
    /// 벌레 팀 준비 버튼 클릭 처리
    /// </summary>
    public void OnBugReadyButtonClick()
    {
        if (!Utilities.IsValid(localPlayer)) return;

        Debug.Log("벌레 팀 준비 버튼 클릭됨!");

        // 마스터 클라이언트인 경우 직접 변경
        if (localPlayer.isMaster)
        {
            // 벌레 팀 준비 상태 변경
            _teamReady[TEAM_BUG] = !_teamReady[TEAM_BUG];

            // 네트워크 동기화
            RequestSerialization();

            // UI 업데이트
            UpdateTeamReadyStatusText();

            // 모든 팀이 준비되었는지 확인
            AreAllTeamsReady();

            Debug.Log("벌레 팀 준비 상태 변경: " + (_teamReady[TEAM_BUG] ? "준비완료" : "준비취소"));
        }
        // 마스터가 아닌 경우 마스터에게 요청
        else
        {
            // 이벤트를 통해 마스터에게 준비 상태 변경 요청
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "RequestBugTeamReadyToggle");
            Debug.Log("마스터에게 벌레 팀 준비 상태 변경 요청");
        }
    }

    /// <summary>
    /// 사냥꾼 팀 준비 상태 변경 요청
    /// </summary>
    public void RequestHunterTeamReadyToggle()
    {
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        Debug.Log("사냥꾼 팀 준비 상태 변경 요청 받음");

        // 사냥꾼 팀 준비 상태 변경
        _teamReady[TEAM_HUNTER] = !_teamReady[TEAM_HUNTER];

        // 네트워크 동기화
        RequestSerialization();

        // UI 업데이트
        UpdateTeamReadyStatusText();

        // 모든 팀이 준비되었는지 확인
        AreAllTeamsReady();

        Debug.Log("사냥꾼 팀 준비 상태 변경: " + (_teamReady[TEAM_HUNTER] ? "준비완료" : "준비취소"));
    }

    /// <summary>
    /// 벌레 팀 준비 상태 변경 요청
    /// </summary>
    public void RequestBugTeamReadyToggle()
    {
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        Debug.Log("벌레 팀 준비 상태 변경 요청 받음");

        // 벌레 팀 준비 상태 변경
        _teamReady[TEAM_BUG] = !_teamReady[TEAM_BUG];

        // 네트워크 동기화
        RequestSerialization();

        // UI 업데이트
        UpdateTeamReadyStatusText();

        // 모든 팀이 준비되었는지 확인
        AreAllTeamsReady();

        Debug.Log("벌레 팀 준비 상태 변경: " + (_teamReady[TEAM_BUG] ? "준비완료" : "준비취소"));
    }

    /// <summary>
    /// 네트워크 직렬화 후 처리
    /// </summary>
    public override void OnDeserialization()
    {
        Debug.Log("네트워크 동기화 받음 - 팀 준비 상태 변경");

        // UI 업데이트
        UpdateTeamReadyStatusText();
    }

    /// <summary>
    /// 팀 준비 현황 텍스트 업데이트
    /// </summary>
    private void UpdateTeamReadyStatusText()
    {
        // 양쪽 팀의 준비 상태 확인
        string hunterStatus = _teamReady[TEAM_HUNTER] ? "준비완료" : "대기중";
        string bugStatus = _teamReady[TEAM_BUG] ? "준비완료" : "대기중";

        // 첫 번째 텍스트 업데이트
        if (teamReadyStatusText != null)
        {
            teamReadyStatusText.text = string.Format("팀 준비 현황:\n사냥꾼 팀: {0}\n벌레 팀: {1}", hunterStatus, bugStatus);
            Debug.Log("텍스트 1 업데이트: " + teamReadyStatusText.text);
        }

        // 두 번째 텍스트 업데이트
        if (teamReadyStatusText2 != null)
        {
            teamReadyStatusText2.text = string.Format("팀 준비 현황:\n사냥꾼 팀: {0}\n벌레 팀: {1}", hunterStatus, bugStatus);
            Debug.Log("텍스트 2 업데이트: " + teamReadyStatusText2.text);
        }
    }

    /// <summary>
    /// 모든 팀이 준비되었는지 확인
    /// </summary>
    public bool AreAllTeamsReady()
    {
        bool allReady = _teamReady[TEAM_HUNTER] && _teamReady[TEAM_BUG];

        // 모든 팀이 준비되었고 마스터 클라이언트인 경우 게임 시작 요청
        if (allReady && Utilities.IsValid(localPlayer) && localPlayer.isMaster)
        {
            // 카운트다운 시간으로 사냥꾼 팀 딜레이 설정
            hunterDelayTime = countdownDuration;
            SendCustomEventDelayedSeconds("TeleportHunterTeam", hunterDelayTime);
            Debug.Log("사냥꾼 팀 텔레포트 " + hunterDelayTime + "초 후 실행 예약");

            // 모든 클라이언트에서 카운트다운 시작
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "StartCountdown");
        }

        // 벌레 팀은 바로 텔레포트
        if (allReady && Utilities.IsValid(localPlayer) && Utilities.IsValid(checkPlayerBehaviour))
        {
            // 자신이 벌레 팀인 경우에만 바로 텔레포트
            object teamObj = checkPlayerBehaviour.GetProgramVariable("localPlayerTeam");
            if (teamObj != null)
            {
                int localPlayerTeam = (int)teamObj;
                if (localPlayerTeam == TEAM_BUG)
                {
                    TeleportToBugTeamSpawn();
                }
            }
            else
            {
                Debug.LogWarning("localPlayerTeam 변수를 찾을 수 없습니다.");
            }
        }

        // 팀 능력 적용
        if (Utilities.IsValid(ability))
        {
            ability.SetTeamAbilities();
            Debug.Log("팀별 능력 적용 완료");
        }
        else
        {
            Debug.LogWarning("Ability 컴포넌트가 없습니다.");
        }
        return allReady;
    }

    /// <summary>
    /// 벌레 팀을 게임 맵으로 텔레포트
    /// </summary>
    private void TeleportToBugTeamSpawn()
    {
        if (!Utilities.IsValid(localPlayer) || gameMapSpawnPoint == null) return;

        localPlayer.TeleportTo(
            gameMapSpawnPoint.position,
            gameMapSpawnPoint.rotation,
            VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
            true
        );

        Debug.Log("벌레 팀 게임 맵으로 텔레포트 완료");
    }

    /// <summary>
    /// 사냥꾼 팀을 게임 맵으로 텔레포트 (30초 후 자동 호출)
    /// </summary>
    public void TeleportHunterTeam()
    {
        if (!Utilities.IsValid(localPlayer) || !Utilities.IsValid(checkPlayerBehaviour) || gameMapSpawnPoint == null)
        {
            Debug.LogWarning("텔레포트에 필요한 객체가 null입니다.");
            return;
        }

        // 자신이 사냥꾼 팀인 경우에만 텔레포트
        object teamObj = checkPlayerBehaviour.GetProgramVariable("localPlayerTeam");
        if (teamObj == null)
        {
            Debug.LogWarning("localPlayerTeam 변수를 찾을 수 없습니다.");
            return;
        }

        int localPlayerTeam = (int)teamObj;
        if (localPlayerTeam == TEAM_HUNTER)
        {
            localPlayer.TeleportTo(
                gameMapSpawnPoint.position,
                gameMapSpawnPoint.rotation,
                VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                true
            );

            Debug.Log("사냥꾼 팀 게임 맵으로 텔레포트 완료");

            // 게임 시작 신호 보내기 (마스터 클라이언트인 경우)
            if (localPlayer.isMaster && gameManager != null)
            {
                // 게임 시작 (3분 타이머 시작)
                gameManager.StartGame();
                Debug.Log("게임 시작 신호 전송 - 3분 타이머 시작");
            }
        }
    }

    /// <summary>
    /// 팀 준비 상태 초기화
    /// </summary>
    public void ResetTeamReady()
    {
        if (!Utilities.IsValid(localPlayer) || !localPlayer.isMaster) return;

        _teamReady[TEAM_HUNTER] = false;
        _teamReady[TEAM_BUG] = false;

        // 네트워크 동기화
        RequestSerialization();

        // UI 업데이트
        UpdateTeamReadyStatusText();

        Debug.Log("팀 준비 상태 초기화");
    }

    /// <summary>
    /// 카운트다운 시작
    /// </summary>
    public void StartCountdown()
    {
        currentCountdown = countdownDuration;
        UpdateCountdown();        // 첫 표시
    }

    /// <summary>
    /// 카운트다운 업데이트 (1초마다 호출)
    /// </summary>
    public void UpdateCountdown()
    {
        if (!Utilities.IsValid(checkPlayerBehaviour) || gameMessageText == null) return;

        // 팀 판별
        object teamObj = checkPlayerBehaviour.GetProgramVariable("localPlayerTeam");
        int myTeam = teamObj != null ? (int)teamObj : -1;

        string msg = (myTeam == TEAM_BUG)
            ? $"{currentCountdown}초 뒤 사냥꾼이 찾아옵니다."
            : $"{currentCountdown}초 뒤 벌레를 박멸하세요.";

        gameMessageText.text = msg;
        Debug.Log("카운트다운 메시지 업데이트: " + msg);

        currentCountdown--;
        if (currentCountdown > 0)
            SendCustomEventDelayedSeconds("UpdateCountdown", 1f);
        else
            gameMessageText.text = "";     // 끝나면 숨김
    }
}
