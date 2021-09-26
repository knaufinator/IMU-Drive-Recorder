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
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace SDKTemplate
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "Video Playback";

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title= "Playback", ClassType=typeof(Playback)},
            new Scenario() { Title= "Record", ClassType=typeof(Recording)},
        };

        public Windows.Media.Playback.MediaPlayer commonMediaPlayer = null;
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }

    /// <summary>
    /// Allows for disposal of the underlying MediaSources attached to a MediaPlayer, regardless
    /// of if a MediaSource or MediaPlaybackItem was passed to the MediaPlayer.
    ///
    /// It is left to the app to implement a clean-up of the other possible IMediaPlaybackSource
    /// type, which is a MediaPlaybackList.
    ///
    /// </summary>
    public static class MediaPlayerHelper
    {
        public static void CleanUpMediaPlayerSource(Windows.Media.Playback.MediaPlayer mp)
        {
            if (mp?.Source != null)
            {
                var source = mp.Source as Windows.Media.Core.MediaSource;
                source?.Dispose();

                var item = mp.Source as Windows.Media.Playback.MediaPlaybackItem;
                item?.Source?.Dispose();

                mp.Source = null;
            }
        }
    }
}
