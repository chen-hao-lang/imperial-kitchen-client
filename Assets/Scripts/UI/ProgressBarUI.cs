using UnityEngine;
using UnityEngine.UI;

public class ProgressBarUI : MonoBehaviour
{
    [SerializeField] private GameObject hasPrograssGameobject;
    [SerializeField] private Image barImage;

    private IHasPrograss hasPrograss;

    // Start is called before the first frame update
    void Start()
    {
        hasPrograss = hasPrograssGameobject.GetComponent<IHasPrograss>();
        hasPrograss.OnProgressChanged += HasPrograss_OnProgressChanged;
        barImage.fillAmount = 0;
        Hide();
    }

    private void Show()
    {
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void HasPrograss_OnProgressChanged(object sender, IHasPrograss.OnPrograssChangeEventArgs e)
    {
        barImage.fillAmount = e.prograssNormalized;

        if (e.prograssNormalized == 0f || e.prograssNormalized == 1f)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }
}
