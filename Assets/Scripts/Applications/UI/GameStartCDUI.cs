using System;
using TMPro;
using UnityEngine;

public class GameStartCDUI : MonoBehaviour
{
    private const string NUMBER_POPUP = "NumberPopup";

    [SerializeField] private TextMeshProUGUI cdText;

    private Animator animator;
    private int previousCDNumber;

    private void Awake()
    {
        animator = GetComponent<Animator>();    
    }

    // Start is called before the first frame update
    void Start()
    {
        KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;

        Hide();
    }

    private void Update()
    {
        int cdNumber = Mathf.CeilToInt(KitchenGameManager.Instance.GetCDToStartTimer());
        cdText.text = cdNumber.ToString();

        if(previousCDNumber != cdNumber)
        {
            previousCDNumber = cdNumber;
            animator.SetTrigger(NUMBER_POPUP);
            //TODO: play sound
        }
    }

    private void Show()
    {
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void KitchenGameManager_OnStateChanged(object sender, EventArgs e)
    {
        if (KitchenGameManager.Instance.IsCDToStartActive())
        {
            Show();
        }
        else
        {
            Hide();
        }
    }
}