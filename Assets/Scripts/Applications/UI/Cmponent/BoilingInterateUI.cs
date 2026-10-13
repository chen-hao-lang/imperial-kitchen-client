using UnityEditor;
using UnityEngine;

public class BoilingInterateUI : MonoBehaviour
{
    [SerializeField] private BoilingCounter boilingCounter;
    [SerializeField] private Transform iconTemplate;

    void Start()
    {
        boilingCounter.OnInteracted += BoilingCounter_OnInteracted;
        boilingCounter.OnPlateCompleted += BoilingCounter_PlateCompleted;
    }

    private void BoilingCounter_PlateCompleted()
    {
        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if(child == iconTemplate) continue;//跳过UI模板
            Destroy(child.gameObject);
        }
    }

    private void BoilingCounter_OnInteracted(OnInteractedEventArgs e)
    {
        Transform iconTransform = Instantiate(iconTemplate, transform);
        iconTransform.gameObject.SetActive(true);
        iconTransform.GetComponent<PlateIconSingleUI>().SetKitchenObjectSO(e.kitchenObjectSO);
    }
}