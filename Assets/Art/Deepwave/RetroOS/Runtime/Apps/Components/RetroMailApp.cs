using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    [AddComponentMenu("")]
    public class RetroMailApp : RetroAppBehaviour, IMailSource
    {
        [Header("Messages")]
        [Tooltip("Inbox contents. Mark a message 'delayed' to hold it back until " +
                 "a DeliverMail rule sends it.")]
        [SerializeField] List<MailEntry> mail = new();

        public List<MailEntry> Messages => mail;

        public override void BuildContent(AppContext ctx) => AppView.Attach<MailView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "mail";
            app.title = "Inbox";
            app.builtinIcon = BuiltinIcon.Mail;
            app.windowSize = new Vector2(660, 460);
            app.minWindowSize = new Vector2(540, 360);
        }
    }
}
