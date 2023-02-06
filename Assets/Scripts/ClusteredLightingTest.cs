using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ClusteredLightingTest : MonoBehaviour
{
    [SerializeField]
    InputActionProperty m_MoveAction;
    
    [SerializeField]
    InputActionProperty m_TurnAction;
    
    [SerializeField, Range(0f, 0.2f)]
    float maxMoveLength = 0.05f;

    public Transform moveTrans;

    public Button funcBtn;
    
    public Transform prefab;
    
    [Header("Camera")][Range(0, 1)]
    public int renderIndex = 0;
    
    [Header("Procedural")]
    public bool procedural = true;
    
    public int instances = 1000;

    public float radius = 50f;

    public float lightRange = 20.0f;
    
    public float lightIntensity = 3.0f;

    private Camera curCamera;
    private UniversalAdditionalCameraData additionalCameraData;
    private int curRenderIndex;
    
    void Start()
    {
        if (procedural)
        {
            for (int i = 0; i < instances; i++) 
            {
                Transform t = Instantiate(prefab);
                t.localPosition = UnityEngine.Random.insideUnitSphere * radius;
                t.SetParent(transform);
            }
        
            prefab.gameObject.SetActive(false);
        
            for (int i = 0; i < 32; i++) 
            {
                var go = new GameObject();
                go.transform.localPosition = UnityEngine.Random.insideUnitSphere * radius;
                go.transform.SetParent(transform);

                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = lightRange;
                l.intensity = lightIntensity;

                var c = UnityEngine.Random.insideUnitSphere;
                l.color = new Color(c.x, c.y, c.z, 1.0f);
            }
        }

        curCamera = Camera.main;
        additionalCameraData = curCamera.GetUniversalAdditionalCameraData();
        additionalCameraData.SetRenderer(renderIndex);
        curRenderIndex = renderIndex;
        SetFuncState(curRenderIndex > 0);
    }

    void OnEnable()
    {
        m_MoveAction.action.performed += OnMovePerformed;
        m_TurnAction.action.performed += OnTurnPerformed;
        
        funcBtn?.onClick.AddListener(OnfuncBtnClick);
    }

    void OnDisable()
    {
        funcBtn?.onClick.RemoveListener(OnfuncBtnClick);
        m_MoveAction.action.performed -= OnMovePerformed;
        m_TurnAction.action.performed -= OnTurnPerformed;
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        var playerInput = context.ReadValue<Vector2>();
        playerInput = Vector2.ClampMagnitude(playerInput, 1.0f);
        Vector3 displacement = new Vector3(playerInput.x, 0.0f, playerInput.y) * maxMoveLength;
        
        moveTrans.localPosition += moveTrans.localRotation * displacement;
        //Debug.Log($"======================== OnMovePerformed displacement:{ displacement }");
    }

    private void OnTurnPerformed(InputAction.CallbackContext context)
    {
        var playerInput = context.ReadValue<Vector2>();
        float angle = playerInput.x > 0 ? 30f : -30f;
        moveTrans.Rotate(Vector3.up, angle);
        //Debug.Log($"======================== OnTurnPerformed angle:{ angle }");
    }
    
    private void OnfuncBtnClick()
    {
        var camera = Camera.main;
        var additional = camera.GetUniversalAdditionalCameraData();
        curRenderIndex = curRenderIndex == 0 ? 1 : 0;
        additional.SetRenderer(curRenderIndex);
        
        SetFuncState(curRenderIndex > 0);
        Debug.Log($"=========Camera stereoEnabled:{ camera.stereoEnabled }, fov: { camera.fieldOfView }, separation { camera.stereoSeparation }");
    }

    private void SetFuncState(bool active)
    {
        if (funcBtn != null)
        {
            var image = funcBtn.GetComponent<Image>();
            image.color = active ? Color.green : Color.white;
        }
    }
}
