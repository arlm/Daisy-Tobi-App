﻿using System.ComponentModel;
using System.Drawing;
using System.Windows.Input;
using Microsoft.Practices.Composite.Logging;
using Tobi.Common.MVVM;

namespace Tobi
{
    public partial class Shell
    {
        //m_PropertyChangeHandler.RaisePropertyChanged(() => WindowTitle);
        //public double SettingsWindowShellHeight
        //{
        //    get
        //    {
        //        return Settings.Default.WindowShellSize.Height;
        //    }
        //    set
        //    {
        //        Settings.Default.WindowShellSize = new Size(Settings.Default.WindowShellSize.Width, value);
        //    }
        //}

        private bool m_SettingsDone;
        private void trySettings()
        {
            if (!m_SettingsDone && m_SettingsAggregator != null)
            {
                m_SettingsDone = true;

                m_Logger.Log(@"settings applied to Shell Window", Category.Debug, Priority.Medium);
            }
        }

        private void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!e.PropertyName.StartsWith(@"Keyboard_")) return;

            foreach (var command in m_listOfRegisteredRichCommands)
            {
                if (command.KeyGestureSettingName == e.PropertyName)
                {
                    command.RefreshKeyGestureSetting();
                }
            }
        }

        private void saveSettings()
        {
            // NOTE: not needed because of TwoWay Data Binding in the XAML
            //if (WindowState == WindowState.Maximized)
            //{
            //    // Use the RestoreBounds as the current values will be 0, 0 and the size of the screen
            //    Settings.Default.WindowShellHeight = RestoreBounds.Height;
            //    Settings.Default.WindowShellWidth = RestoreBounds.Width;
            //    Settings.Default.WindowShellFullScreen = true;
            //}
            //else
            //{
            //    Settings.Default.WindowShellHeight = Height;
            //    Settings.Default.WindowShellWidth = Width;
            //    Settings.Default.WindowShellFullScreen = false;
            //}

            if (m_SettingsAggregator != null)
            {
                m_SettingsAggregator.SaveAll();
            }
        }
    }
}
