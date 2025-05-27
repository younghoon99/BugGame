using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// 팀별 플레이어 능력을 관리하는 클래스
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Ability : UdonSharpBehaviour
{
    // 팀 상수 정의
    private const int TEAM_HUNTER = 0;  // 사냥꾼 팀
    private const int TEAM_BUG = 1;     // 벌레 팀
    
    // 게임 관리자 참조
    [Header("게임 관리자 참조")]
    private UdonBehaviour checkPlayerBehaviour; // CheckPlayer 스크립트 참조
    
    // 능력 설정
    [Header("벌레 팀 능력 설정")]
    [SerializeField] private float bugJumpImpulse = 3.0f;      // 벌레 팀 점프력
    [SerializeField] private float bugWalkSpeed = 4.0f;        // 벌레 팀 걸기 속도
    [SerializeField] private float bugStrafeSpeed = 4.0f;        // 벌레 팀 걸기 속도
    [SerializeField] private float bugRunSpeed = 8.0f;         // 벌레 팀 달리기 속도
    [SerializeField] private float bugGravityStrength = 1.0f;  // 벌레 팀 중력
    
    [Header("사냥꾼 팀 능력 설정")]
    [SerializeField] private float hunterJumpImpulse = 1.5f;   // 사냥꾼 팀 점프력
    [SerializeField] private float hunterWalkSpeed = 2.0f;     // 사냥꾼 팀 걸기 속도
    [SerializeField] private float hunterStrafeSpeed = 2.0f;     // 사냥꾼 팀 걸기 속도
    [SerializeField] private float hunterRunSpeed = 4.0f;      // 사냥꾼 팀 달리기 속도
    [SerializeField] private float hunterGravityStrength = 2.0f; // 사냥꾼 팀 중력
    
    // 로컬 변수
    private VRCPlayerApi localPlayer;
    
    void Start()
    {
        checkPlayerBehaviour = GameObject.Find("GameManager").GetComponent<UdonBehaviour>();
        // 로컬 플레이어 정보 초기화
        localPlayer = Networking.LocalPlayer;
    }
    
    /// <summary>
    /// 팀에 따라 플레이어 능력 설정 (외부에서 호출)
    /// </summary>
    public void SetTeamAbilities()
    {
        if (!Utilities.IsValid(localPlayer) || checkPlayerBehaviour == null) return;

        // CheckPlayer 스크립트에서 로컬 플레이어의 팀 정보 가져오기
        int localPlayerTeam = (int)checkPlayerBehaviour.GetProgramVariable("localPlayerTeam");
        
        Debug.Log("플레이어 팀 확인: " + (localPlayerTeam == TEAM_HUNTER ? "사냥꾼" : "벌레"));

        // 팀에 따라 다른 능력 부여
        if (localPlayerTeam == TEAM_BUG)
        {
            // 벌레 팀 능력 설정
            localPlayer.SetJumpImpulse(bugJumpImpulse);
            localPlayer.SetWalkSpeed(bugWalkSpeed);
            localPlayer.SetStrafeSpeed(bugStrafeSpeed);
            localPlayer.SetRunSpeed(bugRunSpeed);
            localPlayer.SetGravityStrength(bugGravityStrength);
            
            Debug.Log("벌레 팀 능력 적용 - 점프력: " + bugJumpImpulse + ", 속도: " + bugRunSpeed + ", 중력: " + bugGravityStrength);
        }
        else if (localPlayerTeam == TEAM_HUNTER)
        {
            // 사냥꾼 팀 능력 설정
            localPlayer.SetJumpImpulse(hunterJumpImpulse);
            localPlayer.SetWalkSpeed(hunterWalkSpeed);
            localPlayer.SetStrafeSpeed(hunterStrafeSpeed);
            localPlayer.SetRunSpeed(hunterRunSpeed);
            localPlayer.SetGravityStrength(hunterGravityStrength);
            
            Debug.Log("사냥꾼 팀 능력 적용 - 점프력: " + hunterJumpImpulse + ", 속도: " + hunterRunSpeed + ", 중력: " + hunterGravityStrength);
        }
    }
    
    /// <summary>
    /// 기본 능력으로 초기화
    /// </summary>
    public void ResetAbilities()
    {
        if (!Utilities.IsValid(localPlayer)) return;
        
        // 기본 값으로 초기화
        localPlayer.SetJumpImpulse(1.5f);
        localPlayer.SetWalkSpeed(2.0f);
        localPlayer.SetStrafeSpeed(2.0f);
        localPlayer.SetRunSpeed(4.0f);
        localPlayer.SetGravityStrength(1.0f);
        
        Debug.Log("플레이어 능력 초기화");
    }
}
