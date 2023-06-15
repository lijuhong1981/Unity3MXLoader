using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity3MX;

public class UIComponent : MonoBehaviour
{
    public Unity3MXComponent component;
    public Button loadButton;
    public Button clearButton;
    public TMP_InputField urlInputField;
    public Toggle memoryCacheToggle;

    void Start()
    {
        if (loadButton != null)
        {
            var count = loadButton.onClick.GetPersistentEventCount();
            if (count == 0)
                loadButton.onClick.AddListener(StartLoad);
            else
            {                
                for (int i = 0; i < count; i++)
                {
                    var target = loadButton.onClick.GetPersistentTarget(i);
                    if (target == this)
                        return;
                }
                loadButton.onClick.AddListener(StartLoad);
            }
        }
        if (clearButton != null)
        {
            var count = clearButton.onClick.GetPersistentEventCount();
            if (count == 0)
                clearButton.onClick.AddListener(Clear);
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var target = clearButton.onClick.GetPersistentTarget(i);
                    if (target == this)
                        return;
                }
                clearButton.onClick.AddListener(Clear);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void StartLoad() {
        if (component != null && urlInputField != null) {
            component.url = urlInputField.text;
            if (memoryCacheToggle != null)
                component.enableMemeoryCache = memoryCacheToggle.isOn;
            component.Run();
        }
    }

    public void Clear()
    {
        if (component != null)
            component.Clear();
    }
}
