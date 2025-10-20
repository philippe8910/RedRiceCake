using UnityEngine;

[DisallowMultipleComponent]
public class SaltShakerComponent : MonoBehaviour
{
    public ParticleSystem particleSystem;
    
    [Header("傾斜判定")]
    [Tooltip("超過此角度視為正在傾斜")]
    [Range(0f, 180f)] public float tiltThreshold = 15f;

    [Tooltip("是否改用世界Up傾角( transform.up 相對 Vector3.up )")]
    public bool useWorldUpTilt = false;

    [Header("基準姿態")]
    [Tooltip("Awake 時把目前旋轉設為基準姿態")]
    public bool captureReferenceOnAwake = true;
    public Quaternion referenceRotation = Quaternion.identity;

    [Header("讀取用")]
    public bool isTilting;     // <-- 你要的布林

    void Awake()
    {
        if (captureReferenceOnAwake)
            referenceRotation = transform.rotation;
    }

    void Update()
    {
        if (useWorldUpTilt)
        {
            // 世界Up傾角（不受 180/-180 影響）
            float worldTilt = Vector3.Angle(transform.up, Vector3.up);
            isTilting = worldTilt > tiltThreshold;
        }
        else
        {
            // 相對基準的三軸角（處理 180 ↔ -180）
            Quaternion delta = Quaternion.Inverse(referenceRotation) * transform.rotation;
            Vector3 e = delta.eulerAngles;

            float ax = Mathf.Abs(Mathf.DeltaAngle(0f, e.x));
            float ay = Mathf.Abs(Mathf.DeltaAngle(0f, e.y));
            float az = Mathf.Abs(Mathf.DeltaAngle(0f, e.z));

            float maxAxis = Mathf.Max(ax, Mathf.Max(ay, az));
            isTilting = maxAxis > tiltThreshold;
        }

        if (isTilting)
        {
            particleSystem.Play();
        }
        else
        {
            particleSystem.Stop();
        }
    }
}