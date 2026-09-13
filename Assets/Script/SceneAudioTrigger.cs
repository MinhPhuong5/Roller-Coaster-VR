using System.Collections;
using UnityEngine;

/// <summary>
/// Script kich hoat giong noi AoT va sau 2 giay phat nhac nen xuyen suot scene
/// Gan truc tiep vao doi tuong bieu tuong duoi dat (hoac Trigger Zone)
/// </summary>
public class SceneAudioTrigger : MonoBehaviour
{
    [Header("Tag cua nhan vat")]
    [Tooltip("Tag de nhan dien nguoi choi buoc vao")]
    public string playerTag = "Player";

    [Header("Am thanh (Audio Clips)")]
    [Tooltip("File giong noi AoT (phat 1 lan khi vua dam len)")]
    public AudioClip voiceClip;

    [Tooltip("File nhac nen (se lap di lap lai xuyen suot scene)")]
    public AudioClip bgmClip;

    [Header("Thiet lap")]
    [Tooltip("Thoi gian cho (giay) truoc khi bat nhac nen")]
    public float bgmDelay = 2.0f;

    [Range(0f, 1f)]
    [Tooltip("Am luong giong noi")]
    public float voiceVolume = 1.0f;

    [Range(0f, 1f)]
    [Tooltip("Am luong nhac nen")]
    public float bgmVolume = 0.7f;

    [Tooltip("An hoac xoa bieu tuong sau khi da kich hoat")]
    public bool hideSymbolAfterTrigger = false;

    private bool hasTriggered = false;
    private AudioSource voiceSource;
    private AudioSource bgmSource;

    void Awake()
    {
        // 1. Tao AudioSource cho Giong noi (2D Sound - nghe ro toan map)
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f; // 2D: am thanh vang deu tai nguoi choi
        voiceSource.volume = voiceVolume;

        // 2. Tao AudioSource cho Nhac nen BGM (2D Sound + Loop)
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f; // 2D: di dau trong map cung nghe thay nhac
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiem tra da kich hoat chua va co dung la Player dam len khong
        if (!hasTriggered && other.CompareTag(playerTag))
        {
            hasTriggered = true;
            StartCoroutine(PlayVoiceThenBGM());
        }
    }

    private IEnumerator PlayVoiceThenBGM()
    {
        // 1. Phat giong noi AoT ngay lap tuc
        if (voiceClip != null)
        {
            voiceSource.clip = voiceClip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            Debug.Log("[SceneAudioTrigger] Dang phat giong noi AoT...");
        }

        // Neu muon an bieu tuong sau khi dam len
        if (hideSymbolAfterTrigger)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;
        }

        // 2. Cho 2 giay theo yeu cau
        yield return new WaitForSeconds(bgmDelay);

        // 3. Bat nhac nen loop xuyen suot scene
        if (bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
            Debug.Log("[SceneAudioTrigger] Da bat nhac nen scene!");
        }
    }
}