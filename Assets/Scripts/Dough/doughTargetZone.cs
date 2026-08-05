using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class doughTargetZone : MonoBehaviour
{
    
    public MeshRenderer meshRenderer;
    
    public GameObject doughPrefab;
    
    public doughInteractionComponent currentDough;

    private void Start()
    {
        meshRenderer ??= GetComponent<MeshRenderer>();
    }

    private void Update()
    {
        meshRenderer.enabled = (currentDough == null);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "DoughObject")
        {
            var doughXRGrabInteractable = other.gameObject.GetComponent<XRGrabInteractable>();

            if (currentDough == null)
            {
                currentDough = Instantiate(doughPrefab, transform.position, transform.rotation).GetComponent<doughInteractionComponent>();
                currentDough.currentTargetZone = this;
                Destroy(other.gameObject);
            }
        }
    }
}
