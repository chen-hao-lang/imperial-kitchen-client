using UnityEngine;

public class CuttingCounterVisual : MonoBehaviour
{
    private const string CUT = "Cut";

    private CuttingCounter _cuttingCounter;
    private Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _cuttingCounter = GetComponentInParent<CuttingCounter>();
    }

    // Start is called before the first frame update
    void Start()
    {
        _cuttingCounter.OnPlayerInteractAlternate += CuttingCounter_OnCut;
    }

    private void CuttingCounter_OnCut()
    {
        _animator.SetTrigger(CUT);
    }
}