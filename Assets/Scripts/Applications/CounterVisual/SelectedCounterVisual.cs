using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using LiteGameFramework;

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
        // PlayerController.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;

        EventManager.AddListener<OnLocalPlayerSpawned>(this, (_) =>
        {
            EventManager.AddListener<OnSelectedCounterChanged>(this,Player_OnSelectedCounterChanged);
        });
    }

    void OnDestroy()
    {
        // ⚠️ 必须传 this：仅移除本组件的订阅。
        // 无参重载 EventManager.RemoveAllListener() 会清空全局所有订阅，
        // 导致跨场景存活的面板（如 PlayerReadyPanel）监听永久丢失。
        EventManager.RemoveAllListener(this);
    }

    public void AddListener(PlayerController _controller)
    {
        // _controller.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;
    }

    private void Player_OnSelectedCounterChanged(OnSelectedCounterChanged _arg)
    {
        if (_arg.selectCounter == baseCounter)
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
