using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// 테스트용 벌레 오브젝트 스크립트
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TestBug : UdonSharpBehaviour
{
    [Header("참조")]
    [SerializeField] private CheckPlayer checkPlayer; // CheckPlayer 참조

    [Header("설정")]
    [SerializeField] private int fakePlayerId = 999; // 가상 플레이어 ID

    private void Start()
    {
        if (checkPlayer == null)
        {
            Debug.LogError("TestBug: CheckPlayer 참조가 설정되지 않았습니다.");
            return;
        }

        // 큐브를 벌레 팀으로 등록
        RegisterAsBug();
    }

    /// <summary>
    /// 이 오브젝트를 벌레 팀으로 등록
    /// </summary>
    public void RegisterAsBug()
    {
        // 여기서는 실제 등록이 아니라 HammerController에서 테스트할 수 있도록
        // OnTriggerEnter 이벤트를 처리합니다
        Debug.Log("테스트 벌레 오브젝트가 생성되었습니다. ID: " + fakePlayerId);
    }

    /// <summary>
    /// 망치와 충돌 시 호출
    /// </summary>
    public void OnHit()
    {
        Debug.Log("테스트 벌레가 망치에 맞았습니다!");

        // 시각적 효과 (색상 변경)
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red;
        }

        // 잠시 후 비활성화
        SendCustomEventDelayedSeconds(nameof(DeactivateBug), 1.0f);
    }

    /// <summary>
    /// 벌레 비활성화
    /// </summary>
    public void DeactivateBug()
    {
        gameObject.SetActive(false);
        Debug.Log("테스트 벌레가 비활성화되었습니다.");
    }

    /// <summary>
    /// 테스트 벌레 재활성화
    /// </summary>
    public void ReactivateBug()
    {
        gameObject.SetActive(true);

        // 색상 초기화
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.white;
        }

        Debug.Log("테스트 벌레가 재활성화되었습니다.");
    }

    /// <summary>
    /// 가상 플레이어 ID 반환
    /// </summary>
    public int GetFakePlayerId()
    {
        return fakePlayerId;
    }
}
