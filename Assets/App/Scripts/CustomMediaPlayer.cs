using System;
using System.Collections;
using RenderHeads.Media.AVProVideo;
using UnityEngine;
using UnityEngine.Events;

public class CustomMediaPlayer : MonoBehaviour
{
    public Action OnVideoStarted;
    public Action OnVideoEnded;
    public Action OnVideoFailed;

    [System.Serializable]
    private class PlayerSlot
    {
        public MediaPlayer mediaPlayer;
        public CanvasGroup canvasGroup;
    }

    [Header("Players")]
    [SerializeField] private PlayerSlot player1;
    [SerializeField] private PlayerSlot player2;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float crossFadeDuration = 0.35f;
    [SerializeField] private AnimationCurve crossFadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool crossFadeAudio = true;
    [SerializeField, Range(0f, 1f)] private float playbackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float interruptionSwitchThreshold = 0.5f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFirstFrameReady;
    [SerializeField] private UnityEvent onTransitionCompleted;
    [SerializeField] private UnityEvent onMediaError;

    private PlayerSlot[] slots;
    private Coroutine crossFadeRoutine;
    private int activeSlotIndex = -1;
    private int currentVideoSlotIndex = -1;
    private int pendingSlotIndex = -1;
    private int requestVersion;
    private string pendingPath;

    private void Awake()
    {
        slots = new[] { player1, player2 };
        InitialiseSlots();
    }

    private void OnEnable()
    {
        AddPlayerEventListeners();
    }

    private void OnDisable()
    {
        RemovePlayerEventListeners();
        CancelCrossFade();
        requestVersion++;
        currentVideoSlotIndex = -1;
        pendingSlotIndex = -1;
        pendingPath = null;
    }

    /// <summary>
    /// Loads and plays a path relative to StreamingAssets. A newer call
    /// immediately supersedes any load or transition already in progress.
    /// </summary>
    public void PlayVideo(string relativeVideoPath)
    {
        if (string.IsNullOrWhiteSpace(relativeVideoPath))
        {
            Debug.LogWarning("CustomMediaPlayer received an empty video path.", this);
            OnVideoFailed?.Invoke();
            return;
        }

        if (!HasValidSlots())
        {
            Debug.LogError(
                "CustomMediaPlayer requires two different MediaPlayers and CanvasGroups.", this);
            OnVideoFailed?.Invoke();
            return;
        }

        requestVersion++;
        CancelCrossFade();
        PreserveMostVisibleFrame();

        int targetIndex = activeSlotIndex < 0 ? 0 : 1 - activeSlotIndex;
        PlayerSlot target = slots[targetIndex];

        pendingSlotIndex = targetIndex;
        pendingPath = NormaliseRelativePath(relativeVideoPath);

        SetCanvasState(target, 0f, false);
        target.canvasGroup.transform.SetAsLastSibling();
        StopAndRewind(target.mediaPlayer);
        target.mediaPlayer.Loop = false;
        target.mediaPlayer.AudioVolume = crossFadeAudio ? 0f : playbackVolume;

        bool opened = target.mediaPlayer.OpenMedia(
            MediaPathType.RelativeToStreamingAssetsFolder, pendingPath, false);

        if (!opened)
        {
            HandleOpenFailure(pendingPath);
        }
    }

    public void Stop()
    {
        requestVersion++;
        CancelCrossFade();
        activeSlotIndex = -1;
        currentVideoSlotIndex = -1;
        pendingSlotIndex = -1;
        pendingPath = null;

        if (slots == null)
        {
            return;
        }

        foreach (PlayerSlot slot in slots)
        {
            SetCanvasState(slot, 0f, false);
            StopAndRewind(slot != null ? slot.mediaPlayer : null);
        }
    }

    /// <summary>Fades out the displayed video, stops playback, then invokes the callback.</summary>
    public void Hide(Action onHidden = null)
    {
        requestVersion++;
        CancelCrossFade();
        currentVideoSlotIndex = -1;
        pendingSlotIndex = -1;
        pendingPath = null;
        if (!HasValidSlots() || !isActiveAndEnabled)
        {
            Stop();
            onHidden?.Invoke();
            return;
        }
        crossFadeRoutine = StartCoroutine(FadeOut(requestVersion, onHidden));
    }

