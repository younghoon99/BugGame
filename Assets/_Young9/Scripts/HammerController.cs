using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class HammerController : UdonSharpBehaviour
{
    // 사냥꾼 관련 정보
    [Header("사냥꾼 관련")]
    [SerializeField] private CheckPlayer checkPlayer; // CheckPlayer 참조
    [SerializeField] private GameManager gameManager; // GameManager 참조
    [Header("망치 설정")]
    [SerializeField] private float hitForce = 10f; // 타격 시 가해지는 힘
    [SerializeField] private float hitRadius = 0.5f; // 타격 감지 반경
    [SerializeField] private GameObject hitEffect; // 타격 효과 (선택사항)
    [SerializeField] private AudioSource hitSound; // 타격 소리 (선택사항)
    
    // 로컬 플레이어 참조
    private VRCPlayerApi localPlayer;

    private void Start()
    {
        // 콜라이더가 트리거로 설정되어 있는지 확인
        Collider collider = GetComponent<Collider>();
        if (collider != null && !collider.isTrigger)
        {
            Debug.LogWarning("망치의 Collider가 트리거로 설정되어 있지 않습니다. 자동으로 설정합니다.");
            collider.isTrigger = true;
        }
        
        // 로컬 플레이어 참조 설정
        localPlayer = Networking.LocalPlayer;
        
        // CheckPlayer 참조 확인
        if (checkPlayer == null)
        {
            Debug.LogError("HammerController: CheckPlayer 참조가 설정되지 않았습니다.");
        }
        
        // 히트 이펙트 비활성화 (있는 경우)
        if (hitEffect != null)
        {
            hitEffect.SetActive(false);
        }
    }
    public override void OnPickupUseDown()
    {
        StartSwing();
    }

    // 트리거 충돌 감지
    private void OnTriggerEnter(Collider other)
    {
        if (!Utilities.IsValid(localPlayer) || !Utilities.IsValid(checkPlayer)) return;
        
        // 망치가 휘두르는 중이 아니면 충돌 무시
        if (!isSwinging) return;
        
        Debug.Log("망치 충돌 감지");
        
        // 충돌 위치 계산
        Vector3 hitPosition = other.ClosestPoint(transform.position);
        
        // 타격 효과 표시 (있는 경우)
        ShowHitEffect(hitPosition);
        
        // 테스트용 벌레 오브젝트 감지 추가
        TestBug testBug = other.GetComponent<TestBug>();
        if (testBug != null)
        {
            Debug.Log("테스트 벌레 오브젝트를 타격했습니다!");
            
            // 타격 효과음 재생
            PlayHitSound();
            
            // 테스트 벌레 오브젝트에 타격 이벤트 전달
            testBug.OnHit();
            return;
        }
        
        // 모든 플레이어 가져오기
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        
        // 모든 플레이어에 대해 거리 계산
        foreach (var player in players)
        {
            if (!Utilities.IsValid(player)) continue;
            
            // 플레이어의 위치와 충돌 지점의 거리 확인
            float distance = Vector3.Distance(player.GetPosition(), hitPosition);
            
            if (distance < hitRadius) // 설정된 타격 감지 반경 내에 있는지 확인
            {
                Debug.Log("플레이어가 타격 범위 내에 있습니다. 거리: " + distance);
                
                // CheckPlayer를 통해 팀 확인
                int playerTeam = checkPlayer.GetPlayerTeam(player.playerId);
                Debug.Log("플레이어의 팀: " + playerTeam);
                
                if (playerTeam == CheckPlayer.TEAM_BUG) // 벌레 팀인 경우
                {
                    Debug.Log("벌레 팀 플레이어를 타격했습니다!");
                    
                    // 타격 효과음 재생 (있는 경우)
                    PlayHitSound();
                    
                    // 네트워크 소유자인 경우에만 탈락 처리 (마스터 클라이언트)
                    if (Networking.IsMaster)
                    {
                        // 벌레 팀 플레이어 탈락 처리
                        checkPlayer.EliminatePlayer(player.playerId);
                        
                        // GameManager에 탈락 이벤트 전달
                        if (gameManager != null)
                        {
                            gameManager.OnPlayerEliminated(player.playerId);
                        }
                        
                        Debug.Log("플레이어가 탈락 처리되었습니다.");
                    }
                    
                    // 로컬 플레이어가 타격 당한 경우 시각/물리 효과 적용
                    if (player.isLocal)
                    {
                        Debug.Log("로컬 플레이어가 타격 당했습니다.");
                        
                        // 카메라 흔들림 효과 등을 추가할 수 있음
                        // 현재 VRChat에서는 직접적인 물리력 적용이 제한적이므로 시각 효과 위주로 구현
                    }
                }
                else
                {
                    Debug.Log("플레이어는 벌레 팀이 아니므로 타격 효과가 없습니다.");
                }
            }
        }
    }
    
    // 타격 효과 표시
    private void ShowHitEffect(Vector3 position)
    {
        if (hitEffect != null)
        {
            hitEffect.transform.position = position;
            hitEffect.SetActive(true);
            
            // 1초 후 효과 비활성화
            SendCustomEventDelayedSeconds("HideHitEffect", 1.0f);
        }
    }
    
    // 타격 효과 숨기기
    public void HideHitEffect()
    {
        if (hitEffect != null)
        {
            hitEffect.SetActive(false);
        }
    }
    
    // 타격 소리 재생
    private void PlayHitSound()
    {
        if (hitSound != null && !hitSound.isPlaying)
        {
            hitSound.Play();
        }
    }

    private bool isSwinging = false;
    private float swingDuration = 0.2f;
    private float timer = 0f;
    private bool swingingDown = true;

    private float startX;
    private float targetX;

    private void StartSwing()
    {
        if (isSwinging) return;

        isSwinging = true;
        timer = 0f;
        swingingDown = true;

        // 현재 X축 각도 기준으로 상대 회전
        startX = transform.localEulerAngles.x;
        targetX = startX + 100f;

        ContinueSwing();
    }

    public void ContinueSwing()
    {
        if (!isSwinging) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / swingDuration);

        // 현재 Y, Z는 유지
        Vector3 currentEuler = transform.localEulerAngles;
        float currentY = currentEuler.y;
        float currentZ = currentEuler.z;

        float x;

        if (swingingDown)
        {
            x = Mathf.LerpAngle(startX, targetX, t);
            if (t >= 1f)
            {
                swingingDown = false;
                timer = 0f;

                // 되돌리기용 준비
                float temp = startX;
                startX = targetX;
                targetX = temp;
            }
        }
        else
        {
            x = Mathf.LerpAngle(startX, targetX, t);
            if (t >= 1f)
            {
                isSwinging = false;
                return;
            }
        }

        transform.localEulerAngles = new Vector3(x, currentY, currentZ);
        SendCustomEventDelayedFrames("ContinueSwing", 1);
    }
}
