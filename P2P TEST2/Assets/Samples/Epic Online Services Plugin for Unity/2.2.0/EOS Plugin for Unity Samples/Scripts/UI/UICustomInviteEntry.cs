﻿/*
* Copyright (c) 2021 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Epic.OnlineServices;

namespace PlayEveryWare.EpicOnlineServices.Samples
{
    public class UICustomInviteEntry : MonoBehaviour
    {
        public Text SenderText;
        public Text PayloadText;

        private Action<Utf8String> OnAccept;
        private Action<Utf8String> OnReject;

        [NonSerialized]
        public Utf8String InviteId;

        public void SetInviteData(CustomInviteData InviteData, string SenderName)
        {
            InviteId = InviteData.InviteId;
            SenderText.text = SenderName;
            PayloadText.text = InviteData.Payload;
        }

        public void SetCallbacks(Action<Utf8String> AcceptCallback, Action<Utf8String> RejectCallback)
        {
            OnAccept = AcceptCallback;
            OnReject = RejectCallback;
        }

        public void OnAcceptButtonClicked()
        {
            OnAccept?.Invoke(InviteId);
        }

        public void OnRejectButtonClicked()
        {
            OnReject?.Invoke(InviteId);
        }
    }
}