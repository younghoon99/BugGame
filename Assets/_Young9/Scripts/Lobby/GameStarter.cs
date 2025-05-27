using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.Udon;
using System;

/// <summary>
/// 게임 시작 시 카운트다운 및 팀별 텔레포트 처리를 담당하는 클래스
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameStarter : UdonSharpBehaviour
{
    // 팀 상수 정의
    private const int TEAM_HUNTER = 0;  // 사냥꾼 팀
    private const int TEAM_BUG = 1;     // 벌레 팀

    // 카운트다운 관련 변수
    [Header("카운트다운 설정")]
    [SerializeField] private Canvas countdownCanvas; // 카운트다운 캔버스 (Screen Space - Overlay)
    [SerializeField] private TextMeshProUGUI countdownText; // 카운트다운 텍스트
    [SerializeField] private float countdownDuration = 3.0f; // 카운트다운 시간 (초)

    // 텔레포트 관련 변수
    [Header("텔레포트 위치")]
    [SerializeField] private Transform hunterSpawnPoint; // 사냥꾼 팀 스폰 위치
    [SerializeField] private Transform bugSpawnPoint; // 벌레 팀 스폰 위치

    // 게임 관리자 참조
    [Header("게임 관리자 참조")]
    [SerializeField] private UdonBehaviour checkPlayerBehaviour; // CheckPlayer 스크립트 참조
    [SerializeField] private UdonBehaviour abilityBehaviour; // Ability 스크립트 참조

    // 로컬 변수
    private VRCPlayerApi localPlayer;
    private float countdownTimer;
    private bool isCountingDown = false;
    [UdonSynced] private bool gameStarted = false;

    void Start()
    {
        // 로컬 플레이어 정보 초기화
        localPlayer = Networking.LocalPlayer;

        // 카운트다운 캔버스 비활성화
        if (countdownCanvas != null)
        {
            countdownCanvas.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 게임 시작 처리 (CheckPlayer 스크립트에서 호출)
    /// </summary>
    public void StartGame()
    {
        if (!Utilities.IsValid(localPlayer)) return;

        // 마스터 클라이언트만 게임 시작 상태 설정
        if (localPlayer.isMaster && !gameStarted)
        {
            gameStarted = true;
            RequestSerialization();
        }

        // 카운트다운 시작
        StartCountdown();
    }

    /// <summary>
    /// 네트워크 직렬화 후 처리
    /// </summary>
    public override void OnDeserialization()
    {
        // 게임이 시작되었으면 카운트다운 시작
        if (gameStarted && !isCountingDown)
        {
            StartCountdown();
        }
    }

    /// <summary>
    /// 카운트다운 시작
    /// </summary>
    private void StartCountdown()
    {
        if (isCountingDown) return;

        // 카운트다운 캔버스 활성화
        if (countdownCanvas != null)
        {
            countdownCanvas.gameObject.SetActive(true);
        }

        // 카운트다운 시작
        countdownTimer = countdownDuration;
        isCountingDown = true;

        // 초기 카운트다운 텍스트 설정
        UpdateCountdownText();

        Debug.Log("카운트다운 시작: " + countdownDuration + "초");
    }

    /// <summary>
    /// 매 프레임마다 호출
    /// </summary>
    private void Update()
    {
        // 카운트다운 중이면 타이머 업데이트
        if (isCountingDown)
        {
            countdownTimer -= Time.deltaTime;

            // 카운트다운 텍스트 업데이트 (정수 부분이 변경될 때만)
            if (Mathf.FloorToInt(countdownTimer + 1) != Mathf.FloorToInt(countdownTimer + 1 + Time.deltaTime))
            {
                UpdateCountdownText();
            }

            // 카운트다운 종료
            if (countdownTimer <= 0)
            {
                FinishCountdown();
            }
        }
    }

    /// <summary>
    /// 카운트다운 텍스트 업데이트
    /// </summary>
    private void UpdateCountdownText()
    {
        if (countdownText != null)
        {
            int secondsLeft = Mathf.CeilToInt(countdownTimer);
            countdownText.text = secondsLeft.ToString();

            // 텍스트 크기 애니메이션
            countdownText.transform.localScale = Vector3.one * (1.5f + (countdownDuration - countdownTimer) * 0.2f);
        }
    }

    /// <summary>
    /// 카운트다운 종료 및 텔레포트 처리
    /// </summary>
    private void FinishCountdown()
    {
        isCountingDown = false;

        // 카운트다운 캔버스 비활성화
        if (countdownCanvas != null)
        {
            countdownCanvas.gameObject.SetActive(false);
        }

        // 플레이어 팀에 따라 텔레포트
        TeleportPlayerToTeamSpawn();

        // 팀별 능력 적용
        if (abilityBehaviour != null)
        {
            abilityBehaviour.SendCustomEvent("SetTeamAbilities");
            Debug.Log("팀별 능력 적용 요청");
        }

        Debug.Log("카운트다운 종료, 텔레포트 실행");
    }

    /// <summary>
    /// 플레이어를 팀에 따라 텔레포트
    /// </summary>
    private void TeleportPlayerToTeamSpawn()
    {
        if (!Utilities.IsValid(localPlayer) || checkPlayerBehaviour == null) return;

        // CheckPlayer 스크립트에서 로컬 플레이어의 팀 정보 가져오기
        int localPlayerTeam = (int)checkPlayerBehaviour.GetProgramVariable("localPlayerTeam");

        // 팀에 따라 텔레포트 위치 결정
        Transform targetSpawn = null;
        if (localPlayerTeam == TEAM_HUNTER && hunterSpawnPoint != null)
        {
            targetSpawn = hunterSpawnPoint;
            Debug.Log("사냥꾼 팀으로 텔레포트");
        }
        else if (localPlayerTeam == TEAM_BUG && bugSpawnPoint != null)
        {
            targetSpawn = bugSpawnPoint;
            Debug.Log("벌레 팀으로 텔레포트");
        }

        // 텔레포트 실행
        if (targetSpawn != null)
        {
            localPlayer.TeleportTo(
                targetSpawn.position,
                targetSpawn.rotation,
                VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                true
            );
        }
    }

    /// <summary>
    /// 게임 재시작 (게임 종료 후 호출)
    /// </summary>
    public void ResetGame()
    {
        // 마스터 클라이언트만 게임 상태 초기화
        if (Utilities.IsValid(localPlayer) && localPlayer.isMaster)
        {
            gameStarted = false;
            RequestSerialization();
        }
    }
}
