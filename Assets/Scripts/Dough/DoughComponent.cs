using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DoughComponent : MonoBehaviour
{
    public Vector3 targetScale;
    
    public XRGrabInteractable grabInteractable;

    private void Start()
    {
        grabInteractable ??= GetComponentInParent<XRGrabInteractable>();
        
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs arg0)
    {
        transform.localScale = targetScale;
    }
}