    private IEnumerator FadeOut(int version, Action onHidden)
    {
        float[] alphas = { slots[0].canvasGroup.alpha, slots[1].canvasGroup.alpha };
        float[] volumes = { slots[0].mediaPlayer.AudioVolume, slots[1].mediaPlayer.AudioVolume };
        foreach (PlayerSlot slot in slots)
            SetCanvasState(slot, slot.canvasGroup.alpha, false);

        float elapsed = 0f;
        while (elapsed < crossFadeDuration && version == requestVersion)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float blend = Mathf.Clamp01(crossFadeCurve.Evaluate(Mathf.Clamp01(elapsed / crossFadeDuration)));
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].canvasGroup.alpha = alphas[i] * (1f - blend);
                if (crossFadeAudio)
                    slots[i].mediaPlayer.AudioVolume = volumes[i] * (1f - blend);
            }
            yield return null;
        }
        crossFadeRoutine = null;
        if (version != requestVersion) yield break;
        Stop();
        onHidden?.Invoke();
    }

    private void OnMediaPlayerEvent(
        MediaPlayer mediaPlayer,
        MediaPlayerEvent.EventType eventType,
        ErrorCode errorCode)
    {
        if (eventType == MediaPlayerEvent.EventType.FinishedPlaying &&
            currentVideoSlotIndex >= 0 &&
            slots[currentVideoSlotIndex].mediaPlayer == mediaPlayer)
        {
            currentVideoSlotIndex = -1;
            OnVideoEnded?.Invoke();
            return;
        }

        if (eventType == MediaPlayerEvent.EventType.Error &&
            currentVideoSlotIndex >= 0 && slots[currentVideoSlotIndex].mediaPlayer == mediaPlayer)
        {
            currentVideoSlotIndex = -1;
            onMediaError?.Invoke();
            OnVideoFailed?.Invoke();
            return;
        }

        if (pendingSlotIndex < 0 || slots[pendingSlotIndex].mediaPlayer != mediaPlayer)
        {
            return;
        }

        if (eventType == MediaPlayerEvent.EventType.Error)
        {
            HandleOpenFailure(pendingPath);
            return;
        }

        if (eventType != MediaPlayerEvent.EventType.FirstFrameReady ||
            mediaPlayer.MediaPath.Path != pendingPath)
        {
            return;
        }

        int version = requestVersion;
        int incomingIndex = pendingSlotIndex;
        pendingSlotIndex = -1;
        pendingPath = null;

        onFirstFrameReady?.Invoke();
        mediaPlayer.Rewind(false);
        mediaPlayer.Play();
        currentVideoSlotIndex = incomingIndex;
        OnVideoStarted?.Invoke();

        if (activeSlotIndex < 0)
        {
            activeSlotIndex = incomingIndex;
            SetCanvasState(slots[incomingIndex], 1f, true);
            mediaPlayer.AudioVolume = playbackVolume;
            onTransitionCompleted?.Invoke();
            return;
        }

        crossFadeRoutine = StartCoroutine(
            CrossFade(activeSlotIndex, incomingIndex, version));
    }

    private IEnumerator CrossFade(int outgoingIndex, int incomingIndex, int version)
    {
        PlayerSlot outgoing = slots[outgoingIndex];
        PlayerSlot incoming = slots[incomingIndex];

        incoming.canvasGroup.transform.SetAsLastSibling();
        incoming.canvasGroup.interactable = false;
        incoming.canvasGroup.blocksRaycasts = false;
        outgoing.canvasGroup.alpha = 1f;

        float duration = Mathf.Max(0f, crossFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration && version == requestVersion)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float blend = Mathf.Clamp01(crossFadeCurve.Evaluate(progress));

            incoming.canvasGroup.alpha = blend;

            if (crossFadeAudio)
            {
                incoming.mediaPlayer.AudioVolume = playbackVolume * blend;
                outgoing.mediaPlayer.AudioVolume = playbackVolume * (1f - blend);
            }

            yield return null;
        }

        crossFadeRoutine = null;
        if (version != requestVersion)
        {
            yield break;
        }

        SetCanvasState(incoming, 1f, true);
        SetCanvasState(outgoing, 0f, false);
        incoming.mediaPlayer.AudioVolume = playbackVolume;
        StopAndRewind(outgoing.mediaPlayer);
        activeSlotIndex = incomingIndex;
        onTransitionCompleted?.Invoke();
    }

    private void PreserveMostVisibleFrame()
    {
        int visibleIndex = GetMostVisibleSlot();
        if (visibleIndex < 0)
        {
            activeSlotIndex = -1;
            return;
        }

        activeSlotIndex = visibleIndex;
        currentVideoSlotIndex = visibleIndex;
        PlayerSlot visible = slots[visibleIndex];
        PlayerSlot reusable = slots[1 - visibleIndex];

        SetCanvasState(visible, 1f, true);
        visible.mediaPlayer.AudioVolume = playbackVolume;
        SetCanvasState(reusable, 0f, false);
        StopAndRewind(reusable.mediaPlayer);
    }

    private int GetMostVisibleSlot()
    {
        bool firstVisible = IsDisplayingMedia(0);
        bool secondVisible = IsDisplayingMedia(1);

        if (!firstVisible && !secondVisible) return -1;
        if (!firstVisible) return 1;
        if (!secondVisible) return 0;

        bool shareParent = slots[0].canvasGroup.transform.parent ==
                           slots[1].canvasGroup.transform.parent;
        if (shareParent)
        {
            int topIndex = slots[1].canvasGroup.transform.GetSiblingIndex() >
                           slots[0].canvasGroup.transform.GetSiblingIndex() ? 1 : 0;

            if (slots[topIndex].canvasGroup.alpha >= interruptionSwitchThreshold)
            {
                return topIndex;
            }

            return 1 - topIndex;
        }

        return slots[1].canvasGroup.alpha > slots[0].canvasGroup.alpha ? 1 : 0;
    }

    private bool IsDisplayingMedia(int index)
    {
        PlayerSlot slot = slots[index];
        return slot != null &&
               slot.mediaPlayer != null &&
               slot.canvasGroup != null &&
               slot.canvasGroup.alpha > 0f &&
               slot.mediaPlayer.MediaPath != null &&
               !string.IsNullOrEmpty(slot.mediaPlayer.MediaPath.Path);
    }

    private void InitialiseSlots()
    {
        if (!HasValidSlots())
        {
            return;
        }

        foreach (PlayerSlot slot in slots)
        {
            slot.mediaPlayer.Loop = false;
            slot.mediaPlayer.AudioVolume = 0f;
            SetCanvasState(slot, 0f, false);
        }

        player2.canvasGroup.transform.SetAsFirstSibling();
        player1.canvasGroup.transform.SetAsLastSibling();
    }

    private void AddPlayerEventListeners()
    {
        if (slots == null) return;
        foreach (PlayerSlot slot in slots)
        {
            if (slot != null && slot.mediaPlayer != null)
            {
                slot.mediaPlayer.Events.AddListener(OnMediaPlayerEvent);
            }
        }
    }

    private void RemovePlayerEventListeners()
    {
        if (slots == null) return;
        foreach (PlayerSlot slot in slots)
        {
            if (slot != null && slot.mediaPlayer != null)
            {
                slot.mediaPlayer.Events.RemoveListener(OnMediaPlayerEvent);
            }
        }
    }

    private void CancelCrossFade()
    {
        if (crossFadeRoutine == null) return;
        StopCoroutine(crossFadeRoutine);
        crossFadeRoutine = null;
    }

    private void HandleOpenFailure(string path)
    {
        Debug.LogError($"CustomMediaPlayer failed to load StreamingAssets video: {path}", this);
        if (pendingSlotIndex >= 0)
        {
            PlayerSlot failed = slots[pendingSlotIndex];
            SetCanvasState(failed, 0f, false);
            StopAndRewind(failed.mediaPlayer);
        }

        pendingSlotIndex = -1;
        pendingPath = null;
        onMediaError?.Invoke();
        OnVideoFailed?.Invoke();
    }

    private bool HasValidSlots()
    {
        return slots != null &&
               slots.Length == 2 &&
               slots[0] != null &&
               slots[1] != null &&
               slots[0].mediaPlayer != null &&
               slots[1].mediaPlayer != null &&
               slots[0].canvasGroup != null &&
               slots[1].canvasGroup != null &&
               slots[0].mediaPlayer != slots[1].mediaPlayer &&
               slots[0].canvasGroup != slots[1].canvasGroup;
    }

    private static void SetCanvasState(PlayerSlot slot, float alpha, bool interactive)
    {
        if (slot == null || slot.canvasGroup == null) return;
        slot.canvasGroup.alpha = alpha;
        slot.canvasGroup.interactable = interactive;
        slot.canvasGroup.blocksRaycasts = interactive;
    }

    private static void StopAndRewind(MediaPlayer mediaPlayer)
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Stop();
        mediaPlayer.Rewind(false);
    }

    private static string NormaliseRelativePath(string path)
    {
        return path.Trim().TrimStart('/', '\\').Replace('\\', '/');
    }
}
