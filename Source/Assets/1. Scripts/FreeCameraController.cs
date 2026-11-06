using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("이동 속도")]
    public float moveSpeed = 6f;
    public float fastSpeed = 12f;

    [Header("마우스 회전")]
    public float mouseSensitivity = 2f;
    public bool rotateOnlyWhileRightMouse = true;

    [Header("시작 위치/회전 고정")]
    public bool lockToInitialTransformOnPlay = true;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float rotationX;
    private float rotationY;

    void Awake()
    {
        // 플레이 버튼 누르기 직전, 씬에 배치된 원래 위치/회전을 저장
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void Start()
    {
        // 게임 시작 순간 무조건 원래 위치/회전으로 강제 복원
        if (lockToInitialTransformOnPlay)
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }

        Vector3 euler = transform.rotation.eulerAngles;
        rotationX = euler.x;
        rotationY = euler.y;

        // 시작할 때 커서 잠그지 않음
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Move();
        Look();

        // R 키 누르면 언제든 시작 위치로 되돌아감
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToInitialTransform();
        }
    }

    void Move()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : moveSpeed;

        float h = Input.GetAxis("Horizontal"); // A D
        float v = Input.GetAxis("Vertical");   // W S

        float upDown = 0f;
        if (Input.GetKey(KeyCode.E)) upDown = 1f;
        if (Input.GetKey(KeyCode.Q)) upDown = -1f;

        Vector3 move = transform.forward * v + transform.right * h + transform.up * upDown;
        transform.position += move * speed * Time.deltaTime;
    }

    void Look()
    {
        bool canRotate = true;

        if (rotateOnlyWhileRightMouse)
        {
            canRotate = Input.GetMouseButton(1); // 우클릭 누를 때만 회전
        }

        if (!canRotate)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotationY += mouseX;
        rotationX -= mouseY;

        // 카메라 상하 각도 제한
        if (rotationX > 180f) rotationX -= 360f;
        if (rotationX < -180f) rotationX += 360f;
        rotationX = Mathf.Clamp(rotationX, -80f, 80f);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }

    public void ResetToInitialTransform()
    {
        transform.SetPositionAndRotation(initialPosition, initialRotation);

        Vector3 euler = transform.rotation.eulerAngles;
        rotationX = euler.x;
        rotationY = euler.y;
    }
}