using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeliveryResultUI : MonoBehaviour
{
    private const string POPUP = "Popup";

    [SerializeField] private Image backgroundImage;
    //[SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Color sucessColor;
    [SerializeField] private Color failedColor;

    [SerializeField] private GameObject sucessUI;
    [SerializeField] private GameObject failUI;
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();

        sucessUI.SetActive(false);
        failUI.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        NetworkDeliveryManager.Instance.OnRecipeSuccess += DeliveryManager_OnRecipeSuccess;
        NetworkDeliveryManager.Instance.OnRecipeFailed += DeliveryManager_OnRecipeFailed;

        Debug.Log("DeliveryResultUI subscribed to DeliveryManager events");
        // Keep root active to avoid activation edge cases; child UIs are controlled explicitly
        gameObject.SetActive(true);
    }

    private void DeliveryManager_OnRecipeFailed(object sender, EventArgs e)
    {
        gameObject.SetActive(true);
        animator.ResetTrigger(POPUP);
        animator.SetTrigger(POPUP);
        messageText.text = "Delivery\nFailed";
        messageText.color = failedColor;
        //iconImage.sprite = failedSprite;
        failUI.SetActive(true);
        sucessUI.SetActive(false);  
    }

    private void DeliveryManager_OnRecipeSuccess(object sender, EventArgs e)
    {
        gameObject.SetActive(true);
        animator.ResetTrigger(POPUP);
        animator.SetTrigger(POPUP);
        messageText.text = "Delivery\nSuccess";
        messageText.color = sucessColor;
        //iconImage.sprite = sucessSprite;
        sucessUI.SetActive(true);
        failUI.SetActive(false);
    }

    //public void OnAnimatorEnd()
    //{
    //    sucessUI.SetActive(false);
    //    failUI.SetActive(false);
    //    // 隐藏根对象以便下一次可以通过 SetActive(true) 重新触发动画
    //    gameObject.SetActive(false);
    //}
}