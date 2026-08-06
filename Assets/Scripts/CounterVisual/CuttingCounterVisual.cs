using UnityEngine;

public class CuttingCounterVisual : MonoBehaviour
{
    private const string CUT = "Cut";

    private CuttingCounuter _cuttingCounter;
    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _cuttingCounter = GetComponentInParent<CuttingCounuter>();
    }

    // Start is called before the first frame update
    void Start()
    {
        _cuttingCounter.OnPlayerInteractAlternate += CuttingCounuter_OnCut;
    }

    private void CuttingCounuter_OnCut()
    {
        _animator.SetTrigger(CUT);
    }
}