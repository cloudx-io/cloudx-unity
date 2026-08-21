using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * Demo-only UI. Publishers can ignore this file — CloudX calls live in HomeScreen.
 * This class wires buttons and reflows the sample layout on rotate.
 */
[RequireComponent(typeof(Canvas))]
public class HomeScreenUi : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";

    public sealed class Actions
    {
        public Action ShowBanner;
        public Action ToggleMrec;
        public Action ShowInterstitial;
        public Action ShowRewarded;
        public Action<bool> OnOrientationChanged;
    }

    public Button showBannerButton;
    public Button showMrecButton;
    public Button showInterstitialButton;
    public Button showRewardedButton;
    public Text initializationStatusText;
    public Text interstitialStatusText;
    public Text rewardedStatusText;

    private struct ControlSnapshot
    {
        public RectTransform Rect;
        public Transform Parent;
        public int SiblingIndex;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
    }

    private struct TextStyleSnapshot
    {
        public Text Text;
        public int FontSize;
        public bool BestFit;
        public int MinSize;
        public int MaxSize;
    }

    private const float LandscapeCompactButtonWidth = 240f;
    private const float LandscapeCompactButtonHeight = 36f;
    private const float LandscapeCompactStatusWidth = 300f;
    private const float LandscapeCompactStatusHeight = 28f;

    private Actions _actions;
    private Canvas _canvas;
    private ControlSnapshot[] _portraitSnapshots;
    private TextStyleSnapshot[] _portraitTextStyles;
    private RectTransform _landscapeTitle;
    private RectTransform[] _landscapeMainLeft;
    private RectTransform[] _landscapeMainRight;
    private RectTransform _landscapeViewport;
    private RectTransform _landscapeContent;
    private bool _layoutReady;
    private bool _controlsParentedToLandscape;
    private bool _appliedLandscape;
    private bool _orientationApplied;
    private int _lastWidth;
    private int _lastHeight;
    private float _lastScaleFactor;
    private Rect _lastSafeArea;

    private static void Log(string message) => Debug.Log($"[{TAG}] {message}");

    /* Wires the on-screen buttons to the CloudX actions in HomeScreen. */
    public void Bind(Actions actions)
    {
        _actions = actions;

        showBannerButton.onClick.AddListener(() => _actions.ShowBanner());
        showMrecButton.onClick.AddListener(() => _actions.ToggleMrec());
        showInterstitialButton.onClick.AddListener(() => _actions.ShowInterstitial());
        showRewardedButton.onClick.AddListener(() => _actions.ShowRewarded());

        SetupOrientationLayout();
    }

    /*
     * Ad actions stay inert until the SDK answers. A load issued before ATT
     * resolves goes out as do-not-track and never fills, even once the user
     * grants permission, and a Show tap before CreateBanner leaves the demo
     * flagged as showing a banner that does not exist.
     */
    public void SetActionsInteractable(bool interactable)
    {
        SetButtonInteractable(showBannerButton, interactable);
        SetButtonInteractable(showMrecButton, interactable);
        SetButtonInteractable(showInterstitialButton, interactable);
        SetButtonInteractable(showRewardedButton, interactable);
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    public void SetInitializationStatus(string text)
    {
        initializationStatusText.text = text;
    }

    public void SetInterstitialStatus(string text)
    {
        interstitialStatusText.text = text;
    }

    public void SetRewardedStatus(string text)
    {
        rewardedStatusText.text = text;
    }

    public void SetBannerButtonLabel(string text)
    {
        showBannerButton.GetComponentInChildren<Text>().text = text;
    }

    public void SetMrecButtonLabel(string text)
    {
        showMrecButton.GetComponentInChildren<Text>().text = text;
    }

    #region Orientation Layout

    /*
     * Portrait keeps the scene RectTransforms. Landscape insets a full-height viewport so the
     * native vertical banner has empty left/right gutters, and reparents the same controls into
     * two fractional columns. Switching horizontal <-> vertical on rotate is HomeScreen's job
     * via OnOrientationChanged.
     */
    private void SetupOrientationLayout()
    {
        _canvas = GetComponent<Canvas>();
        _landscapeTitle = transform.Find("AppTitle") as RectTransform;
        _landscapeMainLeft = CollectRects(
            showBannerButton,
            showMrecButton,
            showInterstitialButton,
            showRewardedButton);
        _landscapeMainRight = CollectRects(
            initializationStatusText,
            interstitialStatusText,
            rewardedStatusText);

        var snapshotRects = new List<RectTransform>();
        if (_landscapeTitle != null)
            snapshotRects.Add(_landscapeTitle);
        snapshotRects.AddRange(_landscapeMainLeft);
        snapshotRects.AddRange(_landscapeMainRight);
        _portraitSnapshots = new ControlSnapshot[snapshotRects.Count];
        for (var i = 0; i < snapshotRects.Count; i++)
        {
            var rect = snapshotRects[i];
            _portraitSnapshots[i] = new ControlSnapshot
            {
                Rect = rect,
                Parent = rect.parent,
                SiblingIndex = rect.GetSiblingIndex(),
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
            };
        }
        CapturePortraitTextStyles(snapshotRects);

        CreateLandscapeViewport();
        _layoutReady = true;
        ApplyOrientationLayout(Screen.width > Screen.height);
    }

    private static RectTransform[] CollectRects(params Component[] components)
    {
        var rects = new List<RectTransform>(components.Length);
        foreach (var component in components)
        {
            if (component == null)
                continue;
            var rect = component.transform as RectTransform;
            if (rect != null)
                rects.Add(rect);
        }
        return rects.ToArray();
    }

    private void CreateLandscapeViewport()
    {
        var viewportObj = new GameObject("LandscapeViewport");
        viewportObj.transform.SetParent(transform, false);
        _landscapeViewport = viewportObj.AddComponent<RectTransform>();
        _landscapeViewport.anchorMin = Vector2.zero;
        _landscapeViewport.anchorMax = Vector2.one;
        _landscapeViewport.offsetMin = Vector2.zero;
        _landscapeViewport.offsetMax = Vector2.zero;

        var image = viewportObj.AddComponent<Image>();
        image.color = new Color(0.08f, 0.14f, 0.38f, 1f);
        image.raycastTarget = true;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        _landscapeContent = contentObj.AddComponent<RectTransform>();
        _landscapeContent.anchorMin = Vector2.zero;
        _landscapeContent.anchorMax = Vector2.one;
        _landscapeContent.offsetMin = Vector2.zero;
        _landscapeContent.offsetMax = Vector2.zero;

        viewportObj.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!_layoutReady)
            return;

        var landscape = Screen.width > Screen.height;
        var safeArea = Screen.safeArea;
        var scale = _canvas != null ? _canvas.scaleFactor : 0f;
        if (landscape == _appliedLandscape
            && Screen.width == _lastWidth
            && Screen.height == _lastHeight
            && safeArea == _lastSafeArea
            && Mathf.Abs(scale - _lastScaleFactor) < 0.001f)
        {
            return;
        }

        ApplyOrientationLayout(landscape);
    }

    private void ApplyOrientationLayout(bool landscape)
    {
        var previousLandscape = _appliedLandscape;
        var firstApply = !_orientationApplied;
        _orientationApplied = true;
        _appliedLandscape = landscape;
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        _lastScaleFactor = _canvas != null ? _canvas.scaleFactor : 0f;
        _lastSafeArea = Screen.safeArea;

        if (!landscape)
        {
            if (_controlsParentedToLandscape)
                RestorePortraitLayout();
            if (_landscapeViewport != null)
                _landscapeViewport.gameObject.SetActive(false);
            NotifyOrientationChanged(firstApply, previousLandscape, landscape);
            return;
        }

        if (!_controlsParentedToLandscape)
            ParentControlsToLandscape();

        _landscapeViewport.gameObject.SetActive(true);
        ApplyLandscapeGutters();
        SetActiveAll(_landscapeMainLeft, true);
        SetActiveAll(_landscapeMainRight, true);
        if (_landscapeTitle != null)
            _landscapeTitle.gameObject.SetActive(true);
        Log($"Landscape layout applied: {Screen.width}x{Screen.height} safeArea={Screen.safeArea}");
        NotifyOrientationChanged(firstApply, previousLandscape, landscape);
    }

    private void NotifyOrientationChanged(bool firstApply, bool previousLandscape, bool landscape)
    {
        if (firstApply || previousLandscape == landscape)
            return;

        _actions?.OnOrientationChanged?.Invoke(landscape);
    }

    private void ParentControlsToLandscape()
    {
        _landscapeViewport.gameObject.SetActive(false);
        foreach (var snap in _portraitSnapshots)
            snap.Rect.SetParent(_landscapeContent, false);
        PlaceAnchored(_landscapeTitle, 0.5f, 0.90f, 520f, 28f);
        PlaceCompactStack(
            _landscapeMainLeft, 0.28f, 0.72f,
            LandscapeCompactButtonWidth, LandscapeCompactButtonHeight, 0.12f);
        PlaceCompactStack(
            _landscapeMainRight, 0.72f, 0.72f,
            LandscapeCompactStatusWidth, LandscapeCompactStatusHeight, 0.12f);
        ApplyLandscapeMainTypography();
        _controlsParentedToLandscape = true;
    }

    private static void PlaceAnchored(RectTransform rect, float anchorX, float anchorY, float width, float height)
    {
        if (rect == null)
            return;
        rect.anchorMin = new Vector2(anchorX, anchorY);
        rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void PlaceCompactStack(
        RectTransform[] rects, float anchorX, float topY, float width, float height, float step)
    {
        for (var i = 0; i < rects.Length; i++)
            PlaceAnchored(rects[i], anchorX, topY - i * step, width, height);
    }

    private static void SetActiveAll(RectTransform[] rects, bool active)
    {
        foreach (var rect in rects)
        {
            if (rect != null)
                rect.gameObject.SetActive(active);
        }
    }

    private void CapturePortraitTextStyles(List<RectTransform> roots)
    {
        var styles = new List<TextStyleSnapshot>();
        foreach (var root in roots)
        {
            if (root == null)
                continue;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                styles.Add(new TextStyleSnapshot
                {
                    Text = text,
                    FontSize = text.fontSize,
                    BestFit = text.resizeTextForBestFit,
                    MinSize = text.resizeTextMinSize,
                    MaxSize = text.resizeTextMaxSize,
                });
            }
        }
        _portraitTextStyles = styles.ToArray();
    }

    private void ApplyLandscapeMainTypography()
    {
        ApplyCompactText(_landscapeTitle != null ? _landscapeTitle.GetComponent<Text>() : null, 18, 12, 20);
        foreach (var rect in _landscapeMainLeft)
        {
            if (rect == null)
                continue;
            foreach (var text in rect.GetComponentsInChildren<Text>(true))
                ApplyCompactText(text, 14, 10, 16);
        }
        foreach (var rect in _landscapeMainRight)
        {
            if (rect == null)
                continue;
            foreach (var text in rect.GetComponentsInChildren<Text>(true))
                ApplyCompactText(text, 14, 10, 16);
        }
    }

    private static void ApplyCompactText(Text text, int fontSize, int minSize, int maxSize)
    {
        if (text == null)
            return;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
    }

    private void RestorePortraitTextStyles()
    {
        if (_portraitTextStyles == null)
            return;
        foreach (var snap in _portraitTextStyles)
        {
            if (snap.Text == null)
                continue;
            snap.Text.fontSize = snap.FontSize;
            snap.Text.resizeTextForBestFit = snap.BestFit;
            snap.Text.resizeTextMinSize = snap.MinSize;
            snap.Text.resizeTextMaxSize = snap.MaxSize;
        }
    }

    private void RestorePortraitLayout()
    {
        _landscapeViewport.gameObject.SetActive(false);
        foreach (var snap in _portraitSnapshots)
            snap.Rect.SetParent(snap.Parent, false);
        foreach (var snap in _portraitSnapshots)
        {
            snap.Rect.anchorMin = snap.AnchorMin;
            snap.Rect.anchorMax = snap.AnchorMax;
            snap.Rect.pivot = snap.Pivot;
            snap.Rect.anchoredPosition = snap.AnchoredPosition;
            snap.Rect.sizeDelta = snap.SizeDelta;
        }
        for (var targetIndex = 0; targetIndex < transform.childCount; targetIndex++)
        {
            foreach (var snap in _portraitSnapshots)
            {
                if (snap.SiblingIndex == targetIndex)
                {
                    snap.Rect.SetSiblingIndex(targetIndex);
                    break;
                }
            }
        }
        foreach (var snap in _portraitSnapshots)
            snap.Rect.gameObject.SetActive(true);
        RestorePortraitTextStyles();
        _controlsParentedToLandscape = false;
        Log("Portrait layout restored");
    }

    private void ApplyLandscapeGutters()
    {
        var dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
        var bannerPx = 50f * (dpi / 160f) + 8f;
        var leftPx = Mathf.Max(Screen.safeArea.xMin, LeftCutoutInnerX()) + bannerPx;
        var rightPx = Mathf.Max(Screen.width - Screen.safeArea.xMax, RightCutoutInnerInset()) + bannerPx;
        var scale = _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;

        _landscapeViewport.offsetMin = new Vector2(leftPx / scale, 0f);
        _landscapeViewport.offsetMax = new Vector2(-rightPx / scale, 0f);

        var bottomPx = Screen.safeArea.yMin;
        var topPx = Screen.height - Screen.safeArea.yMax;
        _landscapeContent.offsetMin = new Vector2(0f, bottomPx / scale);
        _landscapeContent.offsetMax = new Vector2(0f, -topPx / scale);
    }

    private static float LeftCutoutInnerX()
    {
        var inner = 0f;
        foreach (var cutout in Screen.cutouts)
        {
            if (cutout.xMin <= 0.5f)
                inner = Mathf.Max(inner, cutout.xMax);
        }
        return inner;
    }

    private static float RightCutoutInnerInset()
    {
        var inset = 0f;
        foreach (var cutout in Screen.cutouts)
        {
            if (cutout.xMax >= Screen.width - 0.5f)
                inset = Mathf.Max(inset, Screen.width - cutout.xMin);
        }
        return inset;
    }

    #endregion
}
