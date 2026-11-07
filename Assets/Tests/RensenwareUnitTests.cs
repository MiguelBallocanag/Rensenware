using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestTools;

//from lecture 7 - testing
//arrange - set up class instances in the desired state
//act - execute the code being tested
//assert - verify that the expected result happene

public class RensenwareUnitTests
{
    private GameObject testObject;
    private MailAppController mailController;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("TestObject");
        mailController = testObject.AddComponent<MailAppController>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObject);
    }
    
    //create private serialize fields
    private void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Could not find field: " + fieldName);
        field.SetValue(target, value);
    }

    //MailController.cs test 1
    //tests if openining an email updates its state
    [Test]
    public void OpenEmail_ValidIndex_MarksEmailOpened()
    {
        //arrange - create an unopened email and put it in the MailAppController's email list
        EmailData email = new EmailData{isOpened = false};
        SetPrivateField(mailController, "emails", new List<EmailData>{email});
        //act - call open email in the MailControllerApp
        mailController.OpenEmail(0);
        //assert - Verify that it is actually opened
        Assert.IsTrue(email.isOpened);
    }

    //MailAppController.cs test 2
    //tests if an invalid email index is ignored
    [Test]
    public void OpenEmail_OutOfBoundsIndex_LeavesEmailUnoppened()
    {
        //arrange
        EmailData email = new EmailData {isOpened = false};
        SetPrivateField(mailController, "emails", new List<EmailData> {email});
        //act - call OpenEmail(1), even though index 1 doesn't exist
        mailController.OpenEmail(1);
        //assert - checks if isOpened is still false
        Assert.IsFalse(email.isOpened);
    }
    //MailController.cs test 1 and test 2 for branch coverage
    //first unit test does the valid input branch and second tests the invalid input branch

    //MailAppController.cs test 3 - regression test
    //create an email but the selected inedx is invalid (should be at 0 if one email)
    [Test]
    public void OnEmailLinkClicked_OutOfBoundsIndex_DoesNotThrow()
    {
        //arrange 
        EmailData email = new EmailData {linkURL = "123.com"};
        SetPrivateField(mailController, "emails", new List <EmailData> {email});
        SetPrivateField(mailController,"selectedEmailIndex", 1);

        //act + assert
        //executes OnEmaiLinkClicked()
        Assert.DoesNotThrow(() => mailController.OnEmailLinkClicked());
    } 

    //MailAppController.cs test 4
    //when clicking a sus link it should log a warning 
    //NOTE: does not test if the popups actually happen
    [Test]
    public void OnEmaiLinkClicked_SuspiciousLink_LogsWarning()
    {
        //arrange
        EmailData email= new EmailData{linkURL = "1234.com", isSuspicious = true};
        SetPrivateField(mailController, "emails", new List<EmailData> {email});
        mailController.OpenEmail(0);

        //expect both messages because in this fake scenario we never assign the popup controller
        //this is fine, we're just testing that it gives us a warning for visual confirmation
        LogAssert.Expect(LogType.Warning, "clicked bad link: 1234.com");
        LogAssert.Expect(LogType.Error, "popup sequence controller not assigned");

        //act
        mailController.OnEmailLinkClicked();
    } 

    //MailAppController.cs test 5
    //tests if clicking on a non sus link outputs the log confirming it still works if isSuspicious is false
    [Test]
    public void OnEmailLinkClicked_NormalLink_LogsMessage()
    {
        //arrange
        EmailData email = new EmailData{linkURL = "hi.com", isSuspicious = false};
        SetPrivateField(mailController, "emails", new List<EmailData> {email});
        mailController.OpenEmail(0);

        LogAssert.Expect(LogType.Log, "ok link clicked: hi.com");

        //act
        mailController.OnEmailLinkClicked();
    }

    //DesktopManager.cs test1
    //tests if the player presses on the mail app it actually opens it
    [Test]
    public void OpenMail_WindowInactive_ActivatesWindow()
    {
        //arrange - makes a desktop manager component
        DesktopManager desktopManager = testObject.AddComponent<DesktopManager>();

        //creates a temp mail window
        GameObject mailWindow = new GameObject("MailWindow");
        //makes mailwindow the child of testObject so it also destroys it
        mailWindow.transform.SetParent(testObject.transform);
        mailWindow.SetActive(false);

        //desktopManager - what we're modifying, "mailWindow" - the private field we're assigning, mailWindow - temp gameobj we're assigning to it
        SetPrivateField(desktopManager, "mailWindow", mailWindow);

        //act
        desktopManager.OpenMail();

        //assert
        Assert.IsTrue(mailWindow.activeSelf);
    }

    //DesktopManager.cs test 2
    //tests if x-ing out the mailwindow works and opening it too
    [Test]
    public void CloseMail_WindowActive_DeactivatesWindow()
    {
        //arrange - makes a desktop manager component
        DesktopManager desktopManager = testObject.AddComponent<DesktopManager>();
        
        //creates a temp mail window
        GameObject mailWindow = new GameObject("MailWindow");
        //makes mailwindow the child of testObject so it also destroys it
        mailWindow.transform.SetParent(testObject.transform);
        mailWindow.SetActive(true);

        //desktopManager - what we're modifying, "mailWindow" - the private field we're assigning, mailWindow - temp gameobj we're assigning to it
        SetPrivateField(desktopManager, "mailWindow", mailWindow);

        //act 
        desktopManager.CloseMail();

        //assert
        Assert.IsFalse(mailWindow.activeSelf);
    }

    //WindowDragHandler.cs 
    //what happens if a window without this component assigned is dragged?
    [Test]
    public void OnDrag_WindowNotAssigned_DoesNotThrow()
    {
        //arrange no recttransform is assigned to the handler
        WindowDragHandler dragHandler = testObject.AddComponent<WindowDragHandler>();

        //act + assert - assign it to null
        //should return 
        Assert.DoesNotThrow(() => dragHandler.OnDrag(null));
    }

    //NEED UNIT TEST FOR POPUPHANDLER
}
