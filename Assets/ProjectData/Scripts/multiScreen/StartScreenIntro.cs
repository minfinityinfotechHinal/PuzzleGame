using UnityEngine;
using DG.Tweening;

public class StartScreenIntro : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform title;       // PUZZLE MASTER
    public RectTransform startButton; // Start button

    [Header("Animation Settings")]
    public float titleDuration = 0.6f;
    public float buttonDuration = 0.5f;

    public float delayBetween = 0.2f;

    private Vector2 buttonStartPos;
    private Vector2 buttonTargetPos;

    void Start()
    {
        SetupInitialState();
        PlayIntro();
    }

    void SetupInitialState()
    {
        // 🔹 Title starts small
        title.localScale = Vector3.zero;

        // 🔹 Button starts below screen
        buttonTargetPos = startButton.anchoredPosition;
        buttonStartPos = buttonTargetPos - new Vector2(0, 500f);

        startButton.anchoredPosition = buttonStartPos;
        startButton.localScale = Vector3.one;
    }

    void PlayIntro()
    {
        Sequence seq = DOTween.Sequence();

        // 🔥 Title animation
        seq.Append(title.DOScale(1f, titleDuration)
            .SetEase(Ease.OutBack));

        // 🔥 Small delay
        seq.AppendInterval(delayBetween);

        // 🔥 Button slide + bounce
        seq.Append(startButton.DOAnchorPos(buttonTargetPos, buttonDuration)
            .SetEase(Ease.OutBack));

        // 🔥 Optional tiny punch
        seq.Append(startButton.DOPunchScale(Vector3.one * 0.1f, 0.2f));
    }
}