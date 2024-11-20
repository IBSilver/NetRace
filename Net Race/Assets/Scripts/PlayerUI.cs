using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerUI : MonoBehaviour
{
    public Text nameText;
    private string lastName;

    void Start()
    {
        StartCoroutine(CheckForNameChange());
    }

    IEnumerator CheckForNameChange()
    {
        while (true)
        {
            string rootObjectName = transform.root.name;

            if (nameText != null && rootObjectName != lastName)
            {
                nameText.text = rootObjectName;
                lastName = rootObjectName;
            }

            yield return new WaitForSeconds(1f);
        }
    }
}
