using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SelectedCounterVisual : MonoBehaviour
{
    [SerializeField] private BaseCounter baseCounter;
    // [SerializeField] private GameObject[] visualGameObjectArray;

    [SerializeField] private List<MeshRenderer> _modelMeshRendererList;

    [Header("视觉效果")]
    [SerializeField] private Material _selectedMaterial;
    [SerializeField] private float _selectScale = 1.05f;
    [SerializeField] private float _duration = 0.5f;
    private List<Material> _originMaterial;


    private void Start()
    {
        _originMaterial = new List<Material>();

        for (int i = 0; i < _modelMeshRendererList.Count; i++)
        {
            if (_modelMeshRendererList[i] != null)
            {
                _originMaterial.Add(_modelMeshRendererList[i].material);
            }
        }
        // PlayerController.Instance.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;
        // NetworkPlayerController.Inatance.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;
    }

    public void AddListener(PlayerController _controller)
    {
        _controller.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;
    }

    private void Player_OnSelectedCounterChanged(object sender, PlayerController.OnSelectedCounterChangedEventArgs e)
    {
        if (e.SelectedCounter == baseCounter)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
        transform.DOScale(_selectScale, _duration);
        foreach (var meshRender in _modelMeshRendererList)
        {
            meshRender.material = _selectedMaterial;
        }
    }

    private void Hide()
    {
        transform.DOScale(1f, _duration);
        for (int i = 0; i < _modelMeshRendererList.Count; i++)
        {
            if (_originMaterial[i] != null)
                _modelMeshRendererList[i].material = _originMaterial[i];
        }
    }
}
