﻿using System.Windows;
using System.Windows.Media;
using Microsoft.Practices.Composite.Logging;
using Tobi.Common;
using Tobi.Common.MVVM;
using Tobi.Common.MVVM.Command;
using Tobi.Common.UI;

namespace Tobi.Plugin.AudioPane
{
    public partial class AudioPaneViewModel
    {
        #region Commands

        public RichDelegateCommand CommandAudioSettings { get; private set; }

#if DEBUG
        public RichDelegateCommand CommandShowOptionsDialog { get; private set; }
#endif //DEBUG

        public RichDelegateCommand CommandFocus { get; private set; }
        public RichDelegateCommand CommandFocusStatusBar { get; private set; }
        
        public RichDelegateCommand CommandZoomSelection { get; private set; }
        public RichDelegateCommand CommandZoomFitFull { get; private set; }
        public RichDelegateCommand CommandRefresh { get; private set; }

        private void initializeCommands_View()
        {
            CommandRefresh = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_Reload,
                Tobi_Plugin_AudioPane_Lang.Audio_Reload_,
                null, // KeyGesture obtained from settings (see last parameters below)
                null, //ScalableGreyableImageProvider.ConvertIconFormat((DrawingImage)Application.Current.FindResource("Horizon_Image_Refresh")),
                //shellView.LoadTangoIcon("view-refresh"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandRefresh", Category.Debug, Priority.Medium);

                    StartWaveFormLoadTimer(500, IsAutoPlay);
                },
                () => !IsWaveFormLoading,
                null, null); //IsAudioLoaded

            m_ShellView.RegisterRichCommand(CommandRefresh);
            //
            CommandZoomSelection = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_ZoomSelection,
                Tobi_Plugin_AudioPane_Lang.Audio_ZoomSelection_,
                null, // KeyGesture obtained from settings (see last parameters below)
                ScalableGreyableImageProvider.ConvertIconFormat((DrawingImage)Application.Current.FindResource("Horizon_Image_Search")),
                //shellView.LoadTangoIcon("system-search"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandZoomSelection", Category.Debug, Priority.Medium);

                    View.ZoomSelection();
                },
                () => View != null && !IsWaveFormLoading && IsSelectionSet,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Audio_ZoomSelection));

            m_ShellView.RegisterRichCommand(CommandZoomSelection);
            //
            CommandZoomFitFull = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_FitFull,
                Tobi_Plugin_AudioPane_Lang.Audio_FitFull_,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon("Neu_utilities-system-monitor"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandZoomFitFull", Category.Debug, Priority.Medium);

                    View.ZoomFitFull();
                },
                () => View != null && !IsWaveFormLoading,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Audio_ZoomFitFull));

            m_ShellView.RegisterRichCommand(CommandZoomFitFull);
            //
            //
            CommandAudioSettings = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_Settings,
                Tobi_Plugin_AudioPane_Lang.Audio_Settings_,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon("Neu_audio-x-generic"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandAudioSettings", Category.Debug, Priority.Medium);

                    var windowPopup = new PopupModalWindow(m_ShellView,
                                                           UserInterfaceStrings.EscapeMnemonic(
                                                               UserInterfaceStrings.Audio_Settings),
                                                           new AudioSettings(this),
                                                           PopupModalWindow.DialogButtonsSet.Close,
                                                           PopupModalWindow.DialogButton.Close,
                                                           true, 500, 150, null, 0);

                    windowPopup.ShowFloating(null);
                },
                () => true,
                Settings_KeyGestures.Default,
                null //PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Audio_ShowOptions)
                );

            m_ShellView.RegisterRichCommand(CommandAudioSettings);
            //
#if DEBUG
            CommandShowOptionsDialog = new RichDelegateCommand(
                UserInterfaceStrings.Audio_ShowOptions,
                null,
                null, // KeyGesture obtained from settings (see last parameters below)
                null,
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandShowOptionsDialog", Category.Debug, Priority.Medium);

                    //var window = shellView.View as Window;

                    var pane = new AudioOptions { DataContext = this };

                    var windowPopup = new PopupModalWindow(m_ShellView,
                                                           UserInterfaceStrings.EscapeMnemonic(Tobi_Plugin_AudioPane_Lang.Audio_ShowOptions),
                                                           pane,
                                                           PopupModalWindow.DialogButtonsSet.Close,
                                                           PopupModalWindow.DialogButton.Close,
                                                           true, 400, 500, null, 0);
                    windowPopup.Show();
                },
                () => true,
                Settings_KeyGestures.Default,
                null //PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Audio_ShowOptions)
                );

            m_ShellView.RegisterRichCommand(CommandShowOptionsDialog);
#endif //DEBUG
            //
            CommandFocus = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_Focus,
                null,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadTangoIcon("audio-volume-low"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandFocus", Category.Debug, Priority.Medium);

                    View.BringIntoFocus();
                },
                () => View != null,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Focus_Audio));

            m_ShellView.RegisterRichCommand(CommandFocus);
            //
            CommandFocusStatusBar = new RichDelegateCommand(
                Tobi_Plugin_AudioPane_Lang.Audio_FocusStatusBar,
                null,
                null, // KeyGesture obtained from settings (see last parameters below)
                m_ShellView.LoadGnomeNeuIcon("Neu_utilities-terminal"),
                ()=>
                {
                    Logger.Log("AudioPaneViewModel.CommandFocusStatusBar", Category.Debug, Priority.Medium);

                    View.BringIntoFocusStatusBar();
                },
                () => View != null,
                Settings_KeyGestures.Default,
                PropertyChangedNotifyBase.GetMemberName(() => Settings_KeyGestures.Default.Keyboard_Focus_StatusBar));

            m_ShellView.RegisterRichCommand(CommandFocusStatusBar);
            //
        }

        private void initializeCommands()
        {
            Logger.Log("AudioPaneViewModel.initializeCommands", Category.Debug, Priority.Medium);

            initializeCommands_Selection();
            initializeCommands_Recorder();
            initializeCommands_Player();
            initializeCommands_Edit();
            initializeCommands_Navigation();

            initializeCommands_View();

            if (View != null)
            {
                View.InitGraphicalCommandBindings();
            }
        }

        #endregion Commands
    }
}