﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using MailboxSync.Models;
using MailBoxSync.Models.Subscription;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MailboxSync.Services
{
    public class DataService
    {
        public List<FolderItem> GetFolders()
        {
            string jsonFile = HostingEnvironment.MapPath("~/mail.json");
            List<FolderItem> folderItems = new List<FolderItem>();
            if (!File.Exists(jsonFile))
            {
                return folderItems;
            }

            var mailData = File.ReadAllText(jsonFile);
            if (mailData == null)
            {
                return folderItems;
            }

            try
            {
                var jObject = JObject.Parse(mailData);
                JArray folders = (JArray)jObject["folders"];
                if (folders != null)
                {
                    foreach (var item in folders)
                    {
                        var name = item["Name"].ToString();
                        folderItems.Add(new FolderItem
                        {
                            Name = item["Name"].ToString(),
                            Id = item["Id"].ToString(),
                            Messages = GenerateMessages(item["Messages"].ToString()),
                            SkipToken = (int?) item["SkipToken"]
                        });
                    }
                    return folderItems;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Add Error : " + ex.Message);
            }
            return folderItems;
        }

        private List<MessageItem> GenerateMessages(string messageString)
        {
            var messageItem = new List<MessageItem>();
            try
            {
                var messageArray = JArray.Parse(messageString);
                foreach (var item in messageArray)
                {
                    var mItem = JObject.Parse(item.ToString());
                    messageItem.Add(new MessageItem
                    {
                        Id = mItem["id"].ToString(),
                        Subject = mItem["subject"].ToString(),
                        IsRead = (bool)mItem["isRead"],
                        BodyPreview = mItem["bodyPreview"].ToString(),
                        CreatedDateTime = (DateTimeOffset)mItem["createdDateTime"]
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Add Error : " + ex.Message);
            }
            return messageItem.OrderByDescending(k=>k.CreatedDateTime).ToList();
        }

        public bool FolderExists(string folderId)
        {
            bool exists = false;
            string jsonFile = HostingEnvironment.MapPath("~/mail.json");
            try
            {
                var mailBox = JObject.Parse(File.ReadAllText(jsonFile));
                var folders = mailBox.GetValue("folders") as JArray;
                if (folders != null)
                {
                    if (!string.IsNullOrEmpty(folderId))
                    {
                        var folder = folders.Where(obj => obj["Id"].Value<string>() == folderId);
                        if (folder.ToList().Count > 0)
                        {
                            exists = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return exists;
        }

        public void StoreFolder(FolderItem folder)
        {
            string jsonFile = HostingEnvironment.MapPath("~/mail.json");
            try
            {
                var mailBox = File.ReadAllText(jsonFile);
                var mailBoxObject = JObject.Parse(mailBox);
                var folderArrary = mailBoxObject.GetValue("folders") as JArray;

                if (folderArrary == null)
                    folderArrary = new JArray();

                if (folderArrary.All(obj => obj["Id"].Value<string>() != folder.Id))
                {
                    folderArrary.Add(JObject.Parse(JsonConvert.SerializeObject(folder)));
                }

                mailBoxObject["folders"] = folderArrary;
                string newFolderContents = JsonConvert.SerializeObject(mailBoxObject, Formatting.Indented);
                File.WriteAllText(jsonFile, newFolderContents);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Add Error : " + ex.Message);
            }
        }

        public void StoreMessage(List<MessageItem> messages, string folderId, int? messagesSkipToken)
        {
            string jsonFile = HostingEnvironment.MapPath("~/mail.json");
            try
            {
                var mailBox = JObject.Parse(File.ReadAllText(jsonFile));
                var folders = mailBox.GetValue("folders") as JArray;
                if (folders != null)
                {
                    if (!string.IsNullOrEmpty(folderId))
                    {
                        var folder = folders.Where(obj => obj["Id"].Value<string>() == folderId);
                        foreach (var item in folder)
                        {
                            var newFolderItem = new FolderItem
                            {
                                Name = item["Name"].ToString(),
                                Id = item["Id"].ToString(),
                                Messages = GenerateMessages(item["Messages"].ToString())
                            };
                            newFolderItem.Messages.AddRange(messages);
                            newFolderItem.SkipToken = messagesSkipToken;
                            newFolderItem.Messages = newFolderItem.Messages.GroupBy(p => new { p.Id }).Select(g => g.First()).ToList();
                            UpdateFolder(newFolderItem);
                        }
                    }
                    else
                    {
                        Console.Write(" Try Again!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Add Error : " + ex.Message);
            }
        }

        private void UpdateFolder(FolderItem folder)
        {
            string jsonFile = HostingEnvironment.MapPath("~/mail.json");
            try
            {
                var json = File.ReadAllText(jsonFile);
                var folderObject = JObject.Parse(json);
                var folderArrary = folderObject.GetValue("folders") as JArray;
                if (folderArrary != null)
                {
                    var mailData = JObject.Parse(json);
                    JArray messageObject = JArray.Parse(JsonConvert.SerializeObject(folder.Messages));

                    if (!string.IsNullOrEmpty(folder.Id))
                    {
                        foreach (var mailFolder in folderArrary.Where(obj => obj["Id"].Value<string>() == folder.Id))
                        {
                            mailFolder["Messages"] = messageObject;
                            mailFolder["SkipToken"] = folder.SkipToken;
                        }

                        mailData["folders"] = folderArrary;
                        string output = JsonConvert.SerializeObject(mailData, Formatting.Indented);
                        File.WriteAllText(jsonFile, output);
                    }
                    else
                    {
                        Console.Write(" Try Again!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Add Error : " + ex.Message);
            }
        }
    }
}