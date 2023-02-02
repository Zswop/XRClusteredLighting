using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class TestClusteredLighting : MonoBehaviour
{
    [SerializeField]
    InputActionProperty m_MoveAction;
    
    [SerializeField]
    InputActionProperty m_TurnAction;
    
    [SerializeField, Range(0f, 0.5f)]
    float maxMoveLength = 0.1f;

    public Transform moveTrans;

    public Button funcBtn;
    
    Vector3 velocity;
    
    // Start is called before the first frame update
    void OnEnable()
    {
        m_MoveAction.action.performed += OnMovePerformed;
        m_TurnAction.action.performed += OnTurnPerformed;

        SetFuncState(ClusteredForwardLights.useEyePullBack);
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
        Debug.Log($"======================== OnMovePerformed displacement:{ displacement }");
    }

    private void OnTurnPerformed(InputAction.CallbackContext context)
    {
        var playerInput = context.ReadValue<Vector2>();
        float angle = playerInput.x > 0 ? 30f : -30f;
        moveTrans.Rotate(Vector3.up, angle);
        Debug.Log($"======================== OnTurnPerformed angle:{ angle }");
    }

    private void OnfuncBtnClick()
    {
        ClusteredForwardLights.useEyePullBack = !ClusteredForwardLights.useEyePullBack;
        Debug.Log($"====================================== EyePullBack: { ClusteredForwardLights.useEyePullBack }");
        
        SetFuncState(ClusteredForwardLights.useEyePullBack);
            
        var camera = Camera.main;
        Debug.Log($"=================== Camera sepation { camera.stereoSeparation }");
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
