using UnityEngine;

public class CotainerCounterVisual : MonoBehaviour
{
    private Animator animator;
    private CotainerCounter cotainerCounter;
    private const string OPEN_CLOSE = "OpenClose";

    void Awake()
    {
        animator = GetComponent<Animator>();
        cotainerCounter = GetComponentInParent<CotainerCounter>();
    }

    void Start()
    {
        cotainerCounter.OnPlayerInteract += CotainerCounter_OnPlayerInteract;
    }

    void OnDestroy()
    {
        cotainerCounter.OnPlayerInteract -= CotainerCounter_OnPlayerInteract;
    }   

    /// <summary>
    /// 触发动画
    /// </summary>
    private void CotainerCounter_OnPlayerInteract()
    {
        animator.SetTrigger(OPEN_CLOSE);
    }
}
