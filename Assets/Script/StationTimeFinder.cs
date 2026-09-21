using UnityEngine;

/// <summary>
/// SCRIPT TẠM, chỉ dùng để dò tìm đúng mốc giây của đoạn ga.
/// Giữ phím mũi tên phải/trái để tua animation tới lui, xem trực tiếp
/// trong Scene/Game view. Khi thấy tàu đúng ở ga, bấm Space để in ra
/// Console con số chính xác. Xong việc thì xóa script này đi.
/// </summary>
public class StationTimeFinder : MonoBehaviour
{
    public string animationClipName = "BluffTitler Animation";
    private Animation anim;
    private AnimationState state;

    void Start()
    {
        anim = GetComponent<Animation>();
        state = anim[animationClipName];
        anim.Play(animationClipName);
        state.speed = 0f;
    }

    void Update()
    {
        float step = 5f * Time.deltaTime; // tốc độ tua — giữ phím lâu thì tua nhanh hơn
        if (Input.GetKey(KeyCode.RightArrow)) state.time += step;
        if (Input.GetKey(KeyCode.LeftArrow)) state.time -= step;
        state.time = Mathf.Clamp(state.time, 0f, state.length);
        anim.Sample();

        if (Input.GetKeyDown(KeyCode.Space))
            Debug.Log("STATION TIME = " + state.time);
    }
}