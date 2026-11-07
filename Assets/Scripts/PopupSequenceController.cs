using System.Collections;
using UnityEngine;

public class PopupSequenceController : MonoBehaviour
{
    [SerializeField] private GameObject[] warningPopups;

    [SerializeField] private float[] popUpDelays = {.5f,.2f,.2f};

    private bool sequenceStarted = false;

    private void Awake()
    {
        foreach(GameObject popup in warningPopups)
        {
            if(popup != null)
            {
                popup.SetActive(false);
            }
        }
    }

    public void StartPopupSequence()
    {
        if(sequenceStarted)
        {
            return;
        }

        StartCoroutine(ShowPopups());
    }

    private IEnumerator ShowPopups()
    {
        for(int i = 0; i < warningPopups.Length; i++)
        {
            GameObject popup = warningPopups[i];

            if(popup != null)
            {
                popup.SetActive(true);
                popup.transform.SetAsLastSibling();
            }

            if(i < warningPopups.Length - 1)
            {
                float delay = i < popUpDelays.Length ? popUpDelays[i] : .2f;
                yield return new WaitForSecondsRealtime(Mathf.Max(0f,delay));
            }
        }
    }
}
