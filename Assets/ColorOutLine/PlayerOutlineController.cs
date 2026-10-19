using System.Collections.Generic;
using UnityEngine;

public class PlayerOutlineController : MonoBehaviour
{
    private const string OutlineShaderName = "Custom/PlayerOutline";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Material xrayMaterialAsset;

    private Renderer _renderer;
    private PlayerHeadIcon _headIcon;

    private Material _bodyMat;
    private Material _outlineMat;
    private Material _xrayMat;

    private bool _xrayOn;
    private bool _dead;
    private bool _initialized;

    void Awake()
    {
        Init();
    }

    // Start より先に呼ばれても安全なように、初期化は1回だけ行う
    private void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogError("PlayerOutlineController: このオブジェクトに Renderer がありません", this);
            return;
        }

        _headIcon = GetComponentInParent<PlayerHeadIcon>();

        // Materials 欄の並びに依存せず、シェーダー名で輪郭と本体を見分ける
        var mats = _renderer.materials;
        foreach (var m in mats)
        {
            if (m == null) continue;

            if (m.shader.name == OutlineShaderName)
            {
                _outlineMat = m;
            }
            else if (_bodyMat == null)
            {
                _bodyMat = m;
            }
        }

        if (_outlineMat == null)
        {
            Debug.LogWarning("PlayerOutlineController: Materials に Custom/PlayerOutline が見つかりません", this);
        }
        if (_bodyMat == null)
        {
            Debug.LogWarning("PlayerOutlineController: 本体マテリアルが見つかりません", this);
        }

        if (xrayMaterialAsset != null)
        {
            _xrayMat = new Material(xrayMaterialAsset);
        }

        ApplyMaterials();
    }

    void LateUpdate()
    {
        if (_headIcon == null) return;

        Color c = _headIcon.GetPlayerColor();
        if (_outlineMat != null) _outlineMat.SetColor(BaseColorId, c);
        if (_xrayMat != null) _xrayMat.SetColor(BaseColorId, c);
    }

    // 透視スキル・サドンデスの開始/終了で呼ぶ
    public void SetXRay(bool on)
    {
        Init();
        if (_xrayOn == on) return;
        _xrayOn = on;
        ApplyMaterials();
    }

    // 死亡で true、復活で false
    public void SetDead(bool dead)
    {
        Init();
        if (_dead == dead) return;
        _dead = dead;
        ApplyMaterials();
    }

    private void ApplyMaterials()
    {
        if (_renderer == null) return;

        var list = new List<Material>();
        if (_bodyMat != null) list.Add(_bodyMat);

        if (!_dead)
        {
            if (_outlineMat != null) list.Add(_outlineMat);
            if (_xrayOn && _xrayMat != null) list.Add(_xrayMat);
        }

        _renderer.sharedMaterials = list.ToArray();
    }

    void OnDestroy()
    {
        if (_outlineMat != null) Destroy(_outlineMat);
        if (_bodyMat != null) Destroy(_bodyMat);
        if (_xrayMat != null) Destroy(_xrayMat);
    }
}