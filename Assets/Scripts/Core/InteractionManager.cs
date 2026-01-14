using FPS;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
public class InteractionManager : MonoBehaviour
{
    [SerializeField] private Weapon hoverWeapon;

    // Update is called once per frame
    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            GameObject objectHit = hit.transform.gameObject;

            if (objectHit.GetComponent<Weapon>())
            {
                hoverWeapon = objectHit.GetComponent<Weapon>();
                hoverWeapon.GetComponent<Outline>().enabled = true;
            }
            else
            {
                if (hoverWeapon)
                {
                    hoverWeapon.GetComponent<Outline>().enabled = false;
                }
            }
        }
    }
}
*/