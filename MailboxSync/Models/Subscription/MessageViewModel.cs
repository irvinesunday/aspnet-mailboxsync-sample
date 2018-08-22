﻿using MailBoxSync.Models.Subscription;
using Microsoft.Graph;

namespace MailboxSync.Models.Subscription
{
    // The data that displays in the Notification view.
    public class MessageViewModel
    {
        public Message Message { get; set; }

        // The ID of the user associated with the subscription.
        // Used to filter messages to display in the client.
        public string SubscribedUser { get; set; }

        public MessageViewModel(Message message, string subscribedUserId)
        {
            Message = message;
            SubscribedUser = subscribedUserId;
        }

    }
}