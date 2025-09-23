using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Maps one keyboard / keypad input to one function
/// 
/// 1つのキーボード／キーパッド入力を1つの機能にマップします。
/// </summary>
public class KeyboardInput : MonoBehaviour
{
    [Tooltip("The key that will trigger the action.")]
    [SerializeField]
    private KeyCode triggerKey = KeyCode.Space;

    [Tooltip("The function(s) to be called when the trigger key is pressed.")]
    [SerializeField]
    private UnityEvent onKeyPressEvent;

    void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            onKeyPressEvent?.Invoke();
        }
    }
    public void SetTriggerKey(KeyCode newKey)
    {
        triggerKey = newKey;
        Debug.Log($"Trigger key for KeyboardInput changed to: {newKey}");
    }

    public void AddListener(UnityAction action)
    {
        onKeyPressEvent.AddListener(action);
    }

    public void RemoveListener(UnityAction action)
    {
        onKeyPressEvent.RemoveListener(action);
    }
}
