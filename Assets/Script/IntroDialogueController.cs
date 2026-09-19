using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class IntroDialogueController : MonoBehaviour
{
    public static IntroDialogueController Instance { get; private set; }
    public static bool GameStarted { get; private set; }

    [Header("UI References")]
    [Tooltip("Panel chính chứa toàn bộ cảnh hội thoại")]
    public GameObject dialoguePanel;

    [Tooltip("Hình nền nhân vật + toa tàu Haku")]
    public Image backgroundImage;

    [Tooltip("Text hiển thị nội dung thoại")]
    public TextMeshProUGUI dialogueText;

    [Tooltip("Nút / Text 'TIẾP TỤC ▽' gợi ý chuyển câu")]
    public GameObject continuePrompt;

    [Tooltip("Nút bấm trong suốt phủ toàn màn hình để click chuột bất kỳ đâu")]
    public Button fullScreenClickButton;

    [Header("Settings and Audio")]
    [Tooltip("Tốc độ gõ chữ (giây mỗi ký tự)")]
    public float typingSpeed = 0.035f;

    [Tooltip("Âm thanh khi bấm tiếp tục / chuyển câu")]
    public AudioSource audioSource;
    public AudioClip advanceSound;

    [Header("Dialogue Content")]
    [TextArea(2, 5)]
    public string[] dialogueLines = new string[]
    {
        "Chào bạn! Tôi là Haku, nhân viên của công viên này. Rất vui được đón tiếp bạn đến với thế giới giải trí kỳ thú!",
        "Đầu tiên, bạn hãy tiến lại Quầy Bán Vé ngay phía trước để chọn mua vé tàu lượn siêu tốc nhé.",
        "Sau khi có vé, hãy đi theo biển chỉ dẫn đến khu vực đường ray để bắt đầu chuyến đi. Chúc bạn có những phút giây thật tuyệt vời!"
    };

    [Header("Events")]
    public Action onDialogueComplete;

    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private string currentFullText = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        GameStarted = false;

        if (dialoguePanel == null) dialoguePanel = gameObject;
        ConfigureSeamlessSky();
        AddCloudDrift();
    }

    private void Start()
    {
        if (fullScreenClickButton != null)
        {
            fullScreenClickButton.onClick.AddListener(OnUserAdvance);
        }

        // Tự động bắt đầu thoại khi vào Scene nếu Panel đang active
        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (dialoguePanel == null || !dialoguePanel.activeSelf) return;

        // Phím Enter, Space hoặc Click chuột để tiếp tục thoại
        if (Input.GetKeyDown(KeyCode.Return) || 
            Input.GetKeyDown(KeyCode.KeypadEnter) || 
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetMouseButtonDown(0))
        {
            OnUserAdvance();
        }
    }

    public void StartDialogue()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        currentLineIndex = 0;
        LockPlayer(true);

        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            ShowLine(currentLineIndex);
        }
        else
        {
            EndDialogue();
        }
    }

    private void ShowLine(int index)
    {
        if (index < 0 || index >= dialogueLines.Length)
        {
            EndDialogue();
            return;
        }

        currentFullText = dialogueLines[index];
        if (continuePrompt != null) continuePrompt.SetActive(false);

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypewriterRoutine(currentFullText));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        isTyping = true;
        if (dialogueText != null) dialogueText.text = "";

        // Hỗ trợ gõ từng ký tự mượt mà, bỏ qua tag rich text <color>.. nếu có
        for (int i = 0; i < fullText.Length; i++)
        {
            if (fullText[i] == '<')
            {
                int closeTag = fullText.IndexOf('>', i);
                if (closeTag > i)
                {
                    i = closeTag;
                    if (dialogueText != null) dialogueText.text = fullText.Substring(0, i + 1);
                    continue;
                }
            }

            if (dialogueText != null)
            {
                dialogueText.text = fullText.Substring(0, i + 1);
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        if (dialogueText != null) dialogueText.text = fullText;
        if (continuePrompt != null) continuePrompt.SetActive(true);
    }

    public void OnUserAdvance()
    {
        // 1. Nếu đang gõ chữ dở dang -> Hiện trọn vẹn cả câu ngay lập tức
        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            isTyping = false;
            if (dialogueText != null) dialogueText.text = currentFullText;
            if (continuePrompt != null) continuePrompt.SetActive(true);
            PlaySound();
            return;
        }

        // 2. Nếu đã gõ xong câu hiện tại -> Phát âm thanh và chuyển sang câu tiếp theo
        PlaySound();
        currentLineIndex++;

        if (currentLineIndex < dialogueLines.Length)
        {
            ShowLine(currentLineIndex);
        }
        else
        {
            // Đã hết câu thoại -> Kết thúc cutscene
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        isTyping = false;

        GameStarted = true;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // Mở khóa cho player di chuyển
        LockPlayer(false);

        // Báo cho TicketShopUIManager biết để đồng bộ
        if (TicketShopUIManager.Instance != null)
        {
            TicketShopUIManager.Instance.CloseWelcome();
        }

        onDialogueComplete?.Invoke();
    }

    private void PlaySound()
    {
        if (audioSource != null && advanceSound != null)
        {
            audioSource.PlayOneShot(advanceSound);
        }
    }

    private static void ConfigureSeamlessSky()
    {
        GameObject lowCloudPlane = GameObject.Find("Cloud_Sky_Low");
        if (lowCloudPlane != null) lowCloudPlane.SetActive(false);

        GameObject highCloudPlane = GameObject.Find("Cloud_Sky_High");
        if (highCloudPlane != null) highCloudPlane.SetActive(false);

    }

    private static void AddCloudDrift()
    {
        foreach (GameObject cloud in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            string name = cloud.name.ToLowerInvariant();
            if (cloud.transform.parent == null && (name == "cloud" || name.StartsWith("cloud (")) &&
                cloud.GetComponent<CloudDrift>() == null)
            {
                cloud.AddComponent<CloudDrift>();
            }
        }
    }

    private void LockPlayer(bool isLocked)
    {
        PlayerMovement pm = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (pm != null) pm.enabled = !isLocked;

        AnimatorChihiro ac = UnityEngine.Object.FindFirstObjectByType<AnimatorChihiro>();
        if (ac != null) ac.enabled = !isLocked;

        ShiftOrbitCamera cameraControl = UnityEngine.Object.FindFirstObjectByType<ShiftOrbitCamera>();
        if (cameraControl != null) cameraControl.enabled = !isLocked;

        if (isLocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
