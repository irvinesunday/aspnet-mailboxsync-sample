﻿/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using MailboxSync.Models;
using Microsoft.Graph;

namespace MailboxSync.Services
{
    /// <summary>
    /// Interfaces with the graph client to make requests
    /// </summary>
    public class MailService
    {
        /// <summary>
        /// This results in a call to get the signed in user's mailfolders
        /// Request: GET graph.microsoft.com/v1.0/me/mailfolders
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/user_list_mailfolders
        /// </summary>
        /// <param name="graphClient">An instance of the authenticated graph client</param>
        /// <returns></returns>
        public async Task<List<FolderItem>> GetMyMailFolders(GraphServiceClient graphClient)
        {
            List<FolderItem> items = new List<FolderItem>();
            var folders = await graphClient.Me.MailFolders.Request().GetAsync();
            if (folders?.Count > 0)
            {
                foreach (var folder in folders)
                {
                    var folderMessages = await GetMyFolderMessages(graphClient, folder.Id, null);
                    // checks if it is the mailbox folder so that when displayed,
                    // it can show up first in the list
                    var isStartUpFolder = (folder.DisplayName == "Inbox");
                    items.Add(new FolderItem
                    {
                        Name = folder.DisplayName,
                        Id = folder.Id,
                        MessageItems = folderMessages.Messages,
                        ParentId = null,
                        SkipToken = folderMessages.SkipToken,
                        StartupFolder = isStartUpFolder
                    });
                    var clientFolders = await GetChildFolders(graphClient, folder.Id, isStartUpFolder);
                    items.AddRange(clientFolders);
                }
            }

            // order folder results, showing the startup folders first
            items = OrderFolderResults(items);
            return items;
        }


        /// <summary>
        /// Makes a call to the graph to receive the list of child folders for a particular folder
        /// Request: GET GET graph.microsoft.com/v1.0/me/mailfolders/{id}/childFolders
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/mailfolder_list_childfolders
        /// </summary>
        /// <param name="graphClient">An instance of the authenticated graph client</param>
        /// <param name="id">Id of the parent folder </param>
        /// <returns></returns>
        private async Task<List<FolderItem>> GetChildFolders(GraphServiceClient graphClient, string id, bool isStartUpFolder)
        {
            List<FolderItem> children = new List<FolderItem>();

            var childFolders = await graphClient.Me.MailFolders[id].ChildFolders.Request().GetAsync();

            if (childFolders?.Count > 0)
            {
                foreach (var child in childFolders)
                {
                    var folderMessages = await GetMyFolderMessages(graphClient, child.Id, null);
                    children.Add(new FolderItem
                    {
                        Name = "-- " + child.DisplayName,
                        Id = child.Id,
                        MessageItems = folderMessages.Messages,
                        ParentId = child.ParentFolderId,
                        SkipToken = folderMessages.SkipToken,
                        StartupFolder = isStartUpFolder
                    });
                }
            }
            return children;
        }


        /// <summary>
        /// Makes a call to the graph to receive the list of messages in a particular folder
        /// Request: GET GET graph.microsoft.com/v1.0/me/mailfolders/{id}/messages
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/mailfolder_list_messages
        /// </summary>
        /// <param name="graphClient">An instance of the authenticated graph client</param>
        /// <param name="folderId">The folder whose messages we want to get</param>
        /// <param name="skip">the skip token for when we are going through a list via pagination</param>
        /// <returns></returns>
        public async Task<FolderMessages> GetMyFolderMessages(GraphServiceClient graphClient, string folderId, int? skip)
        {
            var top = Convert.ToInt32(ConfigurationManager.AppSettings["ida:PageSize"]);
            var folderMessages = new FolderMessages { SkipToken = null };

            // Initialise the request
            var request = graphClient.Me.MailFolders[folderId].Messages.Request();

            // if the pagination skip token has a value, add it to the request
            if (skip.HasValue)
            {
                request = request.Skip(skip.Value);
            }
            var messages = await request.Top(top).GetAsync();

            // if there are  other pages in the response, store the skip token
            if (messages.NextPageRequest != null)
            {
                foreach (var x in messages.NextPageRequest.QueryOptions)
                {
                    if (x.Name == "$skip")
                        folderMessages.SkipToken = Convert.ToInt32(x.Value);
                }
            }

            if (messages.Count > 0)
            {
                foreach (Message message in messages)
                {
                    folderMessages.Messages.Add(new MessageItem
                    {
                        ConversationId = message.ConversationId,
                        Id = message.Id,
                        Subject = message.Subject,
                        BodyPreview = message.BodyPreview,
                        IsRead = (bool)message.IsRead,
                        CreatedDateTime = (DateTimeOffset)message.CreatedDateTime
                    });
                }
            }
            return folderMessages;
        }


        /// <summary>
        /// Sends an email message to the current user on behalf of the current user.
        /// Request: POST graph.microsoft.com/v1.0/me/messages/{id}/send
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/message_send
        /// </summary>
        /// <param name="graphClient">An instance of the authenticated graph client</param>
        /// <returns></returns>
        public async Task SendMessage(GraphServiceClient graphClient)
        {
            var me = await graphClient.Me.Request().Select("Mail, UserPrincipalName").GetAsync();
            string address = me.Mail ?? me.UserPrincipalName;
            string guid = Guid.NewGuid().ToString();

            // Create the recipient list and uses the current user as the recipient.
            List<Recipient> recipients = new List<Recipient>();
            recipients.Add(new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = address
                }
            });

            // Create the message.
            Message email = new Message
            {
                Body = new ItemBody
                {
                    Content = "Contents of the test message created by Microsoft Graph. Lorem " + guid,
                    ContentType = BodyType.Text,
                },
                Subject = "Test message created by Microsoft Graph" + guid.Substring(0, 8).ToUpper(),
                ToRecipients = recipients
            };

            // Send the message.
            await graphClient.Me.SendMail(email, true).Request().PostAsync();
        }

        /// <summary>
        /// Get a specified message using its Id
        /// Request: GET graph.microsoft.com/v1.0/me/messages/{id}
        /// https://developer.microsoft.com/en-us/graph/docs/api-reference/v1.0/api/eventmessage_get
        /// </summary>
        /// <param name="graphClient">An instance of the authenticated graph client</param>
        /// <param name="messageId">the id of the specific message</param>
        /// <returns></returns>
        public async Task<Message> GetMessage(GraphServiceClient graphClient, string messageId)
        {
            // Get the message.
            Message message = await graphClient.Me.Messages[messageId].Request().GetAsync();
            return message;
        }

        /// <summary>
        /// Reorders the folders so that those with the startupfolder flag are displayed first
        /// </summary>
        /// <param name="folderResults">folders resulting from the fetch folders query</param>
        /// <returns></returns>
        private List<FolderItem> OrderFolderResults(List<FolderItem> folderResults)
        {
            var startupFolderFolderItems = new List<FolderItem>();
            var nonStartupFolderFolderItems = new List<FolderItem>();
            var listOfFolders = new List<FolderItem>();
            foreach (var item in folderResults)
            {
                if (item.StartupFolder)
                {
                    startupFolderFolderItems.Add(item);
                }
                else
                {
                    nonStartupFolderFolderItems.Add(item);
                }
            }
            listOfFolders.AddRange(startupFolderFolderItems);
            listOfFolders.AddRange(nonStartupFolderFolderItems);
            return listOfFolders;
        }


    }
}