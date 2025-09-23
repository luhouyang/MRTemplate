using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ButtonUI : MonoBehaviour
{
    [SerializeField] private bool hasState = false;
    [SerializeField] private bool isPressed = false;
    [SerializeField] private GameObject neutralButton;
    [SerializeField] private GameObject onButton;
    [SerializeField] private GameObject offButton;

    // Start is called before the first frame update
    void Start()
    {
        if (hasState)
        {
            neutralButton.SetActive(false);
            if (isPressed)
            {
                onButton.SetActive(true);
                offButton.SetActive(false);
            } else
            {
                onButton.SetActive(false);
                offButton.SetActive(true);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setHasState(bool val)
    {
        hasState = val;
    }

    public void toggleIsPressed()
    {
        isPressed = !isPressed;
    }
}
