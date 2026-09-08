using UnityEngine;

public class CoasterMotionInverter : MonoBehaviour
{
    [Header("1. Nguồn Animation ngầm")]
    [Tooltip("Kéo BluffTitler Show bên trong Ghost_Tracker vào đây")]
    public Transform animatedShow;

    [Tooltip("Kéo Ghost_Tracker vào đây")]
    public RideController rideController;

    [Header("2. Tàu thật")]
    [Tooltip("Kéo KexLSMfoSketchfab vào đây")]
    public Transform realCart;

    private Vector3 stationShowPos;
    private Quaternion stationShowRot;

    private Vector3 stationCartPos;
    private Quaternion stationCartRot;

    private bool isCalibrated = false;

    void Start()
    {
        // Chờ 1 frame hoặc lấy dữ liệu sau khi RideController đã tua đến đúng giây ở Ga
        CalibrateStation();
    }

    public void CalibrateStation()
    {
        if (animatedShow == null || realCart == null) return;

        // Lưu lại tư thế chuẩn xác của môi trường ngầm tại mốc Ga
        stationShowPos = animatedShow.position;
        stationShowRot = animatedShow.rotation;

        // Lưu lại vị trí bạn đã đặt tàu nằm trên đường ray tại Ga
        stationCartPos = realCart.position;
        stationCartRot = realCart.rotation;

        isCalibrated = true;
    }

    void LateUpdate()
    {
        if (!isCalibrated || animatedShow == null || realCart == null) return;

        // 1. Tính toán chuyển động xoay tương đối so với lúc ở Ga
        Quaternion deltaRot = animatedShow.rotation * Quaternion.Inverse(stationShowRot);
        Quaternion invertedRot = Quaternion.Inverse(deltaRot);

        // 2. Tính toán độ dịch chuyển tương đối so với lúc ở Ga
        Vector3 deltaPos = animatedShow.position - stationShowPos;
        Vector3 invertedPos = -(invertedRot * deltaPos);

        // 3. Áp dụng chuyển động xuất phát từ Ga
        realCart.position = stationCartPos + invertedPos;
        realCart.rotation = invertedRot * stationCartRot;
    }
}