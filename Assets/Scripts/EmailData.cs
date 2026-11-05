using System;
using UnityEngine;

[Serializable]
public class EmailData
{
    public string emailID;

    public string sender;
    public string subject;

    [TextArea(3,10)]
    public string body;

    public bool isSuspicious;

    public string linkURL;

    public bool isOpened;

}
