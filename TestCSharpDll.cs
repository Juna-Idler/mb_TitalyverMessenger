﻿using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using System.Diagnostics;

//くっ！バージョンが足りない！（Frameworkだと使えない）
//using System.Text.Json;

using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;



namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private static Dictionary<MetaDataType, string> MetaNameDic = new Dictionary<MetaDataType, string>()
        {
            { MetaDataType.TrackTitle, "TrackTitle" },
            { MetaDataType.Album, "Album" },
            { MetaDataType.AlbumArtist, "AlbumArtist" },        // displayed album artist
            { MetaDataType.AlbumArtistRaw, "AlbumArtistRaw" },     // stored album artist
            { MetaDataType.Artist, "Artist" },             // displayed artist
            { MetaDataType.MultiArtist, "MultiArtist" },        // individual artists, separated by a null char
            { MetaDataType.PrimaryArtist, "PrimaryArtist" },      // first artist from multi-artist tagged file, otherwise displayed artist
            { MetaDataType.Artists, "Artists" },
            { MetaDataType.Artwork, "Artwork" },
            { MetaDataType.BeatsPerMin, "BeatsPerMin" },

            { MetaDataType.Composer, "Composer" },           // displayed composer
            { MetaDataType.MultiComposer, "MultiComposer" },      // individual composers, separated by a null char
            { MetaDataType.Comment, "Comment" },
            { MetaDataType.DiscNo, "DiscNo" },
            { MetaDataType.DiscCount, "DiscCount" },
            { MetaDataType.Encoder, "Encoder" },
            { MetaDataType.Genre, "Genre" },
            { MetaDataType.GenreCategory, "GenreCategory" },
            { MetaDataType.Grouping, "Grouping" },
            { MetaDataType.Keywords, "Keywords" },

            { MetaDataType.HasLyrics, "HasLyrics" },
            { MetaDataType.Lyricist, "Lyricist" },
            { MetaDataType.Mood, "Mood" },
            { MetaDataType.Occasion, "Occasion" },
            { MetaDataType.Origin, "Origin" },
            { MetaDataType.Publisher, "Publisher" },
            { MetaDataType.Quality, "Quality" },
            { MetaDataType.Rating, "Rating" },
            { MetaDataType.RatingLove, "RatingLove" },
            { MetaDataType.RatingAlbum, "RatingAlbum" },

            { MetaDataType.Tempo, "Tempo" },
            { MetaDataType.TrackNo, "TrackNo" },
            { MetaDataType.TrackCount, "TrackCount" },
            { MetaDataType.Year, "Year" },
//            { MetaDataType.Lyrics, "Lyrics" },

        }; 

        private Titalyver2.MMFMessenger Messenger = new Titalyver2.MMFMessenger();
        private TitalyverG.WebsocketMessenger gMessenger = new TitalyverG.WebsocketMessenger();
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

        private readonly System.Timers.Timer Timer = new System.Timers.Timer(1000.0) { AutoReset = false };
        private const double MusicBee_Delay = 0.3;

        [DataContract]
        public class JsonStruct
        {
            [DataMember]
            public string path;
            [DataMember]
            public string title;
            [DataMember]
            public string[] artists;
            [DataMember]
            public string album;
            [DataMember]
            public double duration;

            [DataMember]
            public Dictionary<string, object> meta = new Dictionary<string, object>();
        };
        private JsonStruct SendData;


        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Titalyver Messenger";
            about.Description = "Send metadata for display lyrics on Titalyver";
            about.Author = "Juna";
            about.TargetApplication = "";   //  the name of a Plugin Storage device or panel header for a dockable panel
            about.Type = PluginType.General;
            about.VersionMajor = 2;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 0;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
//            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();

            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "prompt:";
                TextBox textBox = new TextBox();
                textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
                configPanel.Controls.AddRange(new Control[] { prompt, textBox });
            }
            return false;
        }
       
        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
