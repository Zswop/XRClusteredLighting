using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ClusteredLightingTest2 : MonoBehaviour
{
    [SerializeField]
    InputActionProperty m_MoveAction;
    
    [SerializeField]
    InputActionProperty m_TurnAction;
    
    [SerializeField, Range(0f, 0.2f)]
    float maxMoveLength = 0.05f;

    public Transform moveTrans;

    public Button funcBtn;
    
    [Header("Camera")][Range(0, 1)]
    public int renderIndex = 0;

    private Camera curCamera;
    private UniversalAdditionalCameraData additionalCameraData;
    private int curRenderIndex;

    private float _curTime = 0;
    private List<Light> lights = new List<Light>();
    
    void Start()
    {
        //if (procedural)
        {
            for (int i = 0; i < 8; i++)
            {
                int row = i / 4;
                int colume = i % 4;
                int lightRange = 2;
                
                var go = new GameObject();
                go.transform.localPosition = new Vector3(2 * (colume - 1.5f) * lightRange, 1f, 2 * (row - 0.5f) * lightRange);
                go.transform.SetParent(transform, false);

                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = lightRange;
                l.intensity = 10;
                
                var c = UnityEngine.Random.insideUnitSphere;
                l.color = new Color(c.x, c.y, c.z, 1.0f);
                
                lights.Add(l);
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
