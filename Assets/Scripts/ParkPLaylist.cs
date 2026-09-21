using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ParkPlaylist : MonoBehaviour
{
    [Header("Danh Sách Bài Nhạc")]
    public AudioClip[] playlist;

    [Header("Tùy Chọn")]
    public bool shuffle = false; // Bật nếu muốn phát xáo trộn ngẫu nhiên

    private AudioSource audioSource;
    private int currentTrackIndex = 0;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        if (playlist == null || playlist.Length == 0) return;

        PlayCurrentTrack();
    }

    void Update()
    {
        // Khi bài hiện tại vừa hát xong, tự động nhảy sang bài tiếp theo
        if (!audioSource.isPlaying && playlist.Length > 0)
        {
            NextTrack();
        }
    }

    void PlayCurrentTrack()
    {
        if (playlist[currentTrackIndex] == null) return;

        audioSource.clip = playlist[currentTrackIndex];
        audioSource.Play();
    }

    void NextTrack()
    {
        if (shuffle && playlist.Length > 1)
        {
            int nextIndex;
            do
            {
                nextIndex = Random.Range(0, playlist.Length);
            } while (nextIndex == currentTrackIndex);

            currentTrackIndex = nextIndex;
        }
        else
        {
            // Chạy tuần tự từ bài đầu tới bài cuối rồi quay vòng lại bài 0
            currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;
        }

        PlayCurrentTrack();
    }
}