using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class OpenDoor : UdonSharpBehaviour
{
    [Header("문 타입 설정")]
    [Tooltip("가로로 회전하는 문 (Y축 회전)")]
    public bool isHorizontalDoor = true;
    
    [Tooltip("세로로 회전하는 문 (X축 회전)")]
    public bool isVerticalDoor = false;
    
    [Tooltip("당기는 문 (Z축 이동)")]
    public bool isPullDoor = false;
    
    [Header("문 설정")]
    [Tooltip("문이 열리는 속도")]
    public float doorSpeed = 2.0f;
    
    [Tooltip("문이 자동으로 닫히기까지의 시간(초)")]
    public float autoCloseDelay = 3.0f;
    
    [Tooltip("자동 닫힘 기능 활성화")]
    public bool useAutoClose = true;
    
    [Header("가로 회전 문 설정")]
    [Tooltip("가로 문이 열릴 때의 Y축 회전값")]
    public float horizontalOpenAngle = -90.0f;
    
    [Tooltip("가로 문이 닫힐 때의 Y축 회전값")]
    public float horizontalCloseAngle = -10.0f;
    
    [Header("세로 회전 문 설정")]
    [Tooltip("세로 문이 열릴 때의 X축 회전값")]
    public float verticalOpenAngle = 90.0f;
    
    [Tooltip("세로 문이 닫힐 때의 X축 회전값")]
    public float verticalCloseAngle = 10.0f;
    
    [Header("당기는 문 설정")]
    [Tooltip("당기는 문이 열릴 때의 Z축 위치")]
    public float pullOpenPosition = 0.8f;
    
    [Tooltip("당기는 문이 닫힐 때의 Z축 위치")]
    public float pullClosePosition = 0.4f;
    
    // 네트워크 동기화를 위한 변수
    [UdonSynced]
    private bool _syncedIsOpen = false;
    
    // 문이 열려있는지 여부를 추적
    private bool isOpen = false;
    
    // 현재 문이 움직이는 중인지 여부를 추적
    private bool isMoving = false;
    
    // 자동 닫힘 타이머
    private float autoCloseTimer = 0.0f;
    
    // 자동 닫힘이 진행 중인지 여부
    private bool isAutoClosing = false;
    
    // 목표 회전값 또는 위치값
    private Vector3 targetTransform;
    
    // 현재 회전값 또는 위치값
    private Vector3 currentTransform;
    
    private void Start()
    {
        // 문 타입 검증 (하나만 선택되도록)
        ValidateDoorType();
        
        // 초기 회전값 또는 위치값 설정
        InitializeDoorTransform();
    }
    
    // 문 타입 검증 (하나만 선택되도록)
    private void ValidateDoorType()
    {
        // 여러 타입이 선택된 경우 우선순위: 가로 > 세로 > 당기는 문
        if (isHorizontalDoor)
        {
            isVerticalDoor = false;
            isPullDoor = false;
        }
        else if (isVerticalDoor)
        {
            isPullDoor = false;
        }
        
        // 아무것도 선택되지 않은 경우 기본값으로 가로 문 설정
        if (!isHorizontalDoor && !isVerticalDoor && !isPullDoor)
        {
            isHorizontalDoor = true;
        }
        
        Debug.Log("문 타입: " + GetDoorTypeString());
    }
    
    // 문 타입에 따른 문자열 반환
    private string GetDoorTypeString()
    {
        if (isHorizontalDoor) return "가로 회전 문";
        if (isVerticalDoor) return "세로 회전 문";
        if (isPullDoor) return "당기는 문";
        return "알 수 없는 문 타입";
    }
    
    // 초기 회전값 또는 위치값 설정
    private void InitializeDoorTransform()
    {
        if (isHorizontalDoor)
        {
            // 가로 회전 문
            Vector3 rotation = transform.localRotation.eulerAngles;
            rotation.y = horizontalCloseAngle;
            transform.localRotation = Quaternion.Euler(rotation);
            currentTransform = rotation;
            targetTransform = rotation;
        }
        else if (isVerticalDoor)
        {
            // 세로 회전 문
            Vector3 rotation = transform.localRotation.eulerAngles;
            rotation.x = verticalCloseAngle;
            transform.localRotation = Quaternion.Euler(rotation);
            currentTransform = rotation;
            targetTransform = rotation;
        }
        else if (isPullDoor)
        {
            // 당기는 문
            Vector3 position = transform.localPosition;
            position.z = pullClosePosition;
            transform.localPosition = position;
            currentTransform = position;
            targetTransform = position;
        }
    }
    
    public override void Interact()
    {
        // 오브젝트 소유권 가져오기
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        
        // 문 상태 전환
        _syncedIsOpen = !_syncedIsOpen;
        
        // 네트워크 이벤트 전송
        RequestSerialization();
        
        // 자동 닫힘 타이머 초기화
        ResetAutoCloseTimer();
        
        // 로컬에서 문 상태 업데이트
        UpdateDoorState();
    }
    
    // 자동 닫힘 타이머 초기화
    private void ResetAutoCloseTimer()
    {
        // 문이 열렸을 때만 타이머 활성화
        if (_syncedIsOpen && useAutoClose)
        {
            autoCloseTimer = 0.0f;
            isAutoClosing = true;
        }
        else
        {
            isAutoClosing = false;
        }
    }
    
    public override void OnDeserialization()
    {
        // 네트워크에서 데이터를 받았을 때 문 상태 업데이트
        bool wasOpen = isOpen;
        isOpen = _syncedIsOpen;
        
        // 문이 열렸을 때만 자동 닫힘 타이머 초기화
        if (!wasOpen && isOpen && useAutoClose)
        {
            ResetAutoCloseTimer();
        }
        
        UpdateDoorState();
    }
    
    private void UpdateDoorState()
    {
        // 동기화된 변수에서 문 상태 업데이트
        isOpen = _syncedIsOpen;
        
        // 문 타입에 따라 목표 회전값 또는 위치값 설정
        if (isHorizontalDoor)
        {
            // 가로 회전 문
            Vector3 rotation = transform.localRotation.eulerAngles;
            rotation.y = isOpen ? horizontalOpenAngle : horizontalCloseAngle;
            targetTransform = rotation;
        }
        else if (isVerticalDoor)
        {
            // 세로 회전 문
            Vector3 rotation = transform.localRotation.eulerAngles;
            rotation.x = isOpen ? verticalOpenAngle : verticalCloseAngle;
            targetTransform = rotation;
        }
        else if (isPullDoor)
        {
            // 당기는 문
            Vector3 position = transform.localPosition;
            position.z = isOpen ? pullOpenPosition : pullClosePosition;
            targetTransform = position;
        }
        
        // 움직임 시작
        isMoving = true;
        
        // 디버그 로그
        Debug.Log("문 상태 변경: " + (isOpen ? "열림" : "닫힘") + " - " + GetDoorTypeString());
    }
    
    private void Update()
    {
        // 자동 닫힘 타이머 업데이트
        UpdateAutoCloseTimer();
        
        // 문이 움직이는 중이라면
        if (isMoving)
        {
            if (isHorizontalDoor || isVerticalDoor)
            {
                // 회전 문 (가로 또는 세로)
                Vector3 currentRotation = transform.localRotation.eulerAngles;
                
                if (isHorizontalDoor)
                {
                    // Y축 회전 보간
                    currentRotation.y = Mathf.LerpAngle(currentRotation.y, targetTransform.y, Time.deltaTime * doorSpeed);
                }
                else
                {
                    // X축 회전 보간
                    currentRotation.x = Mathf.LerpAngle(currentRotation.x, targetTransform.x, Time.deltaTime * doorSpeed);
                }
                
                transform.localRotation = Quaternion.Euler(currentRotation);
                currentTransform = currentRotation;
                
                // 목표 회전값에 충분히 가까워지면 움직임 종료
                float angleDifference = isHorizontalDoor ? 
                    Mathf.Abs(Mathf.DeltaAngle(currentRotation.y, targetTransform.y)) : 
                    Mathf.Abs(Mathf.DeltaAngle(currentRotation.x, targetTransform.x));
                
                if (angleDifference < 0.1f)
                {
                    transform.localRotation = Quaternion.Euler(targetTransform);
                    currentTransform = targetTransform;
                    isMoving = false;
                    Debug.Log("문 이동 완료 (회전): " + currentTransform);
                }
            }
            else if (isPullDoor)
            {
                // 당기는 문
                Vector3 currentPosition = transform.localPosition;
                
                // Z축 위치 보간
                currentPosition.z = Mathf.Lerp(currentPosition.z, targetTransform.z, Time.deltaTime * doorSpeed);
                
                transform.localPosition = currentPosition;
                currentTransform = currentPosition;
                
                // 목표 위치에 충분히 가까워지면 움직임 종료
                float positionDifference = Mathf.Abs(currentPosition.z - targetTransform.z);
                
                if (positionDifference < 0.01f)
                {
                    transform.localPosition = targetTransform;
                    currentTransform = targetTransform;
                    isMoving = false;
                    Debug.Log("문 이동 완료 (위치): " + currentTransform);
                }
            }
        }
    }
    
    // 자동 닫힘 타이머 업데이트
    private void UpdateAutoCloseTimer()
    {
        // 자동 닫힘이 활성화되어 있고, 문이 열려있고, 자동 닫힘이 진행 중일 때
        if (useAutoClose && isOpen && isAutoClosing)
        {
            // 타이머 증가
            autoCloseTimer += Time.deltaTime;
            
            // 지정된 시간이 지나면 문 닫기
            if (autoCloseTimer >= autoCloseDelay)
            {
                // 오브젝트 소유권 가져오기
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                
                // 문 닫기
                _syncedIsOpen = false;
                
                // 네트워크 이벤트 전송
                RequestSerialization();
                
                // 자동 닫힘 비활성화
                isAutoClosing = false;
                
                // 로컬에서 문 상태 업데이트
                UpdateDoorState();
                
                Debug.Log("자동 닫힘 실행: " + autoCloseDelay + "초 경과");
            }
        }
    }
}
