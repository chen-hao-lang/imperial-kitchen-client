using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainPanelVisual : MonoBehaviour
{
    [SerializeField] private Image gameTietle;
    [SerializeField] private Image startBtnImage;
    [SerializeField] private Image endBtnImage;
    [SerializeField] private float parallaxIntensity = 10f;
    [SerializeField] private float smoothTime = 0.08f;

    private Vector2 velocityGameTitle;
    private Vector2 velocityStartBtn;
    private Vector2 velocityEndBtn;

    // Update is called once per frame
    void Update()
    {
        // 读取鼠标归一化位置（由 PlayerInput 提供）
        if (PlayerInput.Instance == null) return;

        Vector2 mouse = PlayerInput.Instance.MouseScreenPositionNormalized;

        // 目标偏移（屏幕坐标系到像素偏移）
        Vector2 targetTitle = mouse * parallaxIntensity * 0.5f;
        Vector2 targetStart = mouse * parallaxIntensity * 1.0f;
        Vector2 targetEnd = mouse * parallaxIntensity * 0.7f;

        // 平滑过渡到目标位置（使用 RectTransform.anchoredPosition）
        if (gameTietle != null)
        {
            RectTransform rt = gameTietle.rectTransform;
            Vector2 cur = rt.anchoredPosition;
            Vector2 next = Vector2.SmoothDamp(cur, targetTitle, ref velocityGameTitle, smoothTime);
            rt.anchoredPosition = next;
        }

        if (startBtnImage != null)
        {
            RectTransform rt = startBtnImage.rectTransform;
            Vector2 cur = rt.anchoredPosition;
            Vector2 next = Vector2.SmoothDamp(cur, targetStart, ref velocityStartBtn, smoothTime);
            rt.anchoredPosition = next;
        }

        if (endBtnImage != null)
        {
            RectTransform rt = endBtnImage.rectTransform;
            Vector2 cur = rt.anchoredPosition;
            Vector2 next = Vector2.SmoothDamp(cur, targetEnd, ref velocityEndBtn, smoothTime);
            rt.anchoredPosition = next;
        }
    }
}