using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class EmailRowUI
{
    public Button senderButton;
    public TMP_Text senderText;

    public Button subjectButton;
    public TMP_Text subjectText;
}

public class MailAppController : MonoBehaviour
{
    [SerializeField] private TMP_Text emailPreviewText;
    [SerializeField] private Button emailLinkButton;
    [SerializeField] private TMP_Text emailLinkText;

    //controls the popups
    [SerializeField] private PopupSequenceController popupSequenceController;
    
    //list containing all emails in the inbox
    [SerializeField] private List<EmailData> emails = new List<EmailData>();

    //holds the corresponding buttons to the eamails
    [SerializeField] private List<EmailRowUI> emailRows = new List<EmailRowUI>(); 

    //tracks which email we're at starting at -1 meaning no email is slected
    private int selectedEmailIndex = -1;

    private void Start()
    {
        if(emailPreviewText != null)
        {
            emailPreviewText.text = "From:\n\nSubject:\n\n\nThere is no message selected.";
        }

        if(emailLinkButton != null)
        {
            emailLinkButton.gameObject.SetActive(false);
            emailLinkButton.onClick.AddListener(OnEmailLinkClicked);
        }

        for(int i = 0; i < emailRows.Count; i++)
        {
            EmailRowUI row = emailRows[i];

            //only shows rows that have an email assigned
            bool hasEmail = i < emails.Count;

            if(row.senderButton != null)
            {
                row.senderButton.gameObject.SetActive(hasEmail);
            }

            if(row.subjectButton != null)
            {
                row.subjectButton.gameObject.SetActive(hasEmail);
            }

            if (!hasEmail)
            {
                continue;
            }

            EmailData email = emails[i];

            if(row.senderText != null)
            {
                row.senderText.text = email.sender;
            }

            if(row.subjectText != null)
            {
                row.subjectText.text = email.subject;
            }


            int emailIndex = i;

            if(row.senderButton != null)
            {
                row.senderButton.onClick.AddListener(() => OpenEmail(emailIndex));
            }

            if(row.subjectButton != null)
            {
                row.subjectButton.onClick.AddListener(() => OpenEmail(emailIndex));
            }
        }
    }

    //function that displays the selected email
    public void OpenEmail(int emailIndex)
    {
        if(emailIndex < 0 || emailIndex >= emails.Count)
        {
            return;
        }

        selectedEmailIndex = emailIndex;

        EmailData email = emails[emailIndex];

        email.isOpened = true;

        if(emailPreviewText != null)
        {
            emailPreviewText.text = "From: " + email.sender + "\n\n" + "Subject: " + email.subject + "\n\n\n" + email.body;
        }

        bool hasLink = !string.IsNullOrWhiteSpace(email.linkURL);

        if(emailLinkButton != null)
        {
            emailLinkButton.gameObject.SetActive(hasLink);
        }

        if(hasLink && emailLinkText != null)
        {
            emailLinkText.text = email.linkURL;
        }
    }

    //called when player clicks link for now just displays a log warning that if successfully clicked
    public void OnEmailLinkClicked()
    {
        if(selectedEmailIndex < 0 || selectedEmailIndex >= emails.Count)
        {
            return;
        }

        EmailData email = emails[selectedEmailIndex];

        if (string.IsNullOrWhiteSpace(email.linkURL))
        {
            return;
        }

        if (email.isSuspicious)
        {
            Debug.LogWarning("clicked bad link: " + email.linkURL);

            if(popupSequenceController != null)
            {
                popupSequenceController.StartPopupSequence();
            }
            else
            {
                Debug.LogError("popup sequence controller not assigned");
            }
        }
        else
        {
            Debug.Log("ok link clicked: " + email.linkURL);
        }
    }



}