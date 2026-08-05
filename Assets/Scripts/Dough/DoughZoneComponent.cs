using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoughZoneComponent : MonoBehaviour
{
    public GameObject doughPrefab;
    
    public bool isdoughSpawned = false;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!isdoughSpawned)
        {
            Instantiate(doughPrefab, transform.position, transform.rotation);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "DoughBigObject")
        {
            isdoughSpawned = false;
        }
    }
}
