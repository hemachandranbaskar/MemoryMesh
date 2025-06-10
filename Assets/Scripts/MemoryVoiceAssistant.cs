using System.Collections;
using UnityEngine;
using TMPro;

public class MemoryVoiceAssistant : MonoBehaviour
{
    [Header("Voice & Audio")]
    public GroqTTS voiceTTS;
    public AudioSource ambientAudioSource;

    [Header("UI")]
    public CanvasGroup bannerCanvasGroup;
    public TextMeshProUGUI bannerTitle;
    public float fadeDuration = 1.5f;
    public float displayDuration = 3f;

    private Coroutine bannerRoutine;

    void Start()
    {
        int anchorCount = PlayerPrefs.GetInt("numUuids", 0);
        if (anchorCount > 0)
        {
            OnReloaded();
            ShowBanner("Welcome back to MemoryMesh");
        }
        else
        {
            Say("Welcome to MemoryMesh. I’m your memory guide. Let’s start capturing what matters most to you.");
            ShowBanner("Welcome to MemoryMesh");
        }

        // Start ambient audio if assigned
        if (ambientAudioSource != null && !ambientAudioSource.isPlaying)
        {
            ambientAudioSource.loop = true;
            ambientAudioSource.Play();
        }
    }

    public void Say(string message)
    {
        StartCoroutine(SpeakWithDelay(message));
    }

    private IEnumerator SpeakWithDelay(string message)
    {
        if (voiceTTS != null)
        {
            _ = voiceTTS.GenerateAndPlaySpeech(message);
            float estimatedDuration = Mathf.Max(message.Length / 15f, 1.5f); // estimate
            yield return new WaitForSeconds(estimatedDuration + 0.5f); // add delay after speech
        }
        else
        {
            Debug.Log("[MemoryVoiceAssistant] Speaking: " + message);
            yield return new WaitForSeconds(2f); // default wait if no TTS
        }
    }

    public void ShowBanner(string message)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(FadeBanner(message));
    }

    private IEnumerator FadeBanner(string message)
    {
        bannerTitle.text = message;
        bannerCanvasGroup.alpha = 0;
        bannerCanvasGroup.gameObject.SetActive(true);

        // Fade in
        float t = 0;
        while (t < fadeDuration)
        {
            bannerCanvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        bannerCanvasGroup.alpha = 1;

        yield return new WaitForSeconds(displayDuration);

        // Fade out
        t = 0;
        while (t < fadeDuration)
        {
            bannerCanvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        bannerCanvasGroup.alpha = 0;
        bannerCanvasGroup.gameObject.SetActive(false);
    }

    // Convenience events
    public void OnPromptReceived(string prompt)
    {
        Say($"Got it — {prompt}. Creating a memory image for you now.");
    }

    public void OnGenerating()
    {
        Say("Thinking in metaphors… generating your visual.");
    }

    public void OnImageReady()
    {
        Say("All set! You can now place your memory anywhere around you. Press the A button to anchor it.");
    }

    public void OnAnchorPlaced(string prompt)
    {
        Say($"Anchor placed for {prompt}. What would you like to remember next?");
    }

    public void OnReloaded()
    {
        Say("Welcome back to MemoryMesh. Press the B button to restore your memories.");
    }

    public void OnLoadingMemories()
    {
        Say("Loading your memory anchors.");
    }
}
