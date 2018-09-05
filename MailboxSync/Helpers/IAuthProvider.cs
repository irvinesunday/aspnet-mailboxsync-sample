﻿/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System.Threading.Tasks;

namespace MailboxSync.Helpers
{
    public interface IAuthProvider
    {
        Task<string> GetUserAccessTokenAsync(string userId);
    }
}
