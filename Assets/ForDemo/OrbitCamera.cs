using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5.0f;

    [Header("Rotation Speed (Max)")]
    [SerializeField] private float maxSpeedX = 10.0f;
    [SerializeField] private float maxSpeedY = 7.0f;

    [Header("Smooth Settings (Inertia)")]
    [SerializeField] private float acceleration = 2.0f; 
    [SerializeField] private float deceleration = 4.0f;

    [Header("Rotation Limits")]
    [SerializeField] private float yMinLimit = -20f;
    [SerializeField] private float yMaxLimit = 80f;

    // 現在の回転角度
    private float currentX = 0.0f;
    private float currentY = 0.0f;

    // 現在の実際の回転速度（慣性計算用）
    private float velocityX = 0.0f;
    private float velocityY = 0.0f;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;

        if (target == null)
        {
            Debug.LogWarning("OrbitCameraSmooth: ターゲットが設定されていません。");
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float targetSpeedX = 0f;
        float targetSpeedY = 0f;

        if (keyboard.aKey.isPressed) targetSpeedX = -maxSpeedX;
        else if (keyboard.dKey.isPressed) targetSpeedX = maxSpeedX;

        if (keyboard.wKey.isPressed) targetSpeedY = -maxSpeedY; // 上を向く
        else if (keyboard.sKey.isPressed) targetSpeedY = maxSpeedY; // 下を向く

        float accelRateX = (Mathf.Abs(targetSpeedX) > 0.01f) ? acceleration : deceleration;
        float accelRateY = (Mathf.Abs(targetSpeedY) > 0.01f) ? acceleration : deceleration;

        velocityX = Mathf.MoveTowards(velocityX, targetSpeedX, accelRateX * maxSpeedX * Time.deltaTime);
        velocityY = Mathf.MoveTowards(velocityY, targetSpeedY, accelRateY * maxSpeedY * Time.deltaTime);

        currentX += velocityX * Time.deltaTime;
        currentY += velocityY * Time.deltaTime;

        currentY = ClampAngle(currentY, yMinLimit, yMaxLimit);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}