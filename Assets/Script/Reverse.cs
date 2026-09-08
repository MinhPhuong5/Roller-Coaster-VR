using UnityEngine;

public class ReverseRideAnimation : MonoBehaviour
{
    public string animationClipName = "BluffTitler Animation";

    void Start()
    {
        Animation anim = GetComponent<Animation>();
        if (anim == null)
        {
            Debug.LogError("Không tìm thấy Component Animation trên object này!");
            return;
        }

        AnimationState state = anim[animationClipName];
        if (state == null)
        {
            Debug.LogError("Không tìm thấy clip tên: " + animationClipName);
            return;
        }

        // Đảo chiều: phát ngược từ cuối về đầu
        state.speed = -1f;
        state.time = state.length;
        anim.Play(animationClipName);
    }
}