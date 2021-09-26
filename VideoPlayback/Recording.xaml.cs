//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Windows.Media.MediaProperties;
using mPUObserver;
using mPUObserver.Helpers;
using System.Collections.Concurrent;
using Windows.Media.Capture.Frames;

namespace SDKTemplate
{
    /// <summary>
    /// Demonstrates closed captions delivered in-band, specifically SRT in an MKV.
    /// </summary>
    public sealed partial class Recording : Page
    {

        public Recording()
        {
            this.InitializeComponent();
           
        }
    }
}
