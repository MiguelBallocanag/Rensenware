using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.U2D.IK;

public class StartupController : MonoBehaviour
{
    [Header("Startup Screens")]
    [SerializeField] private GameObject biosScreen;
    [SerializeField] private GameObject splashScreen;
    [SerializeField] private GameObject loginScreen;

    [Header("BIOS")]
    [SerializeField] private TMP_Text biosText;
    [SerializeField] private TMP_Text biosFooter;

    [Header("Timing")]
    [SerializeField] private float splashDuration = 3f;

    private Coroutine biosCoroutine;
    private bool isBIOSRunning;

    private void Start()
    {
        biosScreen.SetActive(true);
        splashScreen.SetActive(false);
        loginScreen.SetActive(false);

        isBIOSRunning = true;
        biosCoroutine = StartCoroutine(BIOSSequence());
    }

    private void Update()
    {
        if(isBIOSRunning && Input.GetKeyDown(KeyCode.F1))
        {
            StopCoroutine(biosCoroutine);
            StartCoroutine(SplashSequence());
        }
    }

    private IEnumerator BIOSSequence()
    {
        //clear default bios text
        biosText.text = "";
        biosFooter.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(.04f);

        //starting info
        biosText.text = "Award Modular BIOS v4.51PG\n" + "Copyright (C) 1984-95, Award Software Inc.\n\n" + "PENTIUM-S CPU at 100MHz\n\n";
        yield return new WaitForSecondsRealtime(.3f);

        string header = biosText.text;

        //memory test
        for(int memory = 0; memory <= 16384; memory += 256)
        {
            biosText.text = header + $"Memory Test : {memory:D6}K";
            yield return new WaitForSecondsRealtime(.025f);
        }
        biosText.text += " OK\n\n";

        biosFooter.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.3f);

        biosText.text += "Award Plug and Play BIOS Extension v1.0A\n\n";
        biosText.text += "Detecting IDE Primary Master  ... ";
        
        yield return new WaitForSecondsRealtime(.5f);
        
        biosText.text += "WDC AC31000\n";
        biosText.text += "Detecting IDE Primary Slave  ... ";
        
        yield return new WaitForSecondsRealtime(.3f);

        biosText.text += "None\n";

        biosText.text += "Detecting IDE Secondary Master ... ";
        yield return new WaitForSecondsRealtime(.4f);
        biosText.text  += "CD-ROM\n";

        biosText.text += "Detecting IDE Secondary Slave   ... ";

        yield return new WaitForSecondsRealtime(.3f);
        biosText.text += "None\n\n";

        biosText.text += "Verifying DMI Pool Data ........";

        yield return new WaitForSecondsRealtime(.5f);

        biosText.text += " Success\n";

        string completedText = biosText.text;

        for(int i = 0; i < 3; i++)
        {
            biosText.text = completedText + "_";
            yield return new WaitForSecondsRealtime(.2f);
            biosText.text = completedText;
            yield return new WaitForSecondsRealtime(.5f);
        }
        yield return SplashSequence();

    }
    private IEnumerator SplashSequence()
    {
        isBIOSRunning = false;
        biosScreen.SetActive(false);

        yield return new WaitForSecondsRealtime(.5f);
        splashScreen.SetActive(true);

        yield return new WaitForSecondsRealtime(splashDuration);

        splashScreen.SetActive(false);
        loginScreen.SetActive(true);

    }
}