//            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public async void Close(PluginCloseReason reason)
        {
            Messenger.Update(Titalyver2.MMFMessage.EnumPlaybackEvent.Stop, 0);
            Messenger.Terminalize();

            if (gMessenger.IsValid())
            {
                await gMessenger.Update(TitalyverG.WebsocketMessenger.EnumPlaybackEvent.Stop, 0);
            }
            gMessenger.Terminalize();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    Messenger.Initialize();
                    gMessenger.Initialize();
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    Timer.Elapsed += (s,a)=> { SendPlayMessage(); };

                    break;
                case NotificationType.TrackChanged:
                    {
                        if (!Messenger.IsValid() && !gMessenger.IsValid())
                            return;

                        string url = mbApiInterface.NowPlaying_GetFileUrl();
                        //                    mbApiInterface.NowPlaying_GetFileTag();


                        MetaDataType[] metas = MetaNameDic.Keys.ToArray();
                        string[] output;
                        mbApiInterface.NowPlaying_GetFileTags(metas, out output);

                        JsonStruct data = new JsonStruct
                        {
                            path = url
                        };
                        for (int i = 0; i < metas.Length; i++)
                        {
                            data.meta.Add(MetaNameDic[metas[i]], output[i]);
                        }
                        {
                            int i = Array.IndexOf(metas, MetaDataType.TrackTitle);
                            data.title = i >= 0 ? output[i] : "";
                            i = Array.IndexOf(metas, MetaDataType.Artist);
                            data.artists = new string[] { i >= 0 ? output[i] : "" };
                            i = Array.IndexOf(metas, MetaDataType.Album);
                            data.album = i >= 0 ? output[i] : "";
                            data.duration = mbApiInterface.NowPlaying_GetDuration() / 1000.0;
                        }
                        string lyrics = mbApiInterface.NowPlaying_GetLyrics();
                        if (lyrics == null || lyrics == "")
                            lyrics = mbApiInterface.NowPlaying_GetDownloadedLyrics();
                        data.meta.Add("lyrics", lyrics);

                        //曲の最後まで再生した場合PlayStateChangedが飛んでこない
                        PlayState ps = mbApiInterface.Player_GetPlayState();
                        Titalyver2.MMFMessage.EnumPlaybackEvent pbe = ps == PlayState.Playing ?
                            Titalyver2.MMFMessage.EnumPlaybackEvent.SeekPlay :
                            Titalyver2.MMFMessage.EnumPlaybackEvent.SeekStop;

                        double p = mbApiInterface.Player_GetPosition() / 1000.0 - MusicBee_Delay;
                        using (var ms = new MemoryStream())
                        {
                            var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                            var serializer = new DataContractJsonSerializer(typeof(JsonStruct), settings);
                            serializer.WriteObject(ms, data);
                            byte[] json = ms.ToArray();
                            Messenger.Update(pbe, p, json);
                        }
                        _ = gMessenger.Update((TitalyverG.WebsocketMessenger.EnumPlaybackEvent)pbe, p, data.path, data.title, data.artists, data.album, data.duration, data.meta);

                        SendData = data;

                    }
                    break;
                case NotificationType.PlayStateChanged:
                    {
                        SendPlayMessage();
                    }
                    break;
                case NotificationType.NowPlayingLyricsReady:
// NowPlaying_GetDownloadedLyrics()を呼んで失敗（というか遅延？）したら飛んでくる（かもしれない）らしい
                    {
                        if (!Messenger.IsValid() && !gMessenger.IsValid())
                            return;
                        string lyrics = mbApiInterface.NowPlaying_GetDownloadedLyrics();
                        if (lyrics == null || lyrics == "")
                            return;
                        SendData.meta["lyrics"] = lyrics;

                        PlayState ps = mbApiInterface.Player_GetPlayState();
                        Titalyver2.MMFMessage.EnumPlaybackEvent pbe = ps == PlayState.Playing ?
                        Titalyver2.MMFMessage.EnumPlaybackEvent.SeekPlay :
                        Titalyver2.MMFMessage.EnumPlaybackEvent.SeekStop;
                        double p = mbApiInterface.Player_GetPosition() / 1000.0 - MusicBee_Delay;
                        using (var ms = new MemoryStream())
                        {
                            var settings = new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true };
                            var serializer = new DataContractJsonSerializer(typeof(JsonStruct), settings);
                            serializer.WriteObject(ms, SendData);
                            byte[] json = ms.ToArray();
                            Messenger.Update(pbe, p, json);
                        }
                        _ = gMessenger.Update((TitalyverG.WebsocketMessenger.EnumPlaybackEvent)pbe, p);
                    }
                    break;
            }
        }


        private void SendPlayMessage()
        {
            if (!Messenger.IsValid() && !gMessenger.IsValid())
                return;
            PlayState ps = mbApiInterface.Player_GetPlayState();
            double p = mbApiInterface.Player_GetPosition() / 1000.0 - MusicBee_Delay;
            switch (ps)
            {
                case PlayState.Undefined:
                    break;
                case PlayState.Loading:
                    break;
                case PlayState.Playing:
                    Messenger.Update(Titalyver2.MMFMessage.EnumPlaybackEvent.SeekPlay, p);
                    _ = gMessenger.Update(TitalyverG.WebsocketMessenger.EnumPlaybackEvent.SeekPlay, p);
                    Timer.Start();
                    break;
                case PlayState.Paused:
                    Messenger.Update(Titalyver2.MMFMessage.EnumPlaybackEvent.SeekStop, p);
                    _ = gMessenger.Update(TitalyverG.WebsocketMessenger.EnumPlaybackEvent.SeekStop, p);
                    Timer.Stop();
                    break;
                case PlayState.Stopped:
                    Messenger.Update(Titalyver2.MMFMessage.EnumPlaybackEvent.Stop, p);
                    _ = gMessenger.Update(TitalyverG.WebsocketMessenger.EnumPlaybackEvent.Stop, p);
                    Timer.Stop();
                    break;
            }
        }

        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        //public string[] GetProviders()
        //{
        //    return null;
        //}

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        //public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        //{
        //    return null;
        //}

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        //public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        //{
        //    //Return Convert.ToBase64String(artworkBinaryData)
        //    return null;
        //}

        //  presence of this function indicates to MusicBee that this plugin has a dockable panel. MusicBee will create the control and pass it as the panel parameter
        //  you can add your own controls to the panel if needed
        //  you can control the scrollable area of the panel using the mbApiInterface.MB_SetPanelScrollableArea function
        //  to set a MusicBee header for the panel, set about.TargetApplication in the Initialise function above to the panel header text
        //public int OnDockablePanelCreated(Control panel)
        //{
        //  //    return the height of the panel and perform any initialisation here
        //  //    MusicBee will call panel.Dispose() when the user removes this panel from the layout configuration
        //  //    < 0 indicates to MusicBee this control is resizable and should be sized to fill the panel it is docked to in MusicBee
        //  //    = 0 indicates to MusicBee this control resizeable
        //  //    > 0 indicates to MusicBee the fixed height for the control.Note it is recommended you scale the height for high DPI screens(create a graphics object and get the DpiY value)
        //    float dpiScaling = 0;
        //    using (Graphics g = panel.CreateGraphics())
        //    {
        //        dpiScaling = g.DpiY / 96f;
        //    }
        //    panel.Paint += panel_Paint;
        //    return Convert.ToInt32(100 * dpiScaling);
        //}

        // presence of this function indicates to MusicBee that the dockable panel created above will show menu items when the panel header is clicked
        // return the list of ToolStripMenuItems that will be displayed
        //public List<ToolStripItem> GetHeaderMenuItems()
        //{
        //    List<ToolStripItem> list = new List<ToolStripItem>();
        //    list.Add(new ToolStripMenuItem("A menu item"));
        //    return list;
        //}

        //private void panel_Paint(object sender, PaintEventArgs e)
        //{
        //    e.Graphics.Clear(Color.Red);
        //    TextRenderer.DrawText(e.Graphics, "hello", SystemFonts.CaptionFont, new Point(10, 10), Color.Blue);
        //}

    }
}